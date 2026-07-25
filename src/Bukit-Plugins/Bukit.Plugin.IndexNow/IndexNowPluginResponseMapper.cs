using Bukit.IndexNow;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.IndexNow;

public static class IndexNowPluginResponseMapper
{
    public static PluginInvokeResponse FromResult(
        PluginInvokeRequest request,
        IndexNowSubmissionResult result,
        bool dryRun)
        => new(
            "invokeResponse",
            PluginProtocolConstants.ProtocolVersion,
            request.RequestId,
            result.Success,
            result.Success ? 0 : 1,
            Messages:
            [
                new PluginMessage(
                    "info",
                    dryRun
                        ? $"indexnow dry-run: deployed={result.DeployedCount}"
                        : $"indexnow submit: deployed={result.DeployedCount} notified={result.NotifiedCount} pending={result.PendingCount}")
            ],
            Diagnostics: result.Diagnostics
                .Select(item => new PluginDiagnostic(item.Code, item.Severity, item.Message, item.Path))
                .ToArray());

    public static PluginInvokeResponse FromOptionsException(
        PluginInvokeRequest request,
        IndexNowPluginOptionsException exception)
        => new(
            "invokeResponse",
            PluginProtocolConstants.ProtocolVersion,
            request.RequestId,
            false,
            exception.ExitCode,
            Diagnostics: [new PluginDiagnostic(exception.Code, "error", exception.Message)]);

    public static PluginInvokeResponse FromException(PluginInvokeRequest request)
        => new(
            "invokeResponse",
            PluginProtocolConstants.ProtocolVersion,
            request.RequestId,
            false,
            1,
            Diagnostics:
            [
                new PluginDiagnostic(
                    "plugin.indexnow.failed",
                    "error",
                    "IndexNow submission failed; inspect local state and retry.")
            ]);
}
