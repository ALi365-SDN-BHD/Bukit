namespace Bukit.Plugin.Abstractions.Manifest;

public sealed record PluginOptionSpec(
    string Name,
    string Type,
    string Description,
    bool Required = false,
    string? ValueName = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? ConflictWith = null)
{
    public IReadOnlyList<string> AllowedValues { get; init; } = AllowedValues ?? [];
}
