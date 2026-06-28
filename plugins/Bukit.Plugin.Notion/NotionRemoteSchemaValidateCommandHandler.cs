using Bukit.Notion.RemoteSchema;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Notion;

public static class NotionRemoteSchemaValidateCommandHandler
{
    public static PluginInvokeResponse Handle(string requestId, PluginInvokeRequest request)
        => Handle(requestId, request, new NotionRemoteSchemaValidationService());

    public static PluginInvokeResponse Handle(
        string requestId,
        PluginInvokeRequest request,
        INotionRemoteSchemaValidationService service)
    {
        NotionRemoteSchemaValidateMapperResult mapped = NotionOptionsMapper.MapRemoteSchemaValidateOptions(request);
        if (!mapped.Success || mapped.Options is null)
        {
            return new PluginInvokeResponse(
                Type: "invokeResponse",
                Protocol: PluginProtocolConstants.ProtocolVersion,
                RequestId: requestId,
                Success: false,
                ExitCode: 2,
                Diagnostics: mapped.Diagnostics);
        }

        NotionRemoteSchemaValidationResult result = service.Validate(mapped.Options);
        return new PluginInvokeResponse(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: result.Success,
            ExitCode: result.ExitCode,
            Artifacts: result.Artifacts.Select(artifact => new PluginArtifact(
                artifact.Type,
                NotionPluginPathFormatter.ToProjectRelativePath(mapped.Options.ProjectRoot, artifact.Path),
                artifact.Description)).ToArray(),
            Diagnostics: result.Diagnostics.Select(diagnostic => new PluginDiagnostic(
                diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Message,
                diagnostic.Path)).ToArray());
    }
}
