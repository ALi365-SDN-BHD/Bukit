using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Notion;

public static class NotionPluginInvoker
{
    public static PluginInvokeResponse Invoke(PluginInvokeRequest request)
        => InvokeUnsupportedCommand(request.RequestId);

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
                    Message: "Notion command handlers are not implemented in PR-Notion-001 skeleton.")
            ]);
}
