using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Importing.Seed;

namespace Bukit.Plugin.Import;

public static class ImportPluginInvoker
{
    public static PluginInvokeResponse Invoke(PluginInvokeRequest request)
    {
        if (request.Command.Path.SequenceEqual(["import", "seed"], StringComparer.Ordinal))
        {
            return ImportSeedCommandHandler.Handle(request.RequestId, request, new ImportSeedService());
        }

        if (request.Command.Path.SequenceEqual(["import", "html-demo"], StringComparer.Ordinal))
        {
            return ImportHtmlDemoCommandHandler.Handle(request.RequestId, request);
        }

        return InvokeNotImplemented(request.RequestId);
    }

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
