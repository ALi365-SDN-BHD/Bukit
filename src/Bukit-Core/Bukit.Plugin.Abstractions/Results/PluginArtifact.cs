namespace Bukit.Plugin.Abstractions.Results;

public sealed record PluginArtifact(
    string Type,
    string Path,
    string? Description = null);
