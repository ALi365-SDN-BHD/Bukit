using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class SitemapPlugin : IBukitPlugin, IAfterBuildPlugin
{
    private readonly AppConfig _config;

    internal SitemapPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "sitemap";
    public string Version => "2.0.1";

    public void AfterBuild(BuildContext context)
    {
        if (_config.Site.Languages is { Count: > 0 } && _config.Site.SitemapMode.Equals("merged", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var siteUrl = _config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return;
        }

        var filtered = new List<(string AbsoluteUrl, DateTimeOffset? LastModified)>(context.SeoIndex.Count);
        var documentExclusions = BuildDocumentSitemapExclusions(
            _config,
            context.RoutedDocuments.Concat(context.DerivedDocuments));
        var listRouteExclusions = BuildListRouteSitemapExclusions(context, _config);
        foreach (var seo in context.SeoIndex.Values.OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!seo.Indexable)
            {
                continue;
            }

            if (documentExclusions.Contains(BuildPathUtils.NormalizeRelPath(seo.Route.OutputPath)))
            {
                continue;
            }

            if (listRouteExclusions.Contains(BuildPathUtils.NormalizeRelPath(seo.Route.OutputPath)))
            {
                continue;
            }

            var absoluteHtmlPath = Path.Combine(context.OutputDir, seo.Route.OutputPath);
            if (SitemapPolicy.ShouldExcludeFromSitemapFile(absoluteHtmlPath, context.Logger))
            {
                continue;
            }

            filtered.Add((seo.Canonical, seo.LastModified));
        }

        SitemapGenerator.GenerateAbsolute(context.OutputDir, filtered);
    }

    internal static HashSet<string> BuildDocumentSitemapExclusions(
        AppConfig config,
        IEnumerable<RoutedContentDocument> documents)
    {
        return documents
            .Where(x => x.Document.Publish.ExcludeFromSitemap || IsCollectionSitemapExcluded(config, x.Document))
            .Select(x => BuildPathUtils.NormalizeRelPath(x.Route.OutputPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsCollectionSitemapExcluded(AppConfig config, ContentDocument document)
    {
        var collection = ContentFieldReader.GetCollection(document);
        return !string.IsNullOrWhiteSpace(collection) &&
               config.Site.Collections is { Count: > 0 } collections &&
               collections.TryGetValue(collection, out var collectionConfig) &&
               !collectionConfig.Output.Sitemap;
    }

    private static HashSet<string> BuildListRouteSitemapExclusions(
        BuildContext context,
        AppConfig config)
    {
        var graph = context.Data.TryGetValue(ListRouteGraphBuilder.BuildContextDataKey, out var value) && value is ListRouteGraph routeGraph
            ? routeGraph
            : null;
        return ListRouteSitemapPolicy.BuildExcludedOutputPaths(config, graph);
    }
}
