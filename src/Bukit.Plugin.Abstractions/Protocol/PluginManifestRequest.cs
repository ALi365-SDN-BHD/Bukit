namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginManifestRequest(
    string Type,
    string Protocol,
    string RequestId,
    PluginHostInfo Host);
