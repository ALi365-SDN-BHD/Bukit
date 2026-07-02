using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Import;

public static class ImportPluginInvoker
{
    public static PluginInvokeResponse InvokeNotImplemented(string requestId)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: false,
            ExitCode: 1,
            Diagnostics:
            [
                new PluginDiagnostic(
                    Code: "plugin.import.notImplemented",
                    Severity: "info",
                    Message: "Import plugin skeleton is present. Business logic migration is intentionally not implemented in this phase.")
            ]);
}
