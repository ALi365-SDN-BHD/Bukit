using System.Diagnostics;
using Bukit.PluginProcessProbe;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class SystemProcessRunnerTests
{
    [Fact]
    public async Task WaitForTerminationGraceAsync_IncompleteCleanup_ReturnsFalseWithinGrace()
    {
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();

        var wait = SystemProcessRunner.WaitForTerminationGraceAsync(
            never.Task,
            TimeSpan.FromMilliseconds(50));
        var completed = await wait.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(completed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RunAsync_WritesStdinAndCapturesStdoutAndStderr()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["echo"], stdin: """{"op":"handshake"}"""),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.OutputLimitExceeded);
        Assert.Equal("""{"op":"handshake"}""", result.Stdout);
        Assert.Equal("stderr-log", result.Stderr);
    }

    [Fact]
    public async Task RunAsync_CapturesNonZeroExit()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["exit", "7"]),
            CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
        Assert.Equal("stdout-before-exit", result.Stdout);
        Assert.Equal("stderr-before-exit", result.Stderr);
    }

    [Fact]
    public async Task RunAsync_EnforcesTimeout()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["sleep", "5000"], timeoutMs: 100),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_TimeoutBoundsBlockedStdinWriteAndKillsProcess()
    {
        using TestDirectory directory = TestDirectory.Create();
        string markerPath = System.IO.Path.Combine(directory.Path, "timeout-completed.txt");
        string largeInput = new('x', 8 * 1024 * 1024);
        var runner = new SystemProcessRunner();
        var stopwatch = Stopwatch.StartNew();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(
                arguments: ["ignore-stdin-then-mark", "30000", markerPath],
                stdin: largeInput,
                timeoutMs: 150),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        stopwatch.Stop();
        Assert.True(result.TimedOut);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(markerPath));
    }

    [Fact]
    public async Task RunAsync_CancellationDuringBlockedStdinWriteKillsProcess()
    {
        using TestDirectory directory = TestDirectory.Create();
        string markerPath = System.IO.Path.Combine(directory.Path, "cancel-completed.txt");
        string largeInput = new('x', 8 * 1024 * 1024);
        var runner = new SystemProcessRunner();
        using var cts = new CancellationTokenSource(150);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(
                ProbeRequest(
                    arguments: ["ignore-stdin-then-mark", "1000", markerPath],
                    stdin: largeInput,
                    timeoutMs: 10000),
                cts.Token).WaitAsync(TimeSpan.FromSeconds(5)));

        await Task.Delay(1200);
        Assert.False(File.Exists(markerPath));
    }

    [Fact]
    public async Task RunAsync_ChildExitDuringStdinWriteReturnsExitCode()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(
                arguments: ["exit-without-reading-stdin", "7"],
                stdin: new string('x', 8 * 1024 * 1024)),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(7, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_EnforcesStdoutLimit()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["stdout-bytes", "64"], stdoutMaxBytes: 16),
            CancellationToken.None);

        Assert.True(result.OutputLimitExceeded);
        Assert.Equal(ProcessOutputStream.Stdout, result.OutputLimitStream);
    }

    [Fact]
    public async Task RunAsync_EnforcesStderrLimit()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["stderr-bytes", "64"], stderrMaxBytes: 16),
            CancellationToken.None);

        Assert.True(result.OutputLimitExceeded);
        Assert.Equal(ProcessOutputStream.Stderr, result.OutputLimitStream);
    }

    [Fact]
    public async Task RunAsync_Timeout_KillsTreeAndBoundsStreamDrain()
    {
        var runner = new SystemProcessRunner();
        var stopwatch = Stopwatch.StartNew();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["sleep", "30000"], timeoutMs: 200),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        stopwatch.Stop();
        Assert.True(result.TimedOut);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            $"Timeout + drain took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 8s");
    }

    [Fact]
    public async Task RunAsync_Timeout_KillsDescendantHoldingInheritedPipes()
    {
        var runner = new SystemProcessRunner();
        var stopwatch = Stopwatch.StartNew();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["spawn-inherited-pipe", "30000"], timeoutMs: 200),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        stopwatch.Stop();
        Assert.True(result.TimedOut);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            $"Timeout + inherited-pipe drain took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 8s");
    }

    [Fact]
    public async Task RunAsync_Cancellation_KillsTreeAndBoundsStreamDrain()
    {
        var runner = new SystemProcessRunner();
        using var cts = new CancellationTokenSource(200);
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(
                ProbeRequest(arguments: ["sleep", "30000"], timeoutMs: 30000),
                cts.Token).WaitAsync(TimeSpan.FromSeconds(10)));

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            $"Cancellation + drain took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 8s");
    }

    [Fact]
    public async Task RunAsync_Cancellation_KillsDescendantHoldingInheritedPipes()
    {
        var runner = new SystemProcessRunner();
        using var cts = new CancellationTokenSource(200);
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(
                ProbeRequest(arguments: ["spawn-inherited-pipe", "30000"], timeoutMs: 30000),
                cts.Token).WaitAsync(TimeSpan.FromSeconds(10)));

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            $"Cancellation + inherited-pipe drain took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 8s");
    }

    [Fact]
    public async Task RunAsync_PropagatesCancellation()
    {
        var runner = new SystemProcessRunner();
        using var cts = new CancellationTokenSource(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(ProbeRequest(arguments: ["sleep", "5000"], timeoutMs: 10000), cts.Token));
    }

    [Fact]
    public async Task RunAsync_DoesNotCorruptUtf8Output()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(arguments: ["utf8"]),
            CancellationToken.None);

        Assert.Equal("你好", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_DoesNotInheritParentEnvironment()
    {
        const string secretName = "BUKIT_PLUGIN_RUNNER_SECRET";
        Environment.SetEnvironmentVariable(secretName, "secret-value");
        var runner = new SystemProcessRunner();

        try
        {
            ProcessRunResult result = await runner.RunAsync(
                ProbeRequest(arguments: ["env", secretName]),
                CancellationToken.None);

            Assert.Equal("<missing>", result.Stdout);
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, null);
        }
    }

    [Fact]
    public async Task RunAsync_PassesExplicitEnvironmentVariables()
    {
        var runner = new SystemProcessRunner();

        ProcessRunResult result = await runner.RunAsync(
            ProbeRequest(
                arguments: ["env", "BUKIT_PLUGIN_ALLOWED_ENV"],
                environmentVariables: new Dictionary<string, string?> { ["BUKIT_PLUGIN_ALLOWED_ENV"] = "allowed" }),
            CancellationToken.None);

        Assert.Equal("allowed", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_ReleasesCompletedProcessWorkingDirectory()
    {
        string workingDirectory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"bukit-process-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var runner = new SystemProcessRunner();

            ProcessRunResult result = await runner.RunAsync(
                ProbeRequest(
                    arguments: ["echo"],
                    stdin: """{"op":"handshake"}""",
                    workingDirectory: workingDirectory),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Directory.Delete(workingDirectory);
            Assert.False(Directory.Exists(workingDirectory));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_ChildCpuExceedsTreeLimit_ReturnsResourceLimitExceeded()
    {
        var workingDirectory = CreateWorkingDirectory();
        var markerPath = Path.Combine(workingDirectory, "cpu-child.pid");
        try
        {
            var runner = new SystemProcessRunner();
            ProcessRunResult result = await runner.RunAsync(
                ProbeRequest(
                    arguments: ["spawn-cpu-child", markerPath, "15000"],
                    timeoutMs: 30000,
                    maxCpuTime: TimeSpan.FromSeconds(1)),
                CancellationToken.None);

            Assert.NotNull(result.ResourceLimitExceeded);
            Assert.NotEqual(0, result.ExitCode);

            // The burning child (grandchild of the runner) must be gone: limits apply
            // to the whole process tree, not only the plugin parent.
            var childPid = int.Parse(await File.ReadAllTextAsync(markerPath));
            await WaitForProcessExitAsync(childPid, TimeSpan.FromSeconds(5));
            Assert.False(IsProcessAlive(childPid));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_ChildMemoryExceedsTreeLimit_ReturnsResourceLimitExceeded()
    {
        var workingDirectory = CreateWorkingDirectory();
        var markerPath = Path.Combine(workingDirectory, "memory-child.pid");
        try
        {
            var runner = new SystemProcessRunner();
            ProcessRunResult result = await runner.RunAsync(
                ProbeRequest(
                    arguments: ["spawn-memory-child", markerPath, "384", "10000"],
                    timeoutMs: 30000,
                    maxMemoryBytes: 256L * 1024 * 1024),
                CancellationToken.None);

            Assert.NotNull(result.ResourceLimitExceeded);
            Assert.NotEqual(0, result.ExitCode);

            var childPid = int.Parse(await File.ReadAllTextAsync(markerPath));
            await WaitForProcessExitAsync(childPid, TimeSpan.FromSeconds(5));
            Assert.False(IsProcessAlive(childPid));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    private static string CreateWorkingDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-tree-limits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task WaitForProcessExitAsync(int pid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (IsProcessAlive(pid) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    private static ProcessRunRequest ProbeRequest(
        IReadOnlyList<string> arguments,
        string stdin = "",
        int timeoutMs = 5000,
        int stdoutMaxBytes = 4096,
        int stderrMaxBytes = 4096,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        string? workingDirectory = null,
        TimeSpan? maxCpuTime = null,
        long? maxMemoryBytes = null)
    {
        string? dotnet = Process.GetCurrentProcess().MainModule?.FileName;
        Assert.False(string.IsNullOrWhiteSpace(dotnet));

        string probeAssembly = typeof(ProbeMarker).Assembly.Location;
        return new ProcessRunRequest(
            ExecutablePath: dotnet,
            Arguments: [probeAssembly, .. arguments],
            StandardInput: stdin,
            WorkingDirectory: workingDirectory
                ?? System.IO.Path.GetDirectoryName(probeAssembly)!,
            Timeout: TimeSpan.FromMilliseconds(timeoutMs),
            StdoutMaxBytes: stdoutMaxBytes,
            StderrMaxBytes: stderrMaxBytes,
            EnvironmentVariables: environmentVariables,
            MaxCpuTime: maxCpuTime,
            MaxMemoryBytes: maxMemoryBytes);
    }
}
