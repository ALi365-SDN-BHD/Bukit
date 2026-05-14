using Bukit.Config;
using Bukit.Engine;
using Bukit.Routing;

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
        foreach (var seo in context.SeoIndex.Values.OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!seo.Indexable)
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
}
