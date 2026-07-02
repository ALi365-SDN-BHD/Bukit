namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginRequestEnvelope(
    string Type,
    string Protocol,
    string RequestId,
    PluginHostInfo Host);
