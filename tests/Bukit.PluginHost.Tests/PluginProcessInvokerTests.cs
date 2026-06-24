using Bukit.PluginProcessProbe;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginProcessInvokerTests
{
    [Fact]
    public async Task InvokeAsync_DelegatesToRunnerWithJsonInputAndLimits()
    {
        var runner = new RecordingProcessRunner();
        var invoker = new PluginProcessInvoker(runner);
        var request = new PluginProcessRequest(
            ExecutablePath: "/plugins/echo/bin/osx-arm64/bukit-plugin-echo",
            Arguments: ["--mode", "stdio"],
            StandardInputJson: """{"type":"handshake"}""",
            WorkingDirectory: "/plugins/echo",
            Timeout: TimeSpan.FromSeconds(5),
            StdoutMaxBytes: 123,
            StderrMaxBytes: 456);

        PluginProcessResult result = await invoker.InvokeAsync(request, CancellationToken.None);

        Assert.Equal("""{"ok":true}""", result.StdoutJson);
        Assert.Equal("log", result.Stderr);
        Assert.NotNull(runner.Request);
        Assert.Equal(request.ExecutablePath, runner.Request.ExecutablePath);
        Assert.Equal(request.Arguments, runner.Request.Arguments);
        Assert.Equal(request.StandardInputJson, runner.Request.StandardInput);
        Assert.Equal(request.WorkingDirectory, runner.Request.WorkingDirectory);
        Assert.Equal(request.Timeout, runner.Request.Timeout);
        Assert.Equal(123, runner.Request.StdoutMaxBytes);
        Assert.Equal(456, runner.Request.StderrMaxBytes);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public ProcessRunRequest? Request { get; private set; }

        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                Stdout: """{"ok":true}""",
                Stderr: "log",
                TimedOut: false,
                OutputLimitExceeded: false));
        }
    }
}
