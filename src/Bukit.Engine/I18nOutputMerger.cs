using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class I18nOutputMerger
{
    internal static IReadOnlyList<string> GetLanguages(SiteConfig site)
    {
        if (site.Languages is not { Count: > 0 } langs)
        {
            return Array.Empty<string>();
        }

        var cleaned = langs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        if (cleaned.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in cleaned)
        {
            if (seen.Add(l))
            {
                result.Add(l);
            }
        }

        return result;
    }

    internal static string GetDefaultLanguage(SiteConfig site, IReadOnlyList<string> languages)
    {
        if (languages.Count == 0)
        {
            return site.Language;
        }

        if (string.IsNullOrWhiteSpace(site.DefaultLanguage))
        {
            return languages[0];
        }

        var dl = site.DefaultLanguage.Trim();
        return languages.Contains(dl, StringComparer.OrdinalIgnoreCase) ? dl : languages[0];
    }

    internal static string CombineBaseUrlWithLanguage(string baseUrl, string language)
    {
        var b = BuildPathUtils.NormalizeBaseUrl(baseUrl);
        var l = language.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(l))
        {
            return b;
        }

        if (b == "/")
        {
            return "/" + l;
        }

        return b.TrimEnd('/') + "/" + l;
    }

    internal static IReadOnlyList<ContentItem> FilterItemsByLanguage(IReadOnlyList<ContentItem> items, string language, string defaultLanguage)
    {
        return items.Where(item =>
        {
            if (MetaHelpers.IsDataItem(item))
            {
                var locale = MetaHelpers.TryGetTextField(item.Fields, "locale");
                return string.IsNullOrWhiteSpace(locale) || string.Equals(locale, language, StringComparison.OrdinalIgnoreCase);
            }

            var itemLanguage = item.GetTextValue("language");
            if (!string.IsNullOrWhiteSpace(itemLanguage))
            {
                return string.Equals(itemLanguage, language, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    internal static IReadOnlyList<PublishProjectionResult> GenerateRootOutputs(AppConfig config, string outputDir, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results, ILogger logger, ISearchIndexBuilder searchIndexBuilder)
    {
        _ = searchIndexBuilder;
        var context = new PublishProjectionContext(
            config,
            outputDir,
            CanonicalContentGraph.Empty,
            Array.Empty<(ContentItem Item, RouteInfo Route)>(),
            Array.Empty<(ContentItem Item, RouteInfo Route)>(),
            new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase),
            BaseUrl: rootBaseUrl,
            Logger: logger,
            VariantResults: results);
        return PublishRepresentationRegistry.RootAggregateProjectionAdapters()
            .Select(projection => projection.Project(context))
            .ToArray();
    }

    internal static PublishProjectionResult ProjectRootAggregate(PublishProjectionContext context, PublishRepresentation representation)
    {
        var results = context.VariantResults ?? Array.Empty<BuildVariantResult>();
        GenerateRootAggregate(context.Config, context.OutputDir, context.BaseUrl, results, context.Logger ?? new ConsoleLogger(LogLevel.Error), representation);
        return new PublishProjectionResult(representation, BuildRootRepresentationOutputs(context.OutputDir, representation, results));
    }

    private static void GenerateRootAggregate(AppConfig config, string outputDir, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results, ILogger logger, PublishRepresentation representation)
    {
        var siteUrl = config.Site.Url;
        switch (representation.Kind)
        {
            case "sitemap" when !string.IsNullOrWhiteSpace(siteUrl):
                var sitemapMode = (config.Site.SitemapMode ?? "split").Trim().ToLowerInvariant();
                if (sitemapMode == "merged")
                {
                    GenerateMergedSitemap(config, outputDir, siteUrl, results, logger);
                }
                else if (sitemapMode == "index")
                {
                    var sitemaps = results.Select(r => SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, "/sitemap.xml")).ToList();
                    SitemapGenerator.GenerateIndex(outputDir, sitemaps);
                }

                break;
            case "feed" or "atom" or "jsonfeed" when !string.IsNullOrWhiteSpace(siteUrl):
                var rssMode = (config.Site.RssMode ?? "split").Trim().ToLowerInvariant();
                if (rssMode == "merged")
                {
                    GenerateMergedFeeds(config, outputDir, siteUrl, rootBaseUrl, results);
                }

                break;
            case "search":
                var searchMode = (config.Site.SearchMode ?? "split").Trim().ToLowerInvariant();
                if (searchMode == "merged")
                {
                    SearchIndexBuilder.GenerateMergedSearchIndex(outputDir, results, config.Site.SearchIncludeDerived);
                }
                else if (searchMode == "index")
                {
                    SearchIndexBuilder.GenerateSearchIndexIndex(outputDir, results);
                }

                break;
            case "llms":
                GenerateRootLlms(config, outputDir, rootBaseUrl, results, logger);
                break;
            case "llms-full":
                GenerateRootLlmsFull(config, outputDir, rootBaseUrl, results, logger);
                break;
            case "robots":
                GenerateRootRobots(config, outputDir, rootBaseUrl, results);
                break;
            case "agent-manifest":
                GenerateRootAgentManifest(outputDir, results);
                break;
        }
    }

    private static void GenerateMergedSitemap(AppConfig config, string outputDir, string siteUrl, IReadOnlyList<BuildVariantResult> results, ILogger logger)
    {
        var excludeCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool IsExcludedFile(string absoluteHtmlPath)
        {
            if (excludeCache.TryGetValue(absoluteHtmlPath, out var cached))
            {
                return cached;
            }

            var excluded = SitemapPolicy.ShouldExcludeFromSitemapFile(absoluteHtmlPath, logger);
            excludeCache[absoluteHtmlPath] = excluded;
            return excluded;
        }

        var entries = new List<SitemapGenerator.UrlEntry>();
        foreach (var r in results)
        {
            foreach (var (key, seo) in r.SeoIndex
                         .Where(x => x.Value.Indexable)
                         .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                if (IsExcludedFile(Path.Combine(r.OutputDir, seo.Route.OutputPath)))
                {
                    continue;
                }

                entries.Add(new SitemapGenerator.UrlEntry(
                    seo.Canonical,
                    seo.LastModified,
                    r.SeoModels.TryGetValue(key, out var model) ? BuildAlternates(model.Alternates) : null));
            }
        }

        SitemapGenerator.GenerateAbsoluteWithAlternates(outputDir, entries);

        static IReadOnlyList<SitemapGenerator.Alternate>? BuildAlternates(IReadOnlyList<Bukit.Rendering.SeoAlternateModel> alternates)
        {
            if (alternates.Count <= 1)
            {
                return null;
            }

            return alternates
                .OrderBy(x => string.Equals(x.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase) ? string.Empty : x.Hreflang, StringComparer.OrdinalIgnoreCase)
                .Select(x => new SitemapGenerator.Alternate(x.Hreflang, x.Href))
                .ToList();
        }
    }

    private static void GenerateMergedFeeds(AppConfig config, string outputDir, string siteUrl, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results)
    {
        var posts = new List<RssGenerator.Post>();
        var rssCollections = ResolveRssCollections(config.Site.Collections);
        foreach (var r in results)
        {
            var itemsByPath = SearchIndexBuilder.BuildItemMap(r.Routed);
            foreach (var (key, seo) in r.SeoIndex
                         .Where(x => x.Value.Indexable)
                         .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                if (!itemsByPath.TryGetValue(key, out var item) ||
                    !rssCollections.Contains(GetCollection(item)))
                {
                    continue;
                }

                var record = (r.ContentGraph ?? CanonicalContentGraph.Empty).Records
                    .FirstOrDefault(x => string.Equals(x.Identity.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                posts.Add(RssGenerator.ToPost(item, seo.Canonical, r.BodyStore, record));
            }
        }

        var formats = ParseFeedFormats(config.Site.Feed.Formats);
        var limit = config.Site.Feed.Limit > 0 ? config.Site.Feed.Limit : 20;
        foreach (var format in formats)
        {
            switch (format)
            {
                case "rss":
                    RssGenerator.GenerateMerged(outputDir, siteUrl, rootBaseUrl, config.Site.Title, posts, limit, config.Site.Description);
                    break;
                case "atom":
                    AtomFeedGenerator.Generate(outputDir, siteUrl, rootBaseUrl, config.Site.Title, posts, $"{config.Site.Feed.Path}/atom.xml", limit, config.Site.Description);
                    break;
                case "json":
                    JsonFeedGenerator.Generate(outputDir, siteUrl, rootBaseUrl, config.Site.Title, posts, $"{config.Site.Feed.Path}/feed.json", limit, config.Site.Description);
                    break;
            }
        }
    }

    private static IReadOnlySet<string> ParseFeedFormats(IReadOnlyList<string> formats)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var format in formats)
        {
            var normalized = format.Trim().ToLowerInvariant();
            if (normalized is "rss" or "atom" or "json")
            {
                set.Add(normalized);
            }
        }

        if (set.Count == 0)
        {
            set.Add("rss");
        }

        return set;
    }

    private static HashSet<string> ResolveRssCollections(IReadOnlyDictionary<string, CollectionConfig>? collections)
    {
        if (collections is null || collections.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, cfg) in collections)
        {
            if (cfg.Output.Rss)
            {
                set.Add(key);
            }
        }

        return set;
    }

    private static string GetCollection(ContentItem item)
    {
        return item.GetCollection();
    }

    private static void GenerateRootAgentManifest(string outputDir, IReadOnlyList<BuildVariantResult> results)
    {
        var entries = new List<DefaultContentProjectionWriter.AgentManifestEntry>();
        foreach (var result in results)
        {
            var recordsById = (result.ContentGraph ?? CanonicalContentGraph.Empty).Records
                .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            foreach (var (item, route) in result.Routed.Concat(result.DerivedRouted))
            {
                var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
                result.SeoIndex.TryGetValue(key, out var seoEntry);
                if (seoEntry?.Indexable == false)
                {
                    continue;
                }

                if (!recordsById.TryGetValue(item.Id, out var record))
                {
                    record = CanonicalContentGraphBuilder.ToRecord(item);
                }

                result.SeoModels.TryGetValue(key, out var model);
                var mergedRoute = CombineBaseUrl(result.BaseUrl, route.Url);
                entries.Add(new DefaultContentProjectionWriter.AgentManifestEntry(
                    record.Identity.Id,
                    record.Identity.CanonicalUrlKey,
                    mergedRoute,
                    record.Presentation.Language,
                    record.Trust.ReviewStatus,
                    record.Provenance.Source,
                    record.Entities.Select(x => x.Name).ToArray(),
                    PrefixRepresentationUrls(
                        result.BaseUrl,
                        DefaultContentProjectionWriter.BuildAgentManifestRepresentationEntries(record, mergedRoute, seoEntry, model)),
                    record.Lifecycle.UpdatedAt ?? record.Lifecycle.PublishedAt));
            }
        }

        new AgentManifestProjection().Project(outputDir, entries);
    }

    private static void GenerateRootLlms(AppConfig config, string outputDir, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results, ILogger logger)
    {
        if (!config.Site.Seo.Geo.Enabled || !config.Site.Seo.Geo.LlmsTxt)
        {
            return;
        }

        LlmsTxtPlugin.WriteLlmsTxt(BuildRootPluginContext(config, outputDir, rootBaseUrl, results, logger), config.Site.Seo.Geo);
    }

    private static void GenerateRootLlmsFull(AppConfig config, string outputDir, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results, ILogger logger)
    {
        if (!config.Site.Seo.Geo.Enabled || !config.Site.Seo.Geo.LlmsFullTxt)
        {
            return;
        }

        LlmsTxtPlugin.WriteLlmsFullTxt(BuildRootPluginContext(config, outputDir, rootBaseUrl, results, logger));
    }

    private static void GenerateRootRobots(AppConfig config, string outputDir, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results)
    {
        var seoIndex = BuildRootSeoIndex(results);
        RobotsTxtWriter.WriteIfRequested(config, outputDir, rootBaseUrl, seoIndex);
    }

    private static BuildContext BuildRootPluginContext(
        AppConfig config,
        string outputDir,
        string rootBaseUrl,
        IReadOnlyList<BuildVariantResult> results,
        ILogger logger)
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>();
        var routedDocuments = new List<(ContentDocument Document, RouteInfo Route)>();
        var derivedRouted = new List<(ContentItem Item, RouteInfo Route)>();
        var records = new List<ContentRecord>();
        var entities = new List<EntityRecord>();
        var documents = new List<ContentDocument>();
        var relations = new List<ContentRelation>();
        var seoModels = new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase);
        var bodySources = new Dictionary<string, (ContentItem Item, IContentBodyStore Store)>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            var mergedRouted = result.Routed.Select(x => (x.Item, MergeRoute(result, x.Route))).ToList();
            routed.AddRange(mergedRouted);
            routedDocuments.AddRange(VariantBuildPipeline.BuildRoutedDocuments(
                mergedRouted,
                result.ContentGraph ?? CanonicalContentGraph.Empty));
            derivedRouted.AddRange(result.DerivedRouted.Select(x => (x.Item, MergeRoute(result, x.Route))));
            foreach (var (item, _) in result.Routed.Concat(result.DerivedRouted))
            {
                bodySources[BuildBodyStoreKey(item)] = (item, result.BodyStore);
            }

            records.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Records);
            entities.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Entities);
            documents.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Documents);
            relations.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Relations);
            foreach (var (key, model) in result.SeoModels)
            {
                seoModels[BuildMergedKey(result.Language, key)] = model;
            }
        }

        var context = new BuildContext
        {
            Config = config,
            RootDir = Directory.GetCurrentDirectory(),
            OutputDir = outputDir,
            BaseUrl = rootBaseUrl,
            LayoutsDir = string.Empty,
            Routed = routed,
            RoutedDocuments = routedDocuments,
            ContentGraph = new CanonicalContentGraph(records, entities, documents, relations),
            BodyStore = new MergedVariantContentBodyStore(bodySources),
            SeoIndex = BuildRootSeoIndex(results),
            Logger = logger
        };
        context.DerivedRouted.AddRange(derivedRouted);
        context.Data["__seo_models"] = seoModels;
        return context;
    }

    private static IReadOnlyDictionary<string, SeoIndexEntry> BuildRootSeoIndex(IReadOnlyList<BuildVariantResult> results)
    {
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            foreach (var (key, entry) in result.SeoIndex)
            {
                seoIndex[BuildMergedKey(result.Language, key)] = entry with
                {
                    Route = MergeRoute(result, entry.Route)
                };
            }
        }

        return seoIndex;
    }

    private static RouteInfo MergeRoute(BuildVariantResult result, RouteInfo route)
        => new(
            CombineBaseUrl(result.BaseUrl, route.Url),
            Path.Combine(result.Language, route.OutputPath),
            route.Template);

    private static string BuildBodyStoreKey(ContentItem item)
    {
        var language = MetaHelpers.GetString(item.Meta, "language") ?? string.Empty;
        return item.Id + "\n" + language;
    }

    private sealed class MergedVariantContentBodyStore : IContentBodyStore
    {
        private readonly IReadOnlyDictionary<string, (ContentItem Item, IContentBodyStore Store)> _sources;

        public MergedVariantContentBodyStore(IReadOnlyDictionary<string, (ContentItem Item, IContentBodyStore Store)> sources)
        {
            _sources = sources;
        }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            if (_sources.TryGetValue(BuildBodyStoreKey(item), out var source))
            {
                return source.Store.GetAsync(source.Item, cancellationToken);
            }

            return NullContentBodyStore.Instance.GetAsync(item, cancellationToken);
        }
    }

    private static IReadOnlyList<DefaultContentProjectionWriter.RepresentationEntry> PrefixRepresentationUrls(
        string baseUrl,
        IReadOnlyList<DefaultContentProjectionWriter.RepresentationEntry> representations)
    {
        return representations.Select(x => x.Kind switch
        {
            "json" or "markdown" => x with { Url = CombineBaseUrl(baseUrl, x.Url) },
            _ => x
        }).ToArray();
    }

    private static IReadOnlyList<PublishProjectionResult> BuildRootProjectionResults(
        string outputDir,
        IReadOnlyList<BuildVariantResult> results)
    {
        return PublishRepresentationRegistry.AggregateRepresentations()
            .Select(representation => new PublishProjectionResult(
                representation,
                BuildRootRepresentationOutputs(outputDir, representation, results)))
            .ToArray();
    }

    private static IReadOnlyList<PublishRepresentationOutput> BuildRootRepresentationOutputs(
        string outputDir,
        PublishRepresentation representation,
        IReadOnlyList<BuildVariantResult> results)
    {
        var path = Path.Combine(outputDir, representation.Path);
        var fileExists = File.Exists(path);
        var text = fileExists ? File.ReadAllText(path) : null;
        var outputs = new List<PublishRepresentationOutput>();
        foreach (var result in results)
        {
            foreach (var (_, seo) in result.SeoIndex.OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                var url = CombineBaseUrl(result.BaseUrl, seo.Route.Url);
                var routePresent = ContainsInvariant(text, url) || ContainsInvariant(text, seo.Canonical);
                var exists = representation.Kind.Equals("robots", StringComparison.OrdinalIgnoreCase)
                    ? fileExists
                    : fileExists && seo.Indexable && routePresent;
                outputs.Add(new PublishRepresentationOutput(
                    representation.Kind,
                    url,
                    representation.Path.Replace('\\', '/'),
                    exists,
                    seo.Indexable));
            }
        }

        if (outputs.Count > 0)
        {
            return outputs;
        }

        return [new PublishRepresentationOutput(representation.Kind, "/" + representation.Path.Replace('\\', '/'), representation.Path.Replace('\\', '/'), fileExists, Indexable: false)];
    }

    private static bool ContainsInvariant(string? haystack, string needle)
        => !string.IsNullOrWhiteSpace(haystack) &&
           !string.IsNullOrWhiteSpace(needle) &&
           haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string BuildMergedKey(string language, string key) => language + "/" + key;

    private static string CombineBaseUrl(string baseUrl, string routeUrl)
    {
        var b = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var r = routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;
        return string.IsNullOrWhiteSpace(b) ? r : b + r;
    }

    private static IReadOnlyList<RouteInfo> BuildCollectionListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections)
    {
        if (collections is null || collections.Count == 0)
        {
            return Array.Empty<RouteInfo>();
        }

        var routes = new List<RouteInfo>();
        foreach (var (_, cfg) in collections)
        {
            if (!cfg.Output.Sitemap || string.IsNullOrWhiteSpace(cfg.ListRoute))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(cfg.ListTemplate))
            {
                continue;
            }

            var url = RoutePathBuilder.NormalizeListRoute(cfg.ListRoute);
            var template = cfg.ListTemplate.Trim();
            routes.Add(new RouteInfo(url, RoutePathBuilder.BuildOutputPathFromUrl(url), template));
        }

        return routes;
    }
}
