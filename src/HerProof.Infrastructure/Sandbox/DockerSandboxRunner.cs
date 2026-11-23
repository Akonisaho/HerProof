using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HerProof.Application.Sandbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HerProof.Infrastructure.Sandbox;

/// <summary>Shells out to the `docker` CLI rather than a client library, so the exact invocation
/// matches the one documented and reviewable in docker/Dockerfile.sandbox's header comment — no
/// surface for the hardening flags to drift from what's actually audited. The web host never calls
/// this directly (CLAUDE.md rule 6); only HerProof.AssessmentWorker does.</summary>
public class DockerSandboxRunner(IConfiguration configuration, ILogger<DockerSandboxRunner> logger) : ISandboxRunner
{
    private const string ImageName = "herproof-sandbox";
    private static readonly TimeSpan WallClockTimeout = TimeSpan.FromMinutes(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SandboxExecutionResult> RunAsync(string readOnlyInputDirectory, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue<bool?>("Execution:Enabled") ?? false;
        if (!enabled)
        {
            // The documented hackathon fallback (docs/03 section 5, docs/01 section 15), not a bug:
            // static analysis evidence still stands on its own.
            return new SandboxExecutionResult(false, false, false, false, "not executed: Execution__Enabled is false", null);
        }

        var containerName = $"herproof-sandbox-{Guid.NewGuid():N}";
        string[] args =
        [
            "run", "--rm",
            "--name", containerName,
            "--network", "none",
            "--read-only",
            "--tmpfs", "/work:rw,size=512m,exec",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            "--pids-limit", "256",
            "--memory", "1g",
            "--cpus", "1",
            "--user", "10001:10001",
            "-v", $"{readOnlyInputDirectory}:/input:ro",
            ImageName,
        ];

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        // Capped while streaming, not just truncated at the end: a malicious program that floods
        // stdout must not be able to exhaust the worker process's own memory while we wait for it to
        // finish or time out (that would just move the attack from the container to the host).
        var stdout = new CappedBuffer();
        var stderr = new CappedBuffer();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.Append(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.Append(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(WallClockTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Sandbox container {ContainerName} exceeded its wall-clock timeout; killing it.", containerName);
            // Kill the actual container by name: killing the `docker` CLI client process does not,
            // by itself, stop a detached container running on the daemon.
            await KillContainerAsync(containerName);
            TryKill(process);
            return new SandboxExecutionResult(true, false, false, false, "sandbox timed out and was killed", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start or run the sandbox container.");
            return new SandboxExecutionResult(false, false, false, false, $"sandbox unavailable: {ex.Message}", null);
        }

        return ParseReport(stdout.ToString(), stderr.ToString());
    }

    private static SandboxExecutionResult ParseReport(string stdout, string stderr)
    {
        var jsonLine = stdout.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(line => line.StartsWith('{'));

        if (jsonLine is null)
        {
            return new SandboxExecutionResult(true, false, false, false, Truncate(stderr.Length > 0 ? stderr : stdout), null);
        }

        try
        {
            var report = JsonSerializer.Deserialize<SandboxReportDto>(jsonLine, JsonOptions);
            if (report is null)
            {
                return new SandboxExecutionResult(true, false, false, false, "sandbox returned an empty report", null);
            }

            return new SandboxExecutionResult(true, report.RestoreOk, report.BuildOk, report.TestsOk, Truncate(report.LogExcerpt ?? string.Empty), null);
        }
        catch (JsonException)
        {
            return new SandboxExecutionResult(true, false, false, false, "sandbox report was not valid JSON — discarded, not trusted (docs/03 rule 2)", null);
        }
    }

    private static string Truncate(string value) => value.Length > 4000 ? value[..4000] : value;

    private static async Task KillContainerAsync(string containerName)
    {
        try
        {
            using var killProcess = Process.Start(new ProcessStartInfo("docker", ["kill", containerName])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (killProcess is not null)
            {
                await killProcess.WaitForExitAsync();
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup; --rm plus the daemon's own container lifecycle will eventually reclaim it.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private record SandboxReportDto(bool RestoreOk, bool BuildOk, bool TestsOk, string? LogExcerpt);

    /// <summary>Keeps only the most recent ~8000 characters, trimming from the front once a 16000-char
    /// threshold is hit. The sandbox's JSON report is always the last line, so keeping the tail (not
    /// the head, and not an unbounded buffer) is what actually matters here.</summary>
    private sealed class CappedBuffer
    {
        private const int TrimThreshold = 16_000;
        private const int KeepLength = 8_000;
        private readonly StringBuilder _builder = new();
        private readonly object _lock = new();

        public void Append(string line)
        {
            lock (_lock)
            {
                _builder.Append(line).Append('\n');
                if (_builder.Length > TrimThreshold)
                {
                    _builder.Remove(0, _builder.Length - KeepLength);
                }
            }
        }

        public override string ToString()
        {
            lock (_lock)
            {
                return _builder.ToString();
            }
        }
    }
}
