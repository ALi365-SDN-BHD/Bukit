using YamlDotNet.RepresentationModel;

namespace Bukit.PluginHost;

/// <summary>
/// Migrates plugin manifest YAML nodes between schema versions.
/// Currently only v1 is supported; this class is the extension point
/// for future schema changes (e.g. adding new required fields, renaming keys).
/// </summary>
internal static class PluginManifestMigrator
{
    /// <summary>
    /// Applies sequential migrations from <paramref name="fromVersion"/>
    /// to <paramref name="toVersion"/>.
    /// </summary>
    internal static YamlMappingNode Migrate(
        YamlMappingNode root, int fromVersion, int toVersion)
    {
        var current = root;
        var version = fromVersion;

        while (version < toVersion)
        {
            current = version switch
            {
                // v1 → v2: reserved for future migration
                // 1 => MigrateV1ToV2(current),
                _ => throw new InvalidOperationException(
                    $"No migration path from manifest version {version} to {version + 1}.")
            };
            version++;
        }

        return current;
    }
}
