using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.RouteMetadata;

namespace Bukit.Engine;

internal sealed record SeoIndexBuildResult(
    IReadOnlyDictionary<string, SeoIndexEntry> Entries,
    IReadOnlyDictionary<string, SeoModel> Models);

internal static class SeoIndexBuilder
{
    internal static SeoIndexBuildResult Build(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<RouteInfo> listRoutes,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> alternates,
        ListRouteGraph? listRouteGraph = null,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata = null)
    {
        var entries = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase);

        if (!config.Site.Seo.Enabled)
        {
            return new SeoIndexBuildResult(entries, models);
        }

        foreach (var routedDocument in routed)
        {
            var document = routedDocument.Document;
            var route = routedDocument.Route;
            var alternateKey = SeoModelBuilder.BuildAlternateKey(document, route);
            var metadata = RouteMetadataApplicator.Find(route.Url, routeMetadata);
            var model = SeoModelBuilder.BuildForContent(
                config,
                baseUrl,
                document,
                route,
                alternates.TryGetValue(alternateKey, out var alts) ? alts : null,
                metadata?.SeoTitle ?? metadata?.Title,
                metadata?.SeoDescription ?? metadata?.Summary);
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            models[key] = model;
            entries[key] = new SeoIndexEntry(
                route,
                model.Canonical,
                model.Robots,
                IsIndexableContent(document, model.Robots),
                SitemapPolicy.ResolveLastModified(document),
                document.Id,
                document.Record.Identity.ContentType,
                IsDerived: IsDerived(document),
                Collection: ResolveExplicitCollection(document));
        }

        if (listRouteGraph is not null && listRouteGraph.Routes.Count > 0)
        {
            foreach (var route in listRouteGraph.Routes)
            {
                AddGraphRoute(config, baseUrl, routed, alternates, entries, models, route);
            }

            return new SeoIndexBuildResult(entries, models);
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
                ContentType: "list",
                IsDerived: true,
                Collection: ResolveLegacyListCollection(config, route));
        }

        return new SeoIndexBuildResult(entries, models);
    }

    private static void AddGraphRoute(
        AppConfig config,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> alternates,
        Dictionary<string, SeoIndexEntry> entries,
        Dictionary<string, SeoModel> models,
        ListRoutePlan route)
    {
        var routeInfo = route.ToRouteInfo();
        var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
        var page = BuildListPageInfo(config, route, routed);
        if (!string.IsNullOrWhiteSpace(route.SeoTitle) || !string.IsNullOrWhiteSpace(route.SeoDescription))
        {
            page = page with
            {
                Title = string.IsNullOrWhiteSpace(route.SeoTitle) ? page.Title : BuildPagedSeoTitle(route.SeoTitle!, route.PageNumber),
                Summary = string.IsNullOrWhiteSpace(route.SeoDescription)
                    ? page.Summary
                    : BuildPagedSeoDescription(route.SeoDescription!, route)
            };
        }
        var alternateKey = SeoModelBuilder.BuildListAlternateKey(routeInfo);
        var model = SeoModelBuilder.BuildForList(
            config,
            baseUrl,
            page,
            route,
            alternates.TryGetValue(alternateKey, out var alts) ? alts : null);
        models[key] = model;
        entries[key] = new SeoIndexEntry(
            routeInfo,
            model.Canonical,
            model.Robots,
            SeoModelBuilder.IsIndexable(model.Robots),
            ResolveListLastModified(config, route, routed),
            SourceItemId: null,
            ContentType: route.TaxonomyContext is null ? "list" : "taxonomy",
            IsDerived: true,
            Collection: string.IsNullOrWhiteSpace(route.Collection) ? null : route.Collection);
    }

    private static string BuildPagedSeoTitle(string title, int? page)
        => page > 1 ? $"{title.Trim()} - Page {page}" : title.Trim();

    private static string BuildPagedSeoDescription(string description, ListRoutePlan route)
    {
        var text = description.Trim();
        if (route.PageNumber is not > 1)
        {
            return text;
        }

        var pagination = ListPageMetadataBuilder.BuildPagination(route);
        if (pagination is null)
        {
            return $"{text} Browse page {route.PageNumber}.";
        }

        var pageSize = pagination.PageSize.GetValueOrDefault();
        var start = ((pagination.Page - 1) * pageSize) + 1;
        var end = Math.Min(pagination.TotalItems, pagination.Page * pageSize);
        return $"{text} Browse page {pagination.Page}, showing items {start}-{end} of {pagination.TotalItems}.";
    }

    internal static PageInfo BuildListPageInfo(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<RoutedContentDocument>? routed = null)
    {
        return new PageInfo
        {
            Title = ListPageMetadataBuilder.BuildTitle(config.Site, route),
            Url = route.Url,
            Content = string.Empty,
            Summary = BuildListSummary(config, route, routed),
            Fields = routed is null ? null : BuildListFields(config, route, routed)
        };
    }

    internal static PageInfo BuildListPageInfo(
        AppConfig config,
        ListRoutePlan route,
        IReadOnlyList<RoutedContentDocument>? routed = null)
    {
        var matched = FindByOutputPath(route.OutputPath, routed);
        var pagination = ListPageMetadataBuilder.BuildPagination(route);
        var summary = !string.IsNullOrWhiteSpace(route.Summary)
            ? route.Summary.Trim()
            : matched is null
                ? ListPageMetadataBuilder.BuildSummary(config.Site, route, pagination)
                : ContentFieldReader.GetSummary(matched.Document) ?? BuildListSummary(config, route);

        return new PageInfo
        {
            Title = !string.IsNullOrWhiteSpace(route.Title)
                ? ListPageMetadataBuilder.BuildTitle(config.Site, route, pagination)
                : matched?.Document.Title ?? ListPageMetadataBuilder.BuildTitle(config.Site, route, pagination),
            Url = route.Url,
            Content = string.Empty,
            Summary = summary,
            Fields = ListRouteRenderPlanBuilder.BuildPageFields(route)
        };
    }

    private static string BuildListSummary(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<RoutedContentDocument>? routed)
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

    private static string BuildListSummary(AppConfig config, ListRoutePlan route, ListPaginationModel? pagination = null)
    {
        if (!string.IsNullOrWhiteSpace(config.Site.Description) && route.Url == "/")
        {
            return config.Site.Description!;
        }

        return ListPageMetadataBuilder.BuildSummary(config.Site, route, pagination);
    }

    private static bool IsIndexableContent(ContentDocument document, string? robots)
    {
        if (!SeoModelBuilder.IsIndexable(robots))
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

    private static IReadOnlyDictionary<string, ContentField>? BuildListFields(
        AppConfig config,
        RouteInfo listRoute,
        IReadOnlyList<RoutedContentDocument> routed)
    {
        var items = ResolveListItems(config, listRoute, routed);
        if (items.Count == 0)
        {
            return null;
        }

        var values = new List<object>(items.Count);
        foreach (var routedDocument in items)
        {
            var document = routedDocument.Document;
            var route = routedDocument.Route;
            var record = document.Record;
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = document.Title,
                ["url"] = route.Url,
                ["publish_date"] = document.PublishAt.DateTime
            };
            var summary = record.Presentation.Summary ?? ContentFieldReader.GetSummary(document);
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

    private static IReadOnlyList<RoutedContentDocument> ResolveListItems(
        AppConfig config,
        RouteInfo listRoute,
        IReadOnlyList<RoutedContentDocument> routed)
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

            return Array.Empty<RoutedContentDocument>();
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

    private static DateTimeOffset ResolveListLastModified(
        AppConfig config,
        RouteInfo route,
        IReadOnlyList<RoutedContentDocument> routed)
    {
        var items = ResolveListItems(config, route, routed);
        return items.Count == 0
            ? DateTimeOffset.UnixEpoch
            : items.Max(x => SitemapPolicy.ResolveLastModified(x.Document));
    }

    private static DateTimeOffset ResolveListLastModified(
        AppConfig config,
        ListRoutePlan route,
        IReadOnlyList<RoutedContentDocument> routed)
    {
        var matched = FindByOutputPath(route.OutputPath, routed);
        if (matched is not null)
        {
            return SitemapPolicy.ResolveLastModified(matched.Document);
        }

        var itemDates = route.Items
            .Select(item => item.PublishDate)
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        if (itemDates.Length > 0)
        {
            return itemDates.Max();
        }

        return ResolveListLastModified(config, route.ToRouteInfo(), routed);
    }

    private static RoutedContentDocument? FindByOutputPath(
        string outputPath,
        IReadOnlyList<RoutedContentDocument>? routed)
    {
        if (routed is null || routed.Count == 0)
        {
            return null;
        }

        var key = BuildPathUtils.NormalizeRelPath(outputPath);
        return routed.FirstOrDefault(x => string.Equals(
            BuildPathUtils.NormalizeRelPath(x.Route.OutputPath),
            key,
            StringComparison.OrdinalIgnoreCase));
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

    private static string? ResolveExplicitCollection(ContentDocument document)
    {
        var collection = document.Record.Classification.Collection;
        if (string.IsNullOrWhiteSpace(collection))
        {
            collection = ContentFieldReader.GetCollection(document);
        }

        return string.IsNullOrWhiteSpace(collection) ? null : collection;
    }

    private static string? ResolveLegacyListCollection(AppConfig config, RouteInfo route)
    {
        if (route.Url == "/" || config.Site.Collections is not { Count: > 0 } collections)
        {
            return null;
        }

        foreach (var (collectionKey, collection) in collections)
        {
            if (!string.IsNullOrWhiteSpace(collection.ListRoute) &&
                string.Equals(NormalizeListUrl(collection.ListRoute), route.Url, StringComparison.OrdinalIgnoreCase))
            {
                return collectionKey;
            }
        }

        return null;
    }

    private static bool IsDerived(ContentDocument document)
        => string.Equals(document.Record.Identity.ContentType, "derived", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(ContentFieldReader.GetContentType(document), "derived", StringComparison.OrdinalIgnoreCase);
}
