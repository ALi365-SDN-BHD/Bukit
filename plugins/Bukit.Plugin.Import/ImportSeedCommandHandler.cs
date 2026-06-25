using Bukit.Importing.Seed;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Import;

public static class ImportSeedCommandHandler
{
    public static PluginInvokeResponse Handle(
        string requestId,
        PluginInvokeRequest request,
        IImportSeedService service)
    {
        ImportOptionsMapperResult mapped = ImportOptionsMapper.MapSeedOptions(request);
        if (!mapped.Success || mapped.Options is null)
        {
            return CreateResponse(requestId, success: false, exitCode: 2, mapped.Diagnostics);
        }

        try
        {
            ImportSeedResult result = service.Import(mapped.Options);
            return new PluginInvokeResponse(
                Type: "invokeResponse",
                Protocol: PluginProtocolConstants.ProtocolVersion,
                RequestId: requestId,
                Success: result.Success,
                ExitCode: result.ExitCode,
                Diagnostics: result.Diagnostics.Select(ToPluginDiagnostic).ToArray(),
                Artifacts: result.Artifacts.Select(ToPluginArtifact).ToArray());
        }
        catch (Exception ex)
        {
            return CreateResponse(
                requestId,
                success: false,
                exitCode: 1,
                [
                    new PluginDiagnostic(
                        Code: "import.seedImportFailed",
                        Severity: "error",
                        Message: ex.Message)
                ]);
        }
    }

    private static PluginInvokeResponse CreateResponse(
        string requestId,
        bool success,
        int exitCode,
        IReadOnlyList<PluginDiagnostic> diagnostics)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: success,
            ExitCode: exitCode,
            Diagnostics: diagnostics);

    private static PluginDiagnostic ToPluginDiagnostic(ImportSeedDiagnostic diagnostic)
        => new(diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Path);

    private static PluginArtifact ToPluginArtifact(ImportSeedArtifact artifact)
        => new(artifact.Type, artifact.Path, artifact.Description);
}
