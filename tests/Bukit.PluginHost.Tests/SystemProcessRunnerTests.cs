using System.Diagnostics;
using Bukit.PluginProcessProbe;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class SystemProcessRunnerTests
{
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

    private static ProcessRunRequest ProbeRequest(
        IReadOnlyList<string> arguments,
        string stdin = "",
        int timeoutMs = 5000,
        int stdoutMaxBytes = 4096,
        int stderrMaxBytes = 4096,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        string? workingDirectory = null)
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
            EnvironmentVariables: environmentVariables);
    }
}
