using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Notion;

public static class NotionPluginInvoker
{
    public static PluginInvokeResponse Invoke(PluginInvokeRequest request)
    {
        if (request.Command.Path.SequenceEqual(["notion", "validate-seed"], StringComparer.Ordinal))
        {
            return NotionValidateSeedCommandHandler.Handle(request.RequestId, request);
        }

        if (request.Command.Path.SequenceEqual(["notion", "validate-database-map"], StringComparer.Ordinal))
        {
            return NotionValidateDatabaseMapCommandHandler.Handle(request.RequestId, request);
        }

        return InvokeUnsupportedCommand(request.RequestId);
    }

    public static PluginInvokeResponse InvokeUnsupportedCommand(string requestId)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: false,
            ExitCode: 1,
            Diagnostics:
            [
                new PluginDiagnostic(
                    Code: "plugin.notion.unsupportedCommand",
                    Severity: "error",
                    Message: "Unsupported notion command path. Supported commands in this phase: notion validate-seed, notion validate-database-map.")
            ]);
}
