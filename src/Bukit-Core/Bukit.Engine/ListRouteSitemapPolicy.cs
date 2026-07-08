using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine;

internal static class ListRouteSitemapPolicy
{
    internal static bool IsExcluded(AppConfig config, ListRouteGraph? graph, SeoIndexEntry entry)
    {
        if (graph is null || graph.Routes.Count == 0)
        {
            return false;
        }

        var outputPath = BuildPathUtils.NormalizeRelPath(entry.Route.OutputPath);
        var route = graph.Routes.FirstOrDefault(candidate =>
            string.Equals(BuildPathUtils.NormalizeRelPath(candidate.OutputPath), outputPath, StringComparison.OrdinalIgnoreCase));
        return route is not null && IsExcluded(config, route);
    }

    internal static HashSet<string> BuildExcludedOutputPaths(AppConfig config, ListRouteGraph? graph)
    {
        if (graph is null || graph.Routes.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return graph.Routes
            .Where(route => IsExcluded(config, route))
            .Select(route => BuildPathUtils.NormalizeRelPath(route.OutputPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsExcluded(AppConfig config, ListRoutePlan route)
    {
        if (string.IsNullOrWhiteSpace(route.Collection))
        {
            return false;
        }

        return config.Site.Collections is { Count: > 0 } collections &&
               collections.TryGetValue(route.Collection, out var collection) &&
               !collection.Output.Sitemap;
    }
}
