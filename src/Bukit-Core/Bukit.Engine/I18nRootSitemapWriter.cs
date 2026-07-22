using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class I18nRootSitemapWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["sitemap"];

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        _ = representation;
        var siteUrl = context.Config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return;
        }

        var sitemapMode = (context.Config.Site.SitemapMode ?? "split").Trim().ToLowerInvariant();
        if (sitemapMode == "merged")
        {
            GenerateMergedSitemap(context, siteUrl);
        }
        else if (sitemapMode == "index")
        {
            var sitemaps = context.Results
                .Select(result => SitemapGenerator.BuildAbsoluteUrl(siteUrl, result.BaseUrl, "/sitemap.xml"))
                .ToList();
            SitemapGenerator.GenerateIndex(context.OutputDir, sitemaps);
        }
    }

    private static void GenerateMergedSitemap(I18nRootProjectionWriterContext context, string siteUrl)
    {
        _ = siteUrl;
        var excludeCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool IsExcludedFile(string absoluteHtmlPath)
        {
            if (excludeCache.TryGetValue(absoluteHtmlPath, out var cached))
            {
                return cached;
            }

            var excluded = SitemapPolicy.ShouldExcludeFromSitemapFile(absoluteHtmlPath, context.Logger);
            excludeCache[absoluteHtmlPath] = excluded;
            return excluded;
        }

        var entries = new List<SitemapGenerator.UrlEntry>();
        foreach (var result in context.Results)
        {
            var documentExclusions = SitemapPlugin.BuildDocumentSitemapExclusions(
                context.Config,
                result.RoutedDocuments.Concat(result.DerivedDocuments));
            foreach (var (key, seo) in result.SeoIndex
                         .Where(x => x.Value.Indexable)
                         .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                if (ListRouteSitemapPolicy.IsExcluded(context.Config, result.ListRouteGraph, seo))
                {
                    continue;
                }

                if (documentExclusions.Contains(BuildPathUtils.NormalizeRelPath(seo.Route.OutputPath)))
                {
                    continue;
                }

                if (IsExcludedFile(Path.Combine(result.OutputDir, seo.Route.OutputPath)))
                {
                    continue;
                }

                entries.Add(new SitemapGenerator.UrlEntry(
                    seo.Canonical,
                    seo.LastModified,
                    result.SeoModels.TryGetValue(key, out var model)
                        ? BuildAlternates(model.Alternates)
                        : null));
            }
        }

        SitemapGenerator.GenerateAbsoluteWithAlternates(context.OutputDir, entries);
    }

    private static IReadOnlyList<SitemapGenerator.Alternate>? BuildAlternates(
        IReadOnlyList<Bukit.Rendering.SeoAlternateModel> alternates)
    {
        if (alternates.Count <= 1)
        {
            return null;
        }

        return alternates
            .OrderBy(
                x => string.Equals(x.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : x.Hreflang,
                StringComparer.OrdinalIgnoreCase)
            .Select(x => new SitemapGenerator.Alternate(x.Hreflang, x.Href))
            .ToList();
    }
}
