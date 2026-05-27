using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
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

            if (item.Meta.TryGetValue("language", out var v) && v is not null && !string.IsNullOrWhiteSpace(v.ToString()))
            {
                return string.Equals(v.ToString(), language, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    internal static void GenerateRootOutputs(AppConfig config, string outputDir, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results, ILogger logger, ISearchIndexBuilder searchIndexBuilder)
    {
        var siteUrl = config.Site.Url;
        if (!string.IsNullOrWhiteSpace(siteUrl))
        {
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

            var rssMode = (config.Site.RssMode ?? "split").Trim().ToLowerInvariant();
            if (rssMode == "merged")
            {
                GenerateMergedRss(config, outputDir, siteUrl, rootBaseUrl, results);
            }
        }

        var searchMode = (config.Site.SearchMode ?? "split").Trim().ToLowerInvariant();
        if (searchMode == "merged")
        {
            searchIndexBuilder.GenerateMergedSearchIndex(outputDir, results, config.Site.SearchIncludeDerived);
        }
        else if (searchMode == "index")
        {
            searchIndexBuilder.GenerateSearchIndexIndex(outputDir, results);
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

    private static void GenerateMergedRss(AppConfig config, string outputDir, string siteUrl, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results)
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

                posts.Add(RssGenerator.ToPost(item, seo.Canonical, r.BodyStore));
            }
        }

        RssGenerator.GenerateMerged(outputDir, siteUrl, rootBaseUrl, config.Site.Title, posts, siteDescription: config.Site.Description);
    }

    private static HashSet<string> ResolveRssCollections(IReadOnlyDictionary<string, CollectionConfig>? collections)
    {
        if (collections is null || collections.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "post" };
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, cfg) in collections)
        {
            if (cfg.Output.Rss)
            {
                set.Add(key);
            }
        }

        return set.Count == 0 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "post" } : set;
    }

    private static string GetCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        if (item.Meta.TryGetValue("type", out var type) && type is not null && !string.IsNullOrWhiteSpace(type.ToString()))
        {
            return type.ToString()!;
        }

        return "page";
    }

    private static IReadOnlyList<RouteInfo> BuildCollectionListRoutes(IReadOnlyDictionary<string, CollectionConfig>? collections)
    {
        if (collections is null || collections.Count == 0)
        {
            return new[]
            {
                new RouteInfo("/blog/", RoutePathBuilder.BuildOutputPathFromUrl("/blog/"), "pages/list.html"),
                new RouteInfo("/pages/", RoutePathBuilder.BuildOutputPathFromUrl("/pages/"), "pages/list.html")
            };
        }

        var routes = new List<RouteInfo>();
        foreach (var (_, cfg) in collections)
        {
            if (!cfg.Output.Sitemap || string.IsNullOrWhiteSpace(cfg.ListRoute))
            {
                continue;
            }

            var url = RoutePathBuilder.NormalizeListRoute(cfg.ListRoute);
            var template = string.IsNullOrWhiteSpace(cfg.ListTemplate) ? "pages/list.html" : cfg.ListTemplate.Trim();
            routes.Add(new RouteInfo(url, RoutePathBuilder.BuildOutputPathFromUrl(url), template));
        }

        return routes;
    }
}
