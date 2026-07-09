using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.WechatSyncing;

namespace Bukit.Plugin.WechatSync;

public static class WechatSyncPluginManifestProvider
{
    public static PluginHandshakeResponse CreateHandshakeResponse(string requestId, string platform = "")
        => new(
            Type: "handshakeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: true,
            Plugin: new PluginIdentity(
                Id: WechatSyncWorkflow.PluginId,
                Name: "Bukit WeChat Sync Plugin",
                Version: WechatSyncWorkflow.Version,
                Platform: platform,
                Capabilities: ["cli-command"]));

    public static PluginManifestResponse CreateManifestResponse(string requestId)
        => new(
            Type: "manifestResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: true,
            Capabilities: ["cli-command"],
            Commands: WechatSyncPluginCommandSpecs.CreateCommands(),
            RequiredPermissions: WechatSyncPluginCommandSpecs.CreateRequiredPermissions());
}
