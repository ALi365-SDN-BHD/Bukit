using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Import;

public static class ImportPluginManifestProvider
{
    public static PluginHandshakeResponse CreateHandshakeResponse(string requestId, string platform = "")
        => new(
            Type: "handshakeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: true,
            Plugin: new PluginIdentity(
                Id: "import",
                Name: "Bukit Import Plugin",
                Version: "0.1.0",
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
                new PluginCommandSpec(
                    Name: "import",
                    Description: "Import content into a Bukit site.")
            ],
            RequiredPermissions: new PluginPermissionSet());
}
