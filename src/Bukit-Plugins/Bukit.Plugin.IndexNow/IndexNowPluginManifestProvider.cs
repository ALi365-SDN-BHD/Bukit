using Bukit.IndexNow;
using Bukit.Plugin.Abstractions.Protocol;

namespace Bukit.Plugin.IndexNow;

public static class IndexNowPluginManifestProvider
{
    public static PluginHandshakeResponse CreateHandshakeResponse(string requestId, string platform = "")
        => new(
            "handshakeResponse",
            PluginProtocolConstants.ProtocolVersion,
            requestId,
            true,
            new PluginIdentity(
                IndexNowSubmissionWorkflow.PluginId,
                "Bukit IndexNow Plugin",
                IndexNowSubmissionWorkflow.Version,
                platform,
                ["cli-command"]));

    public static PluginManifestResponse CreateManifestResponse(string requestId)
        => new(
            "manifestResponse",
            PluginProtocolConstants.ProtocolVersion,
            requestId,
            true,
            ["cli-command"],
            IndexNowPluginCommandSpecs.CreateCommands(),
            IndexNowPluginCommandSpecs.CreateRequiredPermissions());
}
