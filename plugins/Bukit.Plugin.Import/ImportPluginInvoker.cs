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
                    Code: "plugin.import.unsupportedCommand",
                    Severity: "error",
                    Message: "Unsupported import command path. Supported commands: import seed, import html-demo.")
            ]);
}
