using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

public static class RouteInventoryValidator
{
    public static async Task<IReadOnlyList<(ContentItem Item, RouteInfo Route)>> BuildContentRoutesAsync(
        AppConfig config,
        string rootDir,
        bool isCi,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var provider = ContentProviderFactory.Create(config, rootDir, isCi, logger);
        var loadResult = await provider.LoadAsync(cancellationToken);
        var items = loadResult.Items;
        if (!config.Build.Draft)
        {
            items = items.Where(i =>
                !(i.Meta.TryGetValue("draft", out var d) && d is true or "true" or "True")).ToList();
        }

        var siteLanguages = config.Site.Languages;
        if (siteLanguages is null or { Count: 0 })
        {
            var siteLanguage = config.Site.Language;
            items = I18nOutputMerger.FilterItemsByLanguage(items, siteLanguage, siteLanguage);
        }
        else
        {
            var defaultLang = I18nOutputMerger.GetDefaultLanguage(config.Site, siteLanguages);
            items = I18nOutputMerger.FilterItemsByLanguage(items, defaultLang, defaultLang);
        }

        var contentItems = items.Where(i => !MetaHelpers.IsDataItem(i)).ToList();
        var collectionRules = BuildCollectionRules(config.Site);
        return contentItems
            .Select(i => (Item: i, Route: RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
            .ToList();
    }

    public static void ValidateContentRoutes(IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed, string scope = "content")
    {
        ValidateEntries(routed.Select(x => RouteInventoryEntry.ForContent(x.Item, x.Route, scope)).ToList());
    }

    public static void ValidateFinalRoutes(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> derived,
        IReadOnlyList<RouteInfo>? specialRoutes = null)
    {
        var entries = new List<RouteInventoryEntry>(routed.Count + derived.Count + (specialRoutes?.Count ?? 0));
        entries.AddRange(routed.Select(x => RouteInventoryEntry.ForContent(x.Item, x.Route, "content")));
        entries.AddRange(derived.Select(x => RouteInventoryEntry.ForContent(x.Item, x.Route, "derived")));
        if (specialRoutes is not null)
        {
            entries.AddRange(specialRoutes.Select(RouteInventoryEntry.ForRoute));
        }

        ValidateEntries(entries);
    }

    internal static IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule>? BuildCollectionRules(SiteConfig site)
    {
        if (site.Collections is null || site.Collections.Count == 0)
        {
            return null;
        }

        var rules = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, collection) in site.Collections)
        {
            rules[key] = new RouteGenerator.CollectionRouteRule(collection.Permalink, collection.Template);
        }

        return rules;
    }

    private static void ValidateEntries(IReadOnlyList<RouteInventoryEntry> entries)
    {
        ThrowIfDuplicate(
            entries,
            e => NormalizeUrlForComparison(e.Route.Url),
            "url");
        ThrowIfDuplicate(
            entries,
            e => RoutePathBuilder.NormalizeOutputPath(e.Route.OutputPath),
            "outputPath");
    }

    private static void ThrowIfDuplicate(
        IReadOnlyList<RouteInventoryEntry> entries,
        Func<RouteInventoryEntry, string> keySelector,
        string kind)
    {
        var duplicate = entries
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);
        if (duplicate is null)
        {
            return;
        }

        var lines = duplicate
            .Select(e => $"{e.Scope}: {e.Describe()}")
            .ToArray();
        throw new ConfigException($"Route conflict on {kind}: {duplicate.Key}. Conflicting routes: {string.Join("; ", lines)}");
    }

    private static string NormalizeUrlForComparison(string url)
    {
        var normalized = RoutePathBuilder.NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized == "/" ? normalized : normalized.TrimEnd('/');
    }

    private sealed record RouteInventoryEntry(
        string Scope,
        string? Id,
        string? Title,
        string? Slug,
        RouteInfo Route)
    {
        internal static RouteInventoryEntry ForContent(ContentItem item, RouteInfo route, string scope)
            => new(scope, item.Id, item.Title, item.Slug, route);

        internal static RouteInventoryEntry ForRoute(RouteInfo route)
            => new("special", null, null, null, route);

        internal string Describe()
        {
            var identity = string.IsNullOrWhiteSpace(Id) ? "route" : $"id={Id}";
            var title = string.IsNullOrWhiteSpace(Title) ? string.Empty : $", title={Title}";
            var slug = string.IsNullOrWhiteSpace(Slug) ? string.Empty : $", slug={Slug}";
            return $"{identity}{title}{slug}, url={Route.Url}, outputPath={Route.OutputPath}";
        }
    }
}
