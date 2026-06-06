using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

public static class RouteInventoryValidator
{
    /// <summary>
    /// The default kind used when resolving content item templates.
    /// Theme manifests must declare <c>accepts.kind: detail</c> for their template
    /// definitions to be considered by <see cref="ResolveRouteTemplate"/>.
    /// </summary>
    private const string DefaultDetailKind = "detail";

    public static async Task<IReadOnlyList<(ContentItem Item, RouteInfo Route)>> BuildContentRoutesAsync(
        AppConfig config,
        string rootDir,
        bool isCi,
        ILogger logger,
        ThemeTemplateResolver? templateResolver = null,
        CancellationToken cancellationToken = default)
    {
        var provider = ContentProviderFactory.Create(config, rootDir, isCi, logger);
        var loadResult = await provider.LoadAsync(cancellationToken);
        var items = loadResult.Items;
        if (!config.Build.Draft)
        {
            items = items.Where(i =>
                !(i.Meta.TryGetValue("draft", out var d) && ValueCoercion.IsTruthy(d))).ToList();
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
            .Select(x => (x.Item, Route: ResolveRouteTemplate(x.Item, x.Route, templateResolver)))
            .ToList();
    }

    public static void ValidateContentRoutes(IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed, string scope = "content")
    {
        ValidateEntries(routed.Select(x => RouteInventoryEntry.ForContent(x.Item, x.Route, scope)).ToList());
    }

    public static void ValidateContentDocumentRoutes(
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)> routed,
        string scope = "content")
    {
        ValidateEntries(routed.Select(x => RouteInventoryEntry.ForContentDocument(x.Document, x.Route, scope)).ToList());
    }

    public static void ValidateFinalRoutes(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> derived,
        IReadOnlyList<RouteInfo>? specialRoutes = null,
        IReadOnlyList<RouteInfo>? staticHtmlRoutes = null)
    {
        var entries = new List<RouteInventoryEntry>(routed.Count + derived.Count + (specialRoutes?.Count ?? 0) + (staticHtmlRoutes?.Count ?? 0));
        entries.AddRange(routed.Select(x => RouteInventoryEntry.ForContent(x.Item, x.Route, "content")));
        entries.AddRange(derived.Select(x => RouteInventoryEntry.ForContent(x.Item, x.Route, "derived")));
        if (specialRoutes is not null)
        {
            entries.AddRange(specialRoutes.Select(RouteInventoryEntry.ForRoute));
        }

        if (staticHtmlRoutes is not null)
        {
            entries.AddRange(staticHtmlRoutes.Select(RouteInventoryEntry.ForStaticHtmlRoute));
        }

        ValidateEntries(entries);
    }

    public static (RouteInfo Route, string Source) GenerateRouteWithSource(ContentItem item, SiteConfig site)
    {
        var collections = BuildCollectionRules(site);
        var result = RouteGenerator.GenerateWithSource(item, site.OutputPathEncoding, site.Permalinks, collections);
        return (result.Route, result.Source.ToString());
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
            rules[key] = new RouteGenerator.CollectionRouteRule(collection.Permalink, collection.Template ?? string.Empty);
        }

        return rules;
    }

    private static RouteInfo ResolveRouteTemplate(ContentItem item, RouteInfo route, ThemeTemplateResolver? templateResolver)
    {
        if (!string.IsNullOrWhiteSpace(route.Template))
        {
            return route;
        }

        if (templateResolver is null)
        {
            return route;
        }

        return route with { Template = templateResolver.ResolveContentTemplate(item, DefaultDetailKind) };
    }

    private static void ValidateEntries(IReadOnlyList<RouteInventoryEntry> entries)
    {
        foreach (var entry in entries)
        {
            RouteSecurityValidator.ValidateInternalUrl(entry.Route.Url, entry.Describe());
            RouteSecurityValidator.ValidateOutputPath(entry.Route.OutputPath, entry.Describe());
        }

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
        throw new ConfigException($"Route conflict on {kind}: {duplicate.Key}. Conflicting routes: {string.Join("; ", lines)}", DiagnosticCode.RouteConflict);
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

        internal static RouteInventoryEntry ForContentDocument(ContentDocument document, RouteInfo route, string scope)
            => new(
                scope,
                document.Record.Identity.Id,
                document.Record.Presentation.Title,
                document.Record.Identity.Slug,
                route);

        internal static RouteInventoryEntry ForRoute(RouteInfo route)
            => new("special", null, null, null, route);

        internal static RouteInventoryEntry ForStaticHtmlRoute(RouteInfo route)
            => new("static", null, null, null, route);

        internal string Describe()
        {
            var identity = string.IsNullOrWhiteSpace(Id) ? "route" : $"id={Id}";
            var title = string.IsNullOrWhiteSpace(Title) ? string.Empty : $", title={Title}";
            var slug = string.IsNullOrWhiteSpace(Slug) ? string.Empty : $", slug={Slug}";
            return $"{identity}{title}{slug}, url={Route.Url}, outputPath={Route.OutputPath}";
        }
    }
}
