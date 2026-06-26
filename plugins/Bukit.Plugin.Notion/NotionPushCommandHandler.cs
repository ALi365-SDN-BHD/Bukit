using Bukit.Notion.Push;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Notion;

public static class NotionPushCommandHandler
{
    public static PluginInvokeResponse Handle(string requestId, PluginInvokeRequest request)
    {
        NotionPushMapperResult mapped = NotionOptionsMapper.MapPushOptions(request);
        if (!mapped.Success || mapped.Options is null)
        {
            return CreateResponse(requestId, success: false, exitCode: 2, mapped.Diagnostics);
        }

        NotionPushResult result = new NotionPushService().Push(mapped.Options.PushOptions);
        return new PluginInvokeResponse(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: result.Success,
            ExitCode: result.ExitCode,
            Diagnostics: result.Diagnostics.Select(ToPluginDiagnostic).ToArray(),
            Artifacts: result.Artifacts.Select(artifact => ToPluginArtifact(mapped.Options.PushOptions.ProjectRoot, artifact)).ToArray());
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

    private static PluginDiagnostic ToPluginDiagnostic(NotionPushDiagnostic diagnostic)
        => new(diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Path);

    private static PluginArtifact ToPluginArtifact(string projectRoot, NotionPushArtifact artifact)
        => new(artifact.Type, NotionPluginPathFormatter.ToProjectRelativePath(projectRoot, artifact.Path), artifact.Description);
}
