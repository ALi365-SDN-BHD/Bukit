using Bukit.Config;

namespace Bukit.Engine.Plugins.Protocol;

internal interface IProtocolPluginInvoker
{
    Task<ProtocolPluginInvocationResult> InvokeAsync(
        ExternalPluginConfig plugin,
        string requestJson,
        string? arguments,
        CancellationToken cancellationToken);
}

internal sealed record ProtocolPluginInvocationResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    bool TimedOut,
    long ElapsedMs);
