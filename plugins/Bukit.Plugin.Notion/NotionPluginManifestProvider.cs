using Bukit.Notion;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Notion;

public static class NotionPluginManifestProvider
{
    public static PluginHandshakeResponse CreateHandshakeResponse(string requestId, string platform = "")
        => new(
            Type: "handshakeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: true,
            Plugin: new PluginIdentity(
                Id: NotionPluginConstants.Id,
                Name: NotionPluginConstants.Name,
                Version: NotionPluginConstants.Version,
                Platform: platform,
                Capabilities: ["cli-command"]));

    public static PluginManifestResponse CreateManifestResponse(string requestId)
        => new(
            Type: "manifestResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: true,
            Capabilities: ["cli-command"],
            Commands:
            [
                NotionCommandSpecFactory.CreateNotionCommand()
            ],
            RequiredPermissions: CreateRequiredPermissions());

    private static PluginPermissionSet CreateRequiredPermissions()
        => new(
            FileSystem: new PluginFileSystemPermission(
                Read: ["."],
                Write: [NotionPluginConstants.ReportOutputDirectory, NotionPluginConstants.TemporaryOutputDirectory]),
            Network: true,
            Environment: new PluginEnvironmentPermission(Read: [NotionPluginConstants.TokenEnvironmentVariable]));
}
