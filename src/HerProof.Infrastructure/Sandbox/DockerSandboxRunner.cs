using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly TimeSpan WallClockTimeout = TimeSpan.FromMinutes(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string ImageNameFor(SandboxLanguage language) => language switch
    {
        SandboxLanguage.Java => "herproof-sandbox-java",
        _ => "herproof-sandbox",
    };

    public async Task<SandboxExecutionResult> RunAsync(string readOnlyInputDirectory, SandboxLanguage language = SandboxLanguage.CSharp, CancellationToken cancellationToken = default)
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
            // uid/gid/mode are required, not cosmetic: a bare --tmpfs mount is root-owned, and this
            // container runs as --user 10001:10001 below - without this the entrypoint's own first
            // mkdir fails with "Permission denied" before it ever reaches candidate code at all.
            // Confirmed live: restore/build/test all genuinely passed only after adding this.
            "--tmpfs", "/work:rw,size=1024m,exec,uid=10001,gid=10001,mode=1777",
            // The base image's own /tmp is baked into the read-only root filesystem, so it's not
            // writable at runtime. dotnet's first-time-use setup creates a named Mutex ("NuGet-
            // Migrations") whose lock file goes there regardless of TMPDIR - without a real tmpfs at
            // /tmp specifically, that Mutex creation throws before restore/build/test ever runs.
            // Confirmed live: this was the actual cause of every run coming back with restore_ok:false.
            "--tmpfs", "/tmp:rw,size=64m,uid=10001,gid=10001,mode=1777",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            "--pids-limit", "256",
            "--memory", "1g",
            "--cpus", "1",
            "--user", "10001:10001",
            // Docker Desktop on Windows leaks the host's own HOME/USERPROFILE-derived value into the
            // container's environment (observed live: a mangled Windows path like "C:Usersakonisaho"),
            // overriding the image's real, read-only /home/runner. NuGet's first-time-use migration
            // then tries to create its lock file under that bogus, unwritable HOME and throws before
            // restore ever runs. Pinning HOME explicitly to the writable /work closes that off for good.
            "--env", "HOME=/work",
            "-v", $"{readOnlyInputDirectory}:/input:ro",
            ImageNameFor(language),
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

        try
        {
            process.Start();

            // Read concurrently with waiting for exit, not via OutputDataReceived/BeginOutputReadLine:
            // that event-based approach races WaitForExitAsync - the awaited task can complete before
            // the redirected pipes have finished draining, silently losing output (confirmed live: every
            // automated run came back with a fully empty log excerpt despite the exact same docker
            // invocation succeeding, with real output, when reproduced manually). Reading the stream
            // directly (bounded, so a flood of stdout can't exhaust the worker's own memory) has no such
            // race - it only finishes once the pipe has actually hit EOF.
            var stdoutTask = ReadBoundedAsync(process.StandardOutput, cancellationToken);
            var stderrTask = ReadBoundedAsync(process.StandardError, cancellationToken);

            using var timeoutCts = new CancellationTokenSource(WallClockTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await Task.WhenAll(process.WaitForExitAsync(linkedCts.Token), stdoutTask, stderrTask);

            return ParseReport(await stdoutTask, await stderrTask);
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

    private static string Truncate(string value) => value.Length > 4000 ? value[^4000..] : value;

    /// <summary>Reads a stream to the end while keeping only the last ~8000 characters in memory, so a
    /// submission that floods stdout can't exhaust the worker process's own memory while we wait for it
    /// to finish or time out (that would just move the attack from the container to the host). The
    /// sandbox's JSON report is always the last line, so keeping the tail - not the head, not an
    /// unbounded buffer - is what actually matters here.</summary>
    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        const int TrimThreshold = 16_000;
        const int KeepLength = 8_000;
        var builder = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            builder.Append(buffer, 0, read);
            if (builder.Length > TrimThreshold)
            {
                builder.Remove(0, builder.Length - KeepLength);
            }
        }

        return builder.ToString();
    }

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

    // The entrypoint script emits snake_case keys (restore_ok, build_ok, ...), not the camelCase
    // JsonSerializerDefaults.Web expects - without these attributes, every property silently fell
    // back to its default (false/null) on every run, even a fully successful one: confirmed live,
    // raw stdout showed a genuine pass while the deserialized DTO came back entirely false/empty.
    private record SandboxReportDto(
        [property: JsonPropertyName("restore_ok")] bool RestoreOk,
        [property: JsonPropertyName("build_ok")] bool BuildOk,
        [property: JsonPropertyName("tests_ok")] bool TestsOk,
        [property: JsonPropertyName("log_excerpt")] string? LogExcerpt);
}
