using Bukit.Notion.Mapping;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Notion;

public static class NotionValidateDatabaseMapCommandHandler
{
    public static PluginInvokeResponse Handle(string requestId, PluginInvokeRequest request)
    {
        NotionValidateDatabaseMapMapperResult mapped = NotionOptionsMapper.MapValidateDatabaseMapOptions(request);
        if (!mapped.Success || mapped.Options is null)
        {
            return CreateResponse(requestId, success: false, exitCode: 2, mapped.Diagnostics);
        }

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(
            mapped.Options.ProjectRoot,
            mapped.Options.DatabaseMapPath);

        return new PluginInvokeResponse(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: result.Success,
            ExitCode: result.ExitCode,
            Diagnostics: result.Diagnostics.Select(ToPluginDiagnostic).ToArray(),
            Artifacts: result.Artifacts.Select(artifact => ToPluginArtifact(mapped.Options.ProjectRoot, artifact)).ToArray());
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

    private static PluginDiagnostic ToPluginDiagnostic(NotionDatabaseMapDiagnostic diagnostic)
        => new(diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Path);

    private static PluginArtifact ToPluginArtifact(string projectRoot, NotionDatabaseMapArtifact artifact)
        => new(artifact.Type, NotionPluginPathFormatter.ToProjectRelativePath(projectRoot, artifact.Path), artifact.Description);
}
