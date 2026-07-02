namespace Bukit.PluginHost;

public sealed class PluginProcessInvoker : IPluginProcessInvoker
{
    private readonly IProcessRunner _runner;

    public PluginProcessInvoker(IProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<PluginProcessResult> InvokeAsync(
        PluginProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProcessRunResult result = await _runner.RunAsync(
            new ProcessRunRequest(
                request.ExecutablePath,
                request.Arguments,
                request.StandardInputJson,
                request.WorkingDirectory,
                request.Timeout,
                request.StdoutMaxBytes,
                request.StderrMaxBytes,
                request.EnvironmentVariables),
            cancellationToken);

        return new PluginProcessResult(
            result.ExitCode,
            result.Stdout,
            result.Stderr,
            result.TimedOut,
            result.OutputLimitExceeded,
            result.OutputLimitStream);
    }
}
