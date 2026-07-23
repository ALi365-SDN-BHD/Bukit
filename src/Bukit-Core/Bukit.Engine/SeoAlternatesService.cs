using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class SeoAlternatesService
{
    internal static IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>> BuildSeoAlternates(
        AppConfig config,
        IReadOnlyList<ContentDocument> documents,
        IReadOnlyList<string> languages,
        string defaultLanguage,
        string rootBaseUrl,
        ThemeTemplateResolver? templateResolver = null,
        string? rootDir = null,
        string? layoutsDir = null)
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
                .ToList();
            var variantContentDocuments = variantDocuments
                .Where(i => !ContentFieldReader.IsDataItem(i))
                .ToList();
            var variantRouted = variantDocuments
                .Where(i => !ContentFieldReader.IsDataItem(i))
                .Select(i => new RoutedContentDocument(i, RouteGenerator.Generate(i, config.Site.OutputPathEncoding, config.Site.Permalinks, collectionRules)))
                .ToList();

            foreach (var routed in variantRouted)
            {
                AddAlternate(grouped, SeoModelBuilder.BuildAlternateKey(routed.Document, routed.Route), language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, routed.Route.Url));
            }

            var listRouteGraph = ListRouteGraphBuilder.Build(
                variantRouted,
                config.Site.Collections,
                config.Site.OutputPathEncoding,
                templateResolver);
            listRouteGraph = AddTaxonomyRoutesForAlternates(
                config,
                baseUrl,
                rootDir,
                layoutsDir,
                templateResolver,
                variantDocuments,
                variantContentDocuments,
                variantRouted,
                listRouteGraph);
            foreach (var route in listRouteGraph.Routes)
            {
                AddAlternate(grouped, SeoModelBuilder.BuildListAlternateKey(route.ToRouteInfo()), language, SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, route.CanonicalUrl));
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
        ListRouteGraph listRouteGraph,
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

        return existing;
    }

    private static ListRouteGraph AddTaxonomyRoutesForAlternates(
        AppConfig config,
        string baseUrl,
        string? rootDir,
        string? layoutsDir,
        ThemeTemplateResolver? templateResolver,
        IReadOnlyList<ContentDocument> variantDocuments,
        IReadOnlyList<ContentDocument> variantContentDocuments,
        IReadOnlyList<RoutedContentDocument> variantRouted,
        ListRouteGraph listRouteGraph)
    {
        if (string.Equals(TaxonomyPlugin.NormalizeOutputMode(config.Taxonomy.OutputMode), "data", StringComparison.OrdinalIgnoreCase))
        {
            return listRouteGraph;
        }

        var effectiveRootDir = string.IsNullOrWhiteSpace(rootDir)
            ? Directory.GetCurrentDirectory()
            : rootDir;
        var effectiveLayoutsDir = string.IsNullOrWhiteSpace(layoutsDir)
            ? effectiveRootDir
            : layoutsDir;
        var pluginContext = new BuildContext
        {
            Config = config,
            RootDir = effectiveRootDir,
            OutputDir = Path.Combine(Path.GetTempPath(), "bukit-seo-alternates"),
            BaseUrl = baseUrl,
            LayoutsDir = effectiveLayoutsDir,
            RoutedDocuments = variantRouted,
            StaticHtmlRoutes = Array.Empty<RouteInfo>(),
            ContentGraph = CanonicalContentGraphBuilder.BuildFromDocuments(variantContentDocuments),
            BodyStore = NullContentBodyStore.Instance,
            TemplateResolver = templateResolver is null
                ? _ => throw new ConfigException(
                    "No taxonomy template resolver is available while building SEO alternates.",
                    DiagnosticCode.ConfigRequiredFieldMissing)
                : kind => templateResolver.ResolveKindTemplate(kind),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        pluginContext.Data[ListRouteGraphBuilder.BuildContextDataKey] = listRouteGraph;

        var dataDocuments = variantDocuments.Where(ContentFieldReader.IsDataItem).ToList();
        TaxonomyTermsInjector.InjectFromDataDocuments(pluginContext, dataDocuments);
        var derived = new TaxonomyPlugin(config).DerivePages(pluginContext);
        return ListRouteGraphBuilder.AddDerivedTaxonomyRoutes(listRouteGraph, derived);
    }

    internal static IReadOnlyList<string>? GetSeoStringList(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || !fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        if (field.Value is string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (field.Value is IEnumerable<object> values)
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

    internal static IReadOnlyList<string>? GetSeoStringList(IReadOnlyDictionary<string, object>? valuesByKey, string key)
    {
        if (valuesByKey is null || !valuesByKey.TryGetValue(key, out var value) || value is null)
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
