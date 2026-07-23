using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine.Plugins;

internal interface IPluginSource
{
    IEnumerable<IBukitPlugin> GetPlugins();
}

internal sealed class BuiltInPluginSource : IPluginSource
{
    private readonly AppConfig _config;

    internal BuiltInPluginSource(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public IEnumerable<IBukitPlugin> GetPlugins()
    {
        yield return new BuiltIn.AnalyticsPlugin(_config);
        yield return new BuiltIn.DataFilesPlugin(_config);
        yield return new BuiltIn.PagesIndexPlugin(_config);
        yield return new BuiltIn.TaxonomyPlugin(_config);
        yield return new BuiltIn.PaginationPlugin(_config);
        yield return new BuiltIn.ArchivePlugin(_config);
        yield return new BuiltIn.RelatedContentPlugin(_config);
        yield return new BuiltIn.AliasPlugin(_config);
        yield return new BuiltIn.MenuPlugin(_config);
        yield return new BuiltIn.ImageProcessingPlugin(_config);
    }
}

public static class PluginRegistry
{
    private const string CacheKey = "__plugin_registry_cache";
    private static readonly AppConfig CompatibilityConfig = new()
    {
        Site = new SiteConfig
        {
            Name = "plugin-compatibility",
            Title = "Plugin Compatibility"
        },
        Content = new ContentConfig()
    };

    private sealed class PluginCacheEntry
    {
        public required AppConfig Config { get; init; }
        public required IReadOnlyList<(IBukitPlugin Plugin, string Source)> Plugins { get; init; }
    }

    private static int _cacheBuildCount;

    public static IEnumerable<(IBukitPlugin Plugin, string Source)> GetAllPlugins(BuildContext context)
        => GetAllPlugins(context, CompatibilityConfig);

    internal static AppConfig CompatibilityConfiguration => CompatibilityConfig;

    internal static IEnumerable<(IBukitPlugin Plugin, string Source)> GetAllPlugins(
        BuildContext context,
        AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(config);

        if (TryGetCached(context, config, out var cached))
        {
            foreach (var item in cached.Plugins)
            {
                yield return item;
            }

            yield break;
        }

        lock (context.Data)
        {
            if (!TryGetCached(context, config, out cached))
            {
                cached = new PluginCacheEntry
                {
                    Config = config,
                    Plugins = BuildPlugins(config)
                };
                context.Data[CacheKey] = cached;
                _cacheBuildCount++;
            }
        }

        foreach (var item in cached.Plugins)
        {
            yield return item;
        }
    }

    private static IReadOnlyList<(IBukitPlugin Plugin, string Source)> BuildPlugins(AppConfig config)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(IBukitPlugin Plugin, string Source)>();

        var sources = new (IPluginSource Source, string Name)[]
        {
            (new BuiltInPluginSource(config), "built-in")
        };

        foreach (var (source, name) in sources)
        {
            foreach (var plugin in source.GetPlugins())
            {
                if (plugin is not null)
                {
                    var key = $"{plugin.Name}@{plugin.Version}";
                    if (seen.Add(key))
                    {
                        result.Add((plugin, name));
                    }
                }
            }
        }

        return result;
    }

    internal static int CacheBuildCountForTests => _cacheBuildCount;

    internal static void ResetCacheForTests()
    {
        _cacheBuildCount = 0;
    }

    private static bool TryGetCached(
        BuildContext context,
        AppConfig config,
        out PluginCacheEntry cached)
    {
        if (context.Data.TryGetValue(CacheKey, out var value) &&
            value is PluginCacheEntry entry &&
            ReferenceEquals(entry.Config, config))
        {
            cached = entry;
            return true;
        }

        cached = null!;
        return false;
    }
}
