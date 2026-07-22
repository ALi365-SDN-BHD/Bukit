using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record ManifestSetupResult(
    BuildManifest Manifest,
    string TemplateHash,
    string ManifestPath,
    ConcurrentDictionary<string, BuildManifestEntry>? ManifestEntries,
    bool IncrementalEnabled);

internal static class VariantManifestPlanner
{
    internal static ManifestSetupResult Create(
        BuildVariantContext context,
        ConfigOverrides overrides,
        DirectoryHashCache templateHashCache)
    {
        var incrementalEnabled = overrides.Incremental ?? true;
        var cacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(context.RootDir, ".cache")
            : Path.GetFullPath(overrides.CacheDir!);
        var suffix = string.IsNullOrWhiteSpace(context.ManifestSuffix)
            ? null
            : BuildPathUtils.SanitizeFileSegment(context.ManifestSuffix);
        var manifestPath = suffix is null
            ? Path.Combine(cacheDir, "build-manifest.json")
            : Path.Combine(cacheDir, $"build-manifest.{suffix}.json");
        var templateHash = incrementalEnabled
            ? ComputeCompositeTemplateHash(context, templateHashCache)
            : string.Empty;
        var manifest = incrementalEnabled
            ? BuildManifest.Load(manifestPath)
            : new BuildManifest();
        manifest.TemplateHash = templateHash;
        var manifestEntries = incrementalEnabled
            ? new ConcurrentDictionary<string, BuildManifestEntry>(manifest.Entries, StringComparer.Ordinal)
            : null;

        return new ManifestSetupResult(
            manifest,
            templateHash,
            manifestPath,
            manifestEntries,
            incrementalEnabled);
    }

    private static string ComputeCompositeTemplateHash(
        BuildVariantContext context,
        DirectoryHashCache templateHashCache)
    {
        var parts = new List<string>
        {
            "scriban-renderer-v1",
            ComputeTemplateDirectoryPart("child", context.LayoutsDir, templateHashCache),
            ComputeTemplateDirectoryPart("parent", context.ParentLayoutsDir, templateHashCache),
            ComputeTemplateDirectoryPart("user", context.UserLayoutsDir, templateHashCache),
            ComputeThemeYamlPart(context.LayoutsDir),
            ComputeThemeYamlPart(context.ParentLayoutsDir),
            ComputeThemeYamlPart(context.UserLayoutsDir)
        };
        return HashUtil.Sha256Hex(string.Join('\n', parts));
    }

    private static string ComputeTemplateDirectoryPart(
        string label,
        string? directory,
        DirectoryHashCache cache)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return $"{label}:missing";
        }

        return $"{label}:{Path.GetFullPath(directory)}:{cache.GetOrAdd(directory)}";
    }

    private static string ComputeThemeYamlPart(string? layoutsDirectory)
    {
        if (string.IsNullOrWhiteSpace(layoutsDirectory))
        {
            return "theme-yaml:missing";
        }

        var parent = Directory.GetParent(layoutsDirectory)?.FullName ?? string.Empty;
        var themeYamlPath = Path.Combine(parent, "theme.yaml");
        if (!File.Exists(themeYamlPath))
        {
            return $"theme-yaml:{themeYamlPath}:missing";
        }

        return $"theme-yaml:{themeYamlPath}:{HashUtil.Sha256Hex(File.ReadAllBytes(themeYamlPath))}";
    }
}
