using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;

namespace Bukit.Engine;

internal sealed record SeoIndexBuildResult(
    IReadOnlyDictionary<string, SeoIndexEntry> Entries,
    IReadOnlyDictionary<string, SeoModel> Models);

internal static class SeoIndexBuilder
{
    internal static SeoIndexBuildResult Build(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> alternates)
    {
        var entries = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase);

        if (!config.Site.Seo.Enabled)
        {
            return new SeoIndexBuildResult(entries, models);
        }

        foreach (var (item, route) in routed)
        {
            var alternateKey = SeoModelBuilder.BuildAlternateKey(item, route);
            var model = SeoModelBuilder.BuildForContent(
                config,
                baseUrl,
                item,
                route,
                alternates.TryGetValue(alternateKey, out var alts) ? alts : null);
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            models[key] = model;
            entries[key] = new SeoIndexEntry(
                route,
                model.Canonical,
                model.Robots,
                SeoModelBuilder.IsIndexable(model.Robots),
                SitemapPolicy.ResolveLastModified(item),
                item.Id,
                MetaHelpers.GetString(item.Meta, "collection") ?? MetaHelpers.GetString(item.Meta, "type"));
        }

        foreach (var route in listRoutes)
        {
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            var page = BuildListPageInfo(config, route, routed);
            var alternateKey = SeoModelBuilder.BuildListAlternateKey(route);
            var model = SeoModelBuilder.BuildForList(
                config,
                baseUrl,
                page,
                alternates.TryGetValue(alternateKey, out var alts) ? alts : null);
            models[key] = model;
            entries[key] = new SeoIndexEntry(
                route,
                model.Canonical,
                model.Robots,
                SeoModelBuilder.IsIndexable(model.Robots),
                DateTimeOffset.UtcNow,
                SourceItemId: null,
                ContentType: "list");
        }

        return new SeoIndexBuildResult(entries, models);
    }

    internal static PageInfo BuildListPageInfo(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)>? routed = null)
    {
        return new PageInfo
        {
            Title = route.Url == "/" ? config.Site.Title : BuildListTitle(route.Url),
            Url = route.Url,
            Content = string.Empty,
            Summary = config.Site.Description,
            Fields = routed is null ? null : BuildListFields(config, route, routed)
        };
    }

    private static IReadOnlyDictionary<string, ContentField>? BuildListFields(
        AppConfig config,
        RouteInfo listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        var items = ResolveListItems(config, listRoute, routed);
        if (items.Count == 0)
        {
            return null;
        }

        var values = new List<object>(items.Count);
        foreach (var (item, route) in items)
        {
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = item.Title,
                ["url"] = route.Url,
                ["publish_date"] = item.PublishAt.DateTime
            };
            var summary = MetaHelpers.GetString(item.Meta, "summary");
            if (!string.IsNullOrWhiteSpace(summary))
            {
                entry["summary"] = summary!;
            }

            values.Add(entry);
        }

        return new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = new("list", values)
        };
    }

    private static IReadOnlyList<(ContentItem Item, RouteInfo Route)> ResolveListItems(
        AppConfig config,
        RouteInfo listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        if (listRoute.Url == "/")
        {
            return routed;
        }

        if (config.Site.Collections is { Count: > 0 } collections)
        {
            foreach (var (key, collection) in collections)
            {
                if (string.IsNullOrWhiteSpace(collection.ListRoute))
                {
                    continue;
                }

                if (string.Equals(NormalizeListUrl(collection.ListRoute), listRoute.Url, StringComparison.OrdinalIgnoreCase))
                {
                    return routed
                        .Where(x => string.Equals(MetaHelpers.GetString(x.Item.Meta, "collection") ?? MetaHelpers.GetString(x.Item.Meta, "type"), key, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            return Array.Empty<(ContentItem Item, RouteInfo Route)>();
        }

        return routed
            .Where(x => x.Route.Url.StartsWith(listRoute.Url, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string BuildListTitle(string url)
    {
        var lastSegment = (url ?? string.Empty)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastSegment))
        {
            return "Index";
        }

        return char.ToUpperInvariant(lastSegment[0]) + lastSegment[1..].Replace('-', ' ');
    }

    private static string NormalizeListUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (!trimmed.EndsWith('/'))
        {
            trimmed += "/";
        }

        return trimmed;
    }
}
