using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

public static class SeoAlternatesService
{
    internal static IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> BuildSeoAlternates(
        AppConfig config,
        IReadOnlyList<ContentDocument> documents,
        IReadOnlyList<string> languages,
        string defaultLanguage,
        string rootBaseUrl,
        ThemeTemplateResolver? templateResolver = null)
    {
        if (string.IsNullOrWhiteSpace(config.Site.Url) || languages.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal);
        }

        var grouped = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var collectionRules = BuildCollectionRules(config.Site);
        foreach (var language in languages)
        {
            var baseUrl = I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, language);
            var variantDocuments = I18nOutputMerger
                .FilterDocumentsByLanguage(documents, language, defaultLanguage)
                .Where(i => !ContentFieldReader.IsDataItem(i))
                .ToList();
            var variantRouted = variantDocuments
                .Select(i => new RoutedContentDocument(i, RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
                .ToList();

            foreach (var routed in variantRouted)
            {
                AddAlternate(grouped, SeoModelBuilder.BuildAlternateKey(routed.Document, routed.Route), language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, routed.Route.Url));
            }

            foreach (var route in BuildListRoutesCore(config.Site.Collections, config.Site.OutputPathEncoding, templateResolver))
            {
                AddAlternate(grouped, SeoModelBuilder.BuildListAlternateKey(route), language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, route.Url));
            }

            foreach (var url in BuildTaxonomyRouteUrls(config, variantRouted))
            {
                AddAlternate(grouped, $"route:{url}", language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, url));
            }

            foreach (var url in BuildPaginationRouteUrls(config, variantRouted))
            {
                AddAlternate(grouped, $"route:{url}", language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, url));
            }
        }

        var result = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal);
        foreach (var (key, byLanguage) in grouped)
        {
            if (byLanguage.Count < 2)
            {
                continue;
            }

            var list = new List<SeoAlternateModel>(byLanguage.Count + 1);
            if (byLanguage.TryGetValue(defaultLanguage, out var defaultHref))
            {
                list.Add(new SeoAlternateModel("x-default", defaultHref));
            }

            foreach (var language in languages)
            {
                if (byLanguage.TryGetValue(language, out var href))
                {
                    list.Add(new SeoAlternateModel(language, href));
                }
            }

            result[key] = list;
        }

        return result;
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> AddVariantRouteAlternates(
        AppConfig config,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> existing,
        IEnumerable<RouteInfo> routes,
        string? rootBaseUrl,
        string? defaultLanguage)
    {
        var languages = I18nOutputMerger.GetLanguages(config.Site);
        if (string.IsNullOrWhiteSpace(config.Site.Url) ||
            string.IsNullOrWhiteSpace(rootBaseUrl) ||
            string.IsNullOrWhiteSpace(defaultLanguage) ||
            languages.Count < 2)
        {
            return existing;
        }

        Dictionary<string, IReadOnlyList<SeoAlternateModel>>? result = null;
        foreach (var route in routes)
        {
            var key = SeoModelBuilder.BuildListAlternateKey(route);
            if (existing.ContainsKey(key))
            {
                continue;
            }

            result ??= new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(existing, StringComparer.Ordinal);
            var alternates = new List<SeoAlternateModel>(languages.Count + 1);
            alternates.Add(new SeoAlternateModel(
                "x-default",
                SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, defaultLanguage), route.Url)));

            foreach (var language in languages)
            {
                alternates.Add(new SeoAlternateModel(
                    language,
                    SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, language), route.Url)));
            }

            result[key] = alternates;
        }

        return result ?? existing;
    }

    internal static IReadOnlyList<RouteInfo> GetListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections, ThemeTemplateResolver? templateResolver = null)
        => BuildListRoutesCore(collections, "none", templateResolver);

    internal static IReadOnlyList<RouteInfo> BuildListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections, ThemeTemplateResolver? templateResolver = null)
        => BuildListRoutesCore(collections, "none", templateResolver);

    internal static IReadOnlyList<RouteInfo> BuildListRoutesCore(IReadOnlyDictionary<string, CollectionConfig>? collections, string outputPathEncoding, ThemeTemplateResolver? templateResolver = null)
    {
        var routes = new List<RouteInfo>
        {
            new("/", "index.html", templateResolver?.ResolveHomeTemplate() ?? ThemeTemplateResolver.DefaultHomeTemplate)
        };

        if (collections is null || collections.Count == 0)
        {
            return routes;
        }

        foreach (var (_, collection) in collections)
        {
            if (string.IsNullOrWhiteSpace(collection.ListRoute))
            {
                continue;
            }

            var url = RoutePathBuilder.NormalizeListRoute(collection.ListRoute);
            var template = ResolveListTemplate(collection.ListTemplate, templateResolver);
            routes.Add(new RouteInfo(url, RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding), template));

            if (collection.FilteredLists is { Count: > 0 })
            {
                foreach (var filter in collection.FilteredLists)
                {
                    var filterUrl = RoutePathBuilder.NormalizeListRoute(filter.ListRoute);
                    var filterTemplate = string.IsNullOrWhiteSpace(filter.ListTemplate) ? template : filter.ListTemplate.Trim();
                    routes.Add(new RouteInfo(filterUrl, RoutePathBuilder.BuildOutputPathFromUrl(filterUrl, outputPathEncoding), filterTemplate));
                }
            }
        }

        return routes;
    }

    private static string ResolveListTemplate(string? explicitTemplate, ThemeTemplateResolver? templateResolver)
    {
        if (!string.IsNullOrWhiteSpace(explicitTemplate))
        {
            return explicitTemplate.Trim();
        }

        if (templateResolver is null)
        {
            throw new ConfigException("No list template was configured. Add site.collections.*.listTemplate or a matching theme.yaml templates entry.");
        }

        return templateResolver.ResolveKindTemplate("list");
    }

    internal static IReadOnlyList<string> BuildTaxonomyRouteUrls(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> routed)
    {
        if (string.Equals((config.Taxonomy.OutputMode ?? string.Empty).Trim(), "data", StringComparison.OrdinalIgnoreCase) ||
            routed.Count == 0)
        {
            return Array.Empty<string>();
        }

        var pageSize = NormalizePageSize(config.Taxonomy.PageSize);
        var result = new List<string>();
        if (config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            foreach (var kindConfig in kinds)
            {
                var key = (kindConfig.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(kindConfig.Kind) ? key : kindConfig.Kind.Trim();
                AddTaxonomyKindRoutes(result, kind, BuildTaxonomyTermCounts(routed, key), pageSize, kindConfig.IndexEnabled ?? config.Taxonomy.IndexEnabled);
            }

            return result;
        }

        AddTaxonomyKindRoutes(result, "tags", BuildTaxonomyTermCounts(routed, "tags"), pageSize, config.Taxonomy.IndexEnabled);
        AddTaxonomyKindRoutes(result, "categories", BuildTaxonomyTermCounts(routed, "categories"), pageSize, config.Taxonomy.IndexEnabled);
        return result;
    }

    internal static IReadOnlyList<string> BuildPaginationRouteUrls(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> routed)
    {
        if (routed.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (config.Site.Collections is not { Count: > 0 })
        {
            return Array.Empty<string>();
        }

        var paginationCollection = config.Site.Collections.FirstOrDefault(x => x.Value.Pagination.Enabled);
        if (paginationCollection.Value is null || string.IsNullOrWhiteSpace(paginationCollection.Value.ListRoute))
        {
            return Array.Empty<string>();
        }

        var collectionKey = paginationCollection.Key;
        var listRoute = paginationCollection.Value.ListRoute;
        var pageSize = paginationCollection.Value.Pagination.PageSize;
        pageSize = NormalizePageSize(pageSize);
        var count = routed.Count(x => string.Equals(GetCollection(x.Document), collectionKey, StringComparison.OrdinalIgnoreCase));
        if (count <= pageSize)
        {
            return Array.Empty<string>();
        }

        var totalPages = (int)Math.Ceiling(count / (double)pageSize);
        var normalizedListRoute = RoutePathBuilder.NormalizeListRoute(listRoute);
        var result = new List<string>(totalPages - 1);
        for (var page = 2; page <= totalPages; page++)
        {
            result.Add($"{normalizedListRoute}page/{page}/");
        }

        return result;
    }

    internal static void AddTaxonomyKindRoutes(
        List<string> result,
        string kind,
        IReadOnlyDictionary<string, int> termCounts,
        int pageSize,
        bool indexEnabled)
    {
        if (string.IsNullOrWhiteSpace(kind) || termCounts.Count == 0)
        {
            return;
        }

        var normalizedKind = kind.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedKind))
        {
            return;
        }

        if (indexEnabled)
        {
            result.Add($"/{normalizedKind}/");
        }

        foreach (var (slug, count) in termCounts)
        {
            result.Add($"/{normalizedKind}/{slug}/");
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);
            for (var page = 2; page <= totalPages; page++)
            {
                result.Add($"/{normalizedKind}/{slug}/page/{page}/");
            }
        }
    }

    internal static IReadOnlyDictionary<string, int> BuildTaxonomyTermCounts(
        IReadOnlyList<RoutedContentDocument> routed,
        string key)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            var values = ContentFieldReader.GetTextList(routedDocument.Document.CustomFields, key);
            if (values is null)
            {
                continue;
            }

            foreach (var value in values)
            {
                var slug = SlugHelper.Slugify(value);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                result[slug] = result.TryGetValue(slug, out var count) ? count + 1 : 1;
            }
        }

        return result;
    }

    internal static IReadOnlyList<string>? GetSeoStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (value is IEnumerable<object> values)
        {
            var list = values
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
            return list.Count == 0 ? null : list;
        }

        return null;
    }

    internal static string GetCollection(ContentDocument document)
    {
        return ContentFieldReader.GetCollection(document);
    }

    internal static int NormalizePageSize(int pageSize) => pageSize <= 0 ? 10 : pageSize;

    private static IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule>? BuildCollectionRules(SiteConfig site)
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

    private static void AddAlternate(
        Dictionary<string, Dictionary<string, string>> grouped,
        string key,
        string language,
        string href)
    {
        if (!grouped.TryGetValue(key, out var byLanguage))
        {
            byLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            grouped[key] = byLanguage;
        }

        byLanguage[language] = href;
    }
}
