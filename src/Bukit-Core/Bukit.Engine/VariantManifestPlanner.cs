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
        var manifest = BuildManifest.Load(manifestPath);
        if (!PublicOutputLifecycle.SameRoot(manifest, context.OutputDir)) manifest = new BuildManifest();
        if (!incrementalEnabled) manifest.Entries.Clear();
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
            ComputeTemplateDirectoryPart("child", context.LayoutsDir, context, templateHashCache),
            ComputeTemplateDirectoryPart("parent", context.ParentLayoutsDir, context, templateHashCache),
            ComputeTemplateDirectoryPart("user", context.UserLayoutsDir, context, templateHashCache),
            ComputeThemeYamlPart(context.LayoutsDir, context),
            ComputeThemeYamlPart(context.ParentLayoutsDir, context),
            ComputeThemeYamlPart(context.UserLayoutsDir, context)
        };
        return HashUtil.Sha256Hex(string.Join('\n', parts));
    }

    private static string ComputeTemplateDirectoryPart(
        string label,
        string? directory,
        BuildVariantContext context,
        DirectoryHashCache cache)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return $"{label}:missing";
        }

        var identityDirectory = Path.GetFullPath(directory);
        if (!string.IsNullOrWhiteSpace(context.TemplateIdentityRoot) &&
            PathUtils.IsSameOrSubPathOf(identityDirectory, context.RootDir))
        {
            identityDirectory = Path.GetFullPath(Path.Combine(
                context.TemplateIdentityRoot,
                Path.GetRelativePath(context.RootDir, identityDirectory)));
        }
        return $"{label}:{identityDirectory}:{cache.GetOrAdd(directory)}";
    }

    private static string ComputeThemeYamlPart(string? layoutsDirectory, BuildVariantContext context)
    {
        if (string.IsNullOrWhiteSpace(layoutsDirectory))
        {
            return "theme-yaml:missing";
        }

        var parent = Directory.GetParent(layoutsDirectory)?.FullName ?? string.Empty;
        var themeYamlPath = Path.Combine(parent, "theme.yaml");
        var identityPath = themeYamlPath;
        if (!string.IsNullOrWhiteSpace(context.TemplateIdentityRoot) &&
            PathUtils.IsSameOrSubPathOf(themeYamlPath, context.RootDir))
        {
            identityPath = Path.GetFullPath(Path.Combine(
                context.TemplateIdentityRoot,
                Path.GetRelativePath(context.RootDir, themeYamlPath)));
        }
        if (!File.Exists(themeYamlPath))
        {
            return $"theme-yaml:{identityPath}:missing";
        }

        return $"theme-yaml:{identityPath}:{HashUtil.Sha256Hex(File.ReadAllBytes(themeYamlPath))}";
    }
}
