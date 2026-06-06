using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

internal sealed record SeoIndexBuildResult(
    IReadOnlyDictionary<string, SeoIndexEntry> Entries,
    IReadOnlyDictionary<string, SeoModel> Models);

internal static class SeoIndexBuilder
{
    internal static SeoIndexBuildResult BuildDocuments(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)> routed,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> alternates,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)>? derivedRouted = null)
    {
        var entries = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase);

        if (!config.Site.Seo.Enabled)
        {
            return new SeoIndexBuildResult(entries, models);
        }

        foreach (var (document, route) in routed)
        {
            var alternateKey = SeoModelBuilder.BuildDocumentAlternateKey(document, route);
            var model = SeoModelBuilder.BuildForDocument(
                config,
                baseUrl,
                document,
                route,
                alternates.TryGetValue(alternateKey, out var alts) ? alts : null);
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            models[key] = model;
            entries[key] = new SeoIndexEntry(
                route,
                model.Canonical,
                model.Robots,
                IsIndexableContent(document, model.Robots),
                ResolveLastModified(document),
                document.Record.Identity.Id,
                ResolveExplicitCollection(document));
        }

        if (derivedRouted is not null)
        {
            foreach (var (item, route) in derivedRouted)
            {
                var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
                if (entries.ContainsKey(key))
                {
                    continue;
                }

                var alternateKey = SeoModelBuilder.BuildAlternateKey(item, route);
                var model = SeoModelBuilder.BuildForContent(
                    config,
                    baseUrl,
                    item,
                    route,
                    alternates.TryGetValue(alternateKey, out var alts) ? alts : null);
                models[key] = model;
                entries[key] = new SeoIndexEntry(
                    route,
                    model.Canonical,
                    model.Robots,
                    IsIndexableContent(item, model.Robots),
                    SitemapPolicy.ResolveLastModified(item),
                    item.Id,
                    ResolveExplicitCollection(item));
            }
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
                ResolveListLastModified(config, route, routed),
                SourceItemId: null,
                ContentType: "list");
        }

        return new SeoIndexBuildResult(entries, models);
    }

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
                IsIndexableContent(item, model.Robots),
                SitemapPolicy.ResolveLastModified(item),
                item.Id,
                ResolveExplicitCollection(item));
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
                ResolveListLastModified(config, route, routed),
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
            Summary = BuildListSummary(config, route, routed),
            Fields = routed is null ? null : BuildListFields(config, route, routed)
        };
    }

    internal static PageInfo BuildListPageInfo(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)>? routed = null)
    {
        return new PageInfo
        {
            Title = route.Url == "/" ? config.Site.Title : BuildListTitle(route.Url),
            Url = route.Url,
            Content = string.Empty,
            Summary = BuildListSummary(config, route, routed),
            Fields = routed is null ? null : BuildListFields(config, route, routed)
        };
    }

    private static string BuildListSummary(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)>? routed)
    {
        if (!string.IsNullOrWhiteSpace(config.Site.Description) && route.Url == "/")
        {
            return config.Site.Description!;
        }

        var siteTitle = string.IsNullOrWhiteSpace(config.Site.Title) ? config.Site.Name : config.Site.Title;
        var title = route.Url == "/" ? siteTitle : BuildListTitle(route.Url);
        int? count = routed is null ? null : ResolveListItems(config, route, routed).Count;

        if (route.Url == "/")
        {
            return count is > 0
                ? $"Browse {count} content items from {siteTitle}."
                : $"Browse the latest content from {siteTitle}.";
        }

        return count is > 0
            ? $"Browse {count} items in {title} from {siteTitle}."
            : $"Browse {title} from {siteTitle}.";
    }

    private static bool IsIndexableContent(ContentItem item, string? robots)
    {
        if (!SeoModelBuilder.IsIndexable(robots))
        {
            return false;
        }

        var record = CanonicalContentGraphBuilder.ToRecord(item);
        if (!string.Equals(record.Identity.Status, "published", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return record.Lifecycle.ExpiresAt is null || record.Lifecycle.ExpiresAt > DateTimeOffset.UtcNow;
    }

    private static bool IsIndexableContent(ContentDocument document, string? robots)
    {
        if (!SeoModelBuilder.IsIndexable(robots))
        {
            return false;
        }

        if (document.Publish.Draft || document.Publish.NoIndex)
        {
            return false;
        }

        var record = document.Record;
        if (!string.Equals(record.Identity.Status, "published", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return record.Lifecycle.ExpiresAt is null || record.Lifecycle.ExpiresAt > DateTimeOffset.UtcNow;
    }

    private static string BuildListSummary(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)>? routed)
    {
        if (!string.IsNullOrWhiteSpace(config.Site.Description) && route.Url == "/")
        {
            return config.Site.Description!;
        }

        var siteTitle = string.IsNullOrWhiteSpace(config.Site.Title) ? config.Site.Name : config.Site.Title;
        var title = route.Url == "/" ? siteTitle : BuildListTitle(route.Url);
        int? count = routed is null ? null : ResolveListItems(config, route, routed).Count;

        if (route.Url == "/")
        {
            return count is > 0
                ? $"Browse {count} content items from {siteTitle}."
                : $"Browse the latest content from {siteTitle}.";
        }

        return count is > 0
            ? $"Browse {count} items in {title} from {siteTitle}."
            : $"Browse {title} from {siteTitle}.";
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
            var record = CanonicalContentGraphBuilder.ToRecord(item);
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = item.Title,
                ["url"] = route.Url,
                ["publish_date"] = item.PublishAt.DateTime
            };
            var summary = record.Presentation.Summary ?? ContentFieldReader.GetSummary(item.Fields);
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

    private static IReadOnlyDictionary<string, ContentField>? BuildListFields(
        AppConfig config,
        RouteInfo listRoute,
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)> routed)
    {
        var items = ResolveListItems(config, listRoute, routed);
        if (items.Count == 0)
        {
            return null;
        }

        var values = new List<object>(items.Count);
        foreach (var (document, route) in items)
        {
            var record = document.Record;
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = record.Presentation.Title,
                ["url"] = route.Url,
                ["publish_date"] = record.Lifecycle.PublishedAt.DateTime
            };
            if (!string.IsNullOrWhiteSpace(record.Presentation.Summary))
            {
                entry["summary"] = record.Presentation.Summary!;
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
                        .Where(x =>
                        {
                            var record = CanonicalContentGraphBuilder.ToRecord(x.Item);
                            return string.Equals(record.Classification.Collection, key, StringComparison.OrdinalIgnoreCase);
                        })
                        .ToList();
                }
            }

            return Array.Empty<(ContentItem Item, RouteInfo Route)>();
        }

        return routed
            .Where(x => x.Route.Url.StartsWith(listRoute.Url, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<(ContentDocument Document, RouteInfo Route)> ResolveListItems(
        AppConfig config,
        RouteInfo listRoute,
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)> routed)
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
                        .Where(x => string.Equals(x.Document.Record.Classification.Collection, key, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            return Array.Empty<(ContentDocument Document, RouteInfo Route)>();
        }

        return routed
            .Where(x => x.Route.Url.StartsWith(listRoute.Url, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static DateTimeOffset ResolveListLastModified(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)> routed)
    {
        var items = ResolveListItems(config, route, routed);
        return items.Count == 0
            ? DateTimeOffset.UnixEpoch
            : items.Max(x => ResolveLastModified(x.Document));
    }

    private static DateTimeOffset ResolveLastModified(ContentDocument document)
        => document.Record.Lifecycle.UpdatedAt
           ?? document.Record.Lifecycle.PublishedAt;

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

    private static DateTimeOffset ResolveListLastModified(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed)
    {
        var items = ResolveListItems(config, route, routed);
        return items.Count == 0
            ? DateTimeOffset.UnixEpoch
            : items.Max(x => SitemapPolicy.ResolveLastModified(x.Item));
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

    private static string? ResolveExplicitCollection(ContentItem item)
    {
        var collection = ContentFieldReader.GetCollection(item.Fields);
        return string.IsNullOrWhiteSpace(collection) ? null : collection;
    }

    private static string? ResolveExplicitCollection(ContentDocument document)
        => string.IsNullOrWhiteSpace(document.Record.Classification.Collection) ? null : document.Record.Classification.Collection;
}
