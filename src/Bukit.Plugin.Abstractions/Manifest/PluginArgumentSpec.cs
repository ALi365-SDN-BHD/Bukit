namespace Bukit.Plugin.Abstractions.Manifest;

public sealed record PluginArgumentSpec(
    string Name,
    string Description,
    bool Required = false);
