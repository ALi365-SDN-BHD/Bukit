namespace Bukit.Plugin.Abstractions.Config;

public sealed record PluginHostConfig(
    int Version,
    IReadOnlyDictionary<string, PluginConfigEntry>? Plugins = null)
{
    public IReadOnlyDictionary<string, PluginConfigEntry> Plugins { get; init; } = Plugins ?? new Dictionary<string, PluginConfigEntry>(StringComparer.Ordinal);
}
