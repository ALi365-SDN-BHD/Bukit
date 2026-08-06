using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Abstractions.Manifest;

public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Protocol,
    string Kind,
    string Distribution,
    IReadOnlyDictionary<string, PluginPlatformEntry>? Platforms = null,
    IReadOnlyList<PluginCommandSpec>? Commands = null,
    PluginPermissionSet? RequiredPermissions = null)
{
    public int ManifestVersion { get; init; } = 1;
    public IReadOnlyDictionary<string, PluginPlatformEntry> Platforms { get; init; } = Platforms ?? new Dictionary<string, PluginPlatformEntry>(StringComparer.Ordinal);
    public IReadOnlyList<PluginCommandSpec> Commands { get; init; } = Commands ?? [];
    public PluginPermissionSet RequiredPermissions { get; init; } = RequiredPermissions ?? new PluginPermissionSet();
}
