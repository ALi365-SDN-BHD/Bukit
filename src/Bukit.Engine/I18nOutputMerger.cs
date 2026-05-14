using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;
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
        var defaultLanguage = string.IsNullOrWhiteSpace(config.Site.DefaultLanguage) ? null : config.Site.DefaultLanguage.Trim();

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

        bool IsExcluded(BuildVariantResult r, string outputPath)
        {
            var key = BuildPathUtils.NormalizeRelPath(outputPath);
            if (r.SeoIndex.TryGetValue(key, out var seo) && !seo.Indexable)
            {
                return true;
            }

            return IsExcludedFile(Path.Combine(r.OutputDir, outputPath));
        }

        var rootIndexPath = "index.html";
        var collectionListRoutes = BuildCollectionListRoutes(config.Site.Collections);

        var alternatesMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        void AddAlternate(string groupKey, string language, string absoluteUrl)
        {
            if (!alternatesMap.TryGetValue(groupKey, out var langs))
            {
                langs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                alternatesMap[groupKey] = langs;
            }

            if (!langs.ContainsKey(language))
            {
                langs[language] = absoluteUrl;
            }
        }

        foreach (var r in results)
        {
            if (!IsExcluded(r, rootIndexPath))
            {
                AddAlternate("/", r.Language, SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, "/"));
            }

            foreach (var listRoute in collectionListRoutes)
            {
                if (!IsExcluded(r, listRoute.OutputPath))
                {
                    AddAlternate(listRoute.Url, r.Language, SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, listRoute.Url));
                }
            }

            foreach (var (item, route) in r.Routed)
            {
                if (MetaHelpers.TryGetI18nKey(item.Meta, out var key))
                {
                    if (!IsExcluded(r, route.OutputPath))
                    {
                        AddAlternate(key, r.Language, SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, route.Url));
                    }
                }
            }

            foreach (var (route, _) in r.DerivedRoutes)
            {
                if (!IsExcluded(r, route.OutputPath))
                {
                    AddAlternate(route.Url, r.Language, SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, route.Url));
                }
            }
        }

        IReadOnlyList<SitemapGenerator.Alternate>? BuildAlternates(string groupKey)
        {
            if (!alternatesMap.TryGetValue(groupKey, out var map) || map.Count <= 1)
            {
                return null;
            }

            var list = new List<SitemapGenerator.Alternate>(capacity: map.Count + 1);
            if (!string.IsNullOrWhiteSpace(defaultLanguage) && map.TryGetValue(defaultLanguage, out var defHref))
            {
                list.Add(new SitemapGenerator.Alternate("x-default", defHref));
            }

            foreach (var kv in map.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(new SitemapGenerator.Alternate(kv.Key, kv.Value));
            }

            return list;
        }

        var entries = new List<SitemapGenerator.UrlEntry>();
        foreach (var r in results)
        {
            if (!IsExcluded(r, rootIndexPath))
            {
                entries.Add(new SitemapGenerator.UrlEntry(
                    SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, "/"),
                    DateTimeOffset.UtcNow,
                    BuildAlternates("/")));
            }

            foreach (var listRoute in collectionListRoutes)
            {
                if (!IsExcluded(r, listRoute.OutputPath))
                {
                    entries.Add(new SitemapGenerator.UrlEntry(
                        SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, listRoute.Url),
                        DateTimeOffset.UtcNow,
                        BuildAlternates(listRoute.Url)));
                }
            }

            foreach (var (item, route) in r.Routed)
            {
                if (IsExcluded(r, route.OutputPath))
                {
                    continue;
                }

                var alts = MetaHelpers.TryGetI18nKey(item.Meta, out var key) ? BuildAlternates(key) : null;
                entries.Add(new SitemapGenerator.UrlEntry(
                    SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, route.Url),
                    SitemapPolicy.ResolveLastModified(item),
                    alts));
            }

            foreach (var (route, lastModified) in r.DerivedRoutes)
            {
                if (IsExcluded(r, route.OutputPath))
                {
                    continue;
                }

                entries.Add(new SitemapGenerator.UrlEntry(
                    SitemapGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, route.Url),
                    lastModified,
                    BuildAlternates(route.Url)));
            }
        }

        SitemapGenerator.GenerateAbsoluteWithAlternates(outputDir, entries);
    }

    private static void GenerateMergedRss(AppConfig config, string outputDir, string siteUrl, string rootBaseUrl, IReadOnlyList<BuildVariantResult> results)
    {
        static IReadOnlyList<string>? MergeCategories(IReadOnlyList<string>? tags, IReadOnlyList<string>? categories)
        {
            if (tags is null && categories is null)
            {
                return null;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();

            void Add(IReadOnlyList<string>? items)
            {
                if (items is null)
                {
                    return;
                }

                foreach (var v in items)
                {
                    var t = (v ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(t) && seen.Add(t))
                    {
                        list.Add(t);
                    }
                }
            }

            Add(tags);
            Add(categories);
            return list.Count == 0 ? null : list;
        }

        var posts = new List<RssGenerator.Post>();
        var rssCollections = ResolveRssCollections(config.Site.Collections);
        foreach (var r in results)
        {
            foreach (var (item, route) in r.Routed.Where(x => rssCollections.Contains(GetCollection(x.Item))))
            {
                var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
                if (r.SeoIndex.TryGetValue(key, out var seo) && !seo.Indexable)
                {
                    continue;
                }

                posts.Add(new RssGenerator.Post(
                    Title: item.Title,
                    AbsoluteUrl: RssGenerator.BuildAbsoluteUrl(siteUrl, r.BaseUrl, route.Url),
                    PublishAt: item.PublishAt,
                    Description: MetaHelpers.GetString(item.Meta, "summary"),
                    Categories: MergeCategories(MetaHelpers.GetStringList(item.Meta, "tags"), MetaHelpers.GetStringList(item.Meta, "categories")),
                    ContentHtml: ContentBodyResolver.GetHtml(item, r.BodyStore)));
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
                new RouteInfo("/blog/", Path.Combine("blog", "index.html"), "pages/list.html"),
                new RouteInfo("/pages/", Path.Combine("pages", "index.html"), "pages/list.html")
            };
        }

        var routes = new List<RouteInfo>();
        foreach (var (_, cfg) in collections)
        {
            if (!cfg.Output.Sitemap || string.IsNullOrWhiteSpace(cfg.ListRoute))
            {
                continue;
            }

            var url = NormalizeListRoute(cfg.ListRoute);
            routes.Add(new RouteInfo(url, BuildListOutputPath(url), "pages/list.html"));
        }

        return routes;
    }

    private static string NormalizeListRoute(string route)
    {
        var value = (route ?? string.Empty).Trim();
        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        if (!value.EndsWith('/'))
        {
            value += "/";
        }

        return value;
    }

    private static string BuildListOutputPath(string route)
    {
        var trimmed = route.Trim('/');
        return string.IsNullOrWhiteSpace(trimmed)
            ? "index.html"
            : Path.Combine(trimmed.Replace('/', Path.DirectorySeparatorChar), "index.html");
    }
}
