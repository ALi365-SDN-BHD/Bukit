using Bukit.Config;
using Bukit.Engine;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class SitemapPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "sitemap";
    public string Version => "2.0.1";

    public void AfterBuild(BuildContext context)
    {
        if (context.Config.Site.Languages is { Count: > 0 } && context.Config.Site.SitemapMode.Equals("merged", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var siteUrl = context.Config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return;
        }

        var filtered = new List<(string AbsoluteUrl, DateTimeOffset LastModified)>(context.SeoIndex.Count);
        var typedExclusions = BuildTypedSitemapExclusions(context);
        foreach (var seo in context.SeoIndex.Values.OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!seo.Indexable)
            {
                continue;
            }

            if (typedExclusions.Contains(BuildPathUtils.NormalizeRelPath(seo.Route.OutputPath)))
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

    private static HashSet<string> BuildTypedSitemapExclusions(BuildContext context)
    {
        if (context.RoutedDocuments.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return context.RoutedDocuments
            .Where(x => x.Document.Publish.ExcludeFromSitemap)
            .Select(x => BuildPathUtils.NormalizeRelPath(x.Route.OutputPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
