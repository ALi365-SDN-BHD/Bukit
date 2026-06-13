using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Plugins.Protocol;
using Bukit.Engine.Plugins.Protocol;
using Bukit.Shared;

namespace Bukit.Engine.Plugins;

public interface IPluginSource
{
    IEnumerable<IBukitPlugin> GetPlugins();
}

public sealed class BuiltInPluginSource : IPluginSource
{
    public IEnumerable<IBukitPlugin> GetPlugins()
    {
        yield return new BuiltIn.DataFilesPlugin();
        yield return new BuiltIn.PagesIndexPlugin();
        yield return new BuiltIn.TaxonomyPlugin();
        yield return new BuiltIn.PaginationPlugin();
        yield return new BuiltIn.ArchivePlugin();
        yield return new BuiltIn.RelatedContentPlugin();
        yield return new BuiltIn.AliasPlugin();
        yield return new BuiltIn.MenuPlugin();
        yield return new BuiltIn.ImageProcessingPlugin();
    }
}

public static class PluginRegistry
{
    private const string CacheKey = "__plugin_registry_cache";
    private sealed class PluginCacheEntry
    {
        public required IReadOnlyList<(IBukitPlugin Plugin, string Source)> Plugins { get; init; }
    }

    private static int _cacheBuildCount;

    public static IEnumerable<(IBukitPlugin Plugin, string Source)> GetAllPlugins(BuildContext context)
    {
        if (TryGetCached(context, out var cached))
        {
            foreach (var item in cached.Plugins)
            {
                yield return item;
            }

            yield break;
        }

        lock (context.Data)
        {
            if (!TryGetCached(context, out cached))
            {
                cached = new PluginCacheEntry
                {
                    Plugins = BuildPlugins(context)
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

    private static IReadOnlyList<(IBukitPlugin Plugin, string Source)> BuildPlugins(BuildContext context)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(IBukitPlugin Plugin, string Source)>();

        var sources = new (IPluginSource Source, string Name)[]
        {
            (new BuiltInPluginSource(), "built-in"),
            (new ExternalProtocolPluginSource(context), "external-protocol")
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

    private static bool TryGetCached(BuildContext context, out PluginCacheEntry cached)
    {
        if (context.Data.TryGetValue(CacheKey, out var value) && value is PluginCacheEntry entry)
        {
            cached = entry;
            return true;
        }

        cached = null!;
        return false;
    }
}
