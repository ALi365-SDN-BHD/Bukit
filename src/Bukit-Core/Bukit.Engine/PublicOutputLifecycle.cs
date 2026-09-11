using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class PublicOutputLifecycle
{
    internal static string ManifestPath(string rootDir, ConfigOverrides overrides)
        => Path.Combine(string.IsNullOrWhiteSpace(overrides.CacheDir) ? Path.Combine(rootDir, ".cache") : Path.GetFullPath(overrides.CacheDir), "build-manifest.json");

    internal static IReadOnlyList<AssetOutputItem> ProjectionPlan(AppConfig config, IEnumerable<RouteInfo> routes, bool root = false)
    {
        var items = new List<AssetOutputItem>();
        void Add(string path, string kind) => items.Add(new AssetOutputItem(kind, BuildPathUtils.NormalizeRelPath(path), AssetOutputCategory.Projection, Operation: AssetOutputOperation.Generate));
        foreach (var route in routes)
        {
            Add(DefaultContentProjectionWriter.GetContentProjectionRelativePath(route, ".json"), "json " + route.Url);
            Add(DefaultContentProjectionWriter.GetContentProjectionRelativePath(route, ".md"), "markdown " + route.Url);
        }
        Add("agent-manifest.json", "agent-manifest");
        if (!root || SiteModeResolver.ResolveSearchMode(config.Site) == "merged") Add("search.json", "search");
        else if (SiteModeResolver.ResolveSearchMode(config.Site) == "index") Add("search.index.json", "search");
        if (!root && config.Site.Search.Ui?.Trim().ToLowerInvariant() is not (null or "false" or "off")) Add("bukit-search.html", "search-ui");
        if (!string.IsNullOrWhiteSpace(config.Site.Url))
        {
            if (!root || (config.Site.SitemapMode ?? "split").Trim().ToLowerInvariant() is "merged" or "index") Add("sitemap.xml", "sitemap");
            if (config.Site.Seo.RobotsTxt.Enabled) Add("robots.txt", "robots");
            if (!root || SiteModeResolver.ResolveFeedMode(config.Site) == "merged")
            {
                AddFeedOutputs(config, root, Add);
            }
        }
        if (config.Site.Seo.Geo.Enabled)
        {
            if (config.Site.Seo.Geo.LlmsTxt) Add("llms.txt", "llms");
            if (config.Site.Seo.Geo.LlmsFullTxt) Add("llms-full.txt", "llms-full");
        }
        return items;
    }

    private static void AddFeedOutputs(AppConfig config, bool root, Action<string, string> add)
    {
        string[] formats = root
            ? [.. config.Site.Feed.Formats.Select(x => x.Trim().ToLowerInvariant()).Where(x => x is "rss" or "atom" or "json")]
            : [.. config.Site.Feed.Formats];
        if (formats.Contains("rss", StringComparer.OrdinalIgnoreCase) || root && formats.Length == 0) add("rss.xml", "feed");
        if (formats.Contains("atom", StringComparer.OrdinalIgnoreCase)) add(config.Site.Feed.Path + "/atom.xml", "atom");
        if (formats.Contains("json", StringComparer.OrdinalIgnoreCase)) add(config.Site.Feed.Path + "/feed.json", "jsonfeed");
        if (!root && config.Site.Collections is not null)
        {
            foreach (var (name, collection) in config.Site.Collections.Where(x => x.Value.Output.Rss))
            {
                var path = (collection.Output.FeedPath ?? config.Site.Feed.Path + "/" + name).Trim().Replace('\\', '/').Trim('/');
                if (formats.Contains("rss", StringComparer.OrdinalIgnoreCase)) add(path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? path : path + "/rss.xml", "collection-feed " + name);
                if (formats.Contains("atom", StringComparer.OrdinalIgnoreCase)) add(path + "/atom.xml", "collection-atom " + name);
                if (formats.Contains("json", StringComparer.OrdinalIgnoreCase)) add(path + "/feed.json", "collection-jsonfeed " + name);
            }
        }
    }

    internal static void PrepareProjectionWrites(string outputDir, BuildManifest previous, IReadOnlyList<AssetOutputItem> items)
    {
        var current = items.Select(x => x.Destination).ToHashSet(OutputDestinationIdentityComparer.ForOutputRoot(outputDir));
        current.UnionWith(previous.PluginOutputs.Keys);
        DeleteStale(outputDir, previous, current);
        // Generated robots must reflect new policy; a static override is copied and excluded from this plan.
        if (items.Any(x => x.Category == AssetOutputCategory.Projection && x.Destination == "robots.txt") && previous.OwnedOutputs.Contains("robots.txt"))
            DeleteOwnedFile(outputDir, "robots.txt");
    }

    internal static void DeleteStale(string outputDir, BuildManifest previous, IReadOnlySet<string> current)
    {
        if (!SameRoot(previous, outputDir)) return;
        foreach (var path in previous.OwnedOutputs.Where(path => !current.Contains(path)).OrderByDescending(x => x.Length))
        {
            // A removed file may already have become a directory for this build's outputs.
            if (Directory.Exists(FileWriter.GetSafeFullPath(outputDir, path)) &&
                current.Any(destination => destination.StartsWith(path.TrimEnd('/') + "/", PlatformPathHelper.PathComparison))) continue;
            DeleteOwnedFile(outputDir, path);
        }
    }

    internal static void DeleteOwnedFile(string outputDir, string path)
    {
        var full = FileWriter.GetSafeFullPath(outputDir, path);
        if (Directory.Exists(full)) throw new IOException($"Cannot remove owned output '{path}': a directory occupies the file path.");
        try { File.Delete(full); }
        catch (DirectoryNotFoundException) { return; }
        var directory = Path.GetDirectoryName(full);
        while (directory is not null && !string.Equals(Path.GetFullPath(directory), Path.GetFullPath(outputDir), PlatformPathHelper.PathComparison)
               && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    internal static bool SameRoot(BuildManifest manifest, string outputDir)
        => !string.IsNullOrEmpty(manifest.OutputRoot) && string.Equals(manifest.OutputRoot, Path.GetFullPath(outputDir), PlatformPathHelper.PathComparison);

    internal static void Record(BuildManifest manifest, string outputDir, IEnumerable<AssetOutputItem> items)
    {
        manifest.Version = 3;
        manifest.OutputRoot = Path.GetFullPath(outputDir);
        manifest.OwnedOutputs = items.Select(x => x.Destination).ToHashSet(StringComparer.Ordinal);
    }

    internal static BuildManifest CollectAndClean(string rootDir, ConfigOverrides overrides, string outputDir, IReadOnlyList<BuildVariantResult> variants, IReadOnlyList<AssetOutputItem>? rootOutputs = null)
    {
        var items = variants.SelectMany(variant => variant.PlannedOutputs.Select(item => item with
        {
            Destination = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(outputDir, Path.Combine(variant.OutputDir, item.Destination)))
        })).Concat(rootOutputs ?? []).ToArray();
        AssetOutputPlan.Validate(items, OutputDestinationIdentityComparer.ForOutputRoot(outputDir));
        var previous = BuildManifest.Load(ManifestPath(rootDir, overrides));
        DeleteStale(outputDir, previous, items.Select(x => x.Destination).ToHashSet(OutputDestinationIdentityComparer.ForOutputRoot(outputDir)));
        var manifest = variants.Count == 1 && variants[0].OutputDir == outputDir ? variants[0].PendingManifest!.Manifest : new BuildManifest();
        Record(manifest, outputDir, items);
        return manifest;
    }
}
