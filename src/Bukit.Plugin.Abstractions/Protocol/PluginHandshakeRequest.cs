namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginHandshakeRequest(
    string Type,
    string Protocol,
    string RequestId,
    PluginHostInfo Host);
