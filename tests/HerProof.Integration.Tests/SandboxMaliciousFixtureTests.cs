using System.Diagnostics;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>The malicious-fixture tests docs/05 P09 requires: fork bomb, huge output, and a network
/// call attempt must all be contained by the real sandbox. These probe the container security
/// boundary directly (shell entrypoint override, the same hardening flags docker/Dockerfile.sandbox
/// documents) rather than going through the dotnet restore/build/test pipeline, because that pipeline
/// needs network to restore NuGet packages and the sandbox has none — restore failing offline is the
/// correct, honest behaviour (docs/01 section 15), not something to route a malicious payload through.
///
/// Opt-in only (HERPROOF_SANDBOX_TESTS=1): needs a real Docker daemon and the built herproof-sandbox
/// image, which CI and most dev machines can't assume. Run locally:
///   docker build -f docker/Dockerfile.sandbox -t herproof-sandbox .
///   HERPROOF_SANDBOX_TESTS=1 dotnet test --filter SandboxMaliciousFixtureTests</summary>
public class SandboxMaliciousFixtureTests
{
    private static bool SandboxTestsEnabled => Environment.GetEnvironmentVariable("HERPROOF_SANDBOX_TESTS") == "1";

    private static async Task<(int ExitCode, string Stdout, string Stderr, TimeSpan Elapsed)> RunInHardenedContainerAsync(
        string shellCommand, TimeSpan timeout, string shell = "sh")
    {
        var containerName = $"herproof-sandbox-fixture-{Guid.NewGuid():N}";
        string[] args =
        [
            "run", "--rm",
            "--name", containerName,
            "--entrypoint", shell,
            "--network", "none",
            "--read-only",
            "--tmpfs", "/work:rw,size=64m,exec",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            "--pids-limit", "64",
            "--memory", "256m",
            "--cpus", "0.5",
            "--user", "10001:10001",
            "herproof-sandbox",
            "-c", shellCommand,
        ];

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false },
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        var stopwatch = Stopwatch.StartNew();
        var stdout = new List<string>();
        var stderr = new List<string>();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null && stdout.Count < 1000) stdout.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null && stderr.Count < 1000) stderr.Add(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // The test itself timing out the client is a failure of containment we want to see, not
            // hide — but still clean up the container so it doesn't linger.
            using var kill = Process.Start(new ProcessStartInfo("docker", ["kill", containerName]) { UseShellExecute = false });
            if (kill is not null)
            {
                await kill.WaitForExitAsync(CancellationToken.None);
            }

            return (-1, string.Join('\n', stdout), "client-side timeout: container did not stop in time", stopwatch.Elapsed);
        }

        return (process.ExitCode, string.Join('\n', stdout), string.Join('\n', stderr), stopwatch.Elapsed);
    }

    [Fact]
    public async Task Fork_bomb_is_contained_and_the_container_still_exits()
    {
        if (!SandboxTestsEnabled)
        {
            return;
        }

        // The classic exponential fork bomb: each process spawns two more, doubling. Without
        // --pids-limit this can take down the host; with it, the container hits the cap and exits
        // almost instantly (verified manually: ~0.5s with --pids-limit 64 on this same image).
        const string bomb = ":(){ :|:& };:";

        var (_, _, _, elapsed) = await RunInHardenedContainerAsync(bomb, TimeSpan.FromSeconds(10), shell: "bash");

        Assert.True(elapsed < TimeSpan.FromSeconds(5), $"Container took {elapsed} — pids-limit may not be containing the fork bomb.");
    }

    [Fact]
    public async Task Huge_output_does_not_prevent_the_container_from_completing()
    {
        if (!SandboxTestsEnabled)
        {
            return;
        }

        // Flood stdout well beyond our capped-buffer size (see DockerSandboxRunner.CappedBuffer).
        const string flood = "yes 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx' | head -c 50000000";

        var (exitCode, _, _, elapsed) = await RunInHardenedContainerAsync(flood, TimeSpan.FromSeconds(20));

        Assert.Equal(0, exitCode);
        Assert.True(elapsed < TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Network_call_attempt_fails_because_the_container_has_no_network()
    {
        if (!SandboxTestsEnabled)
        {
            return;
        }

        const string command = "wget -T 5 -O - http://example.com 2>&1 || echo NETWORK_BLOCKED";

        var (_, stdout, _, _) = await RunInHardenedContainerAsync(command, TimeSpan.FromSeconds(15));

        Assert.Contains("NETWORK_BLOCKED", stdout, StringComparison.Ordinal);
    }
}
