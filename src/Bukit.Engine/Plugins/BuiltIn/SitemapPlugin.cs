using Bukit.Config;
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

        var metaRoutes = new List<(RouteInfo Route, DateTimeOffset LastModified)>(capacity: context.Routed.Count + 6)
        {
            (new RouteInfo("/", "index.html", "pages/index.html"), DateTimeOffset.UtcNow)
        };
        metaRoutes.AddRange(BuildCollectionListRoutes(context.Config.Site.Collections).Select(x => (x, DateTimeOffset.UtcNow)));

        metaRoutes.AddRange(context.Routed.Select(x => (x.Route, SitemapPolicy.ResolveLastModified(x.Item))));
        if (context.DerivedRoutes.Count > 0)
        {
            metaRoutes.AddRange(context.DerivedRoutes);
        }

        var filtered = new List<(RouteInfo Route, DateTimeOffset LastModified)>(metaRoutes.Count);
        foreach (var (route, lastModified) in metaRoutes)
        {
            var absoluteHtmlPath = Path.Combine(context.OutputDir, route.OutputPath);
            if (SitemapPolicy.ShouldExcludeFromSitemapFile(absoluteHtmlPath, context.Logger))
            {
                continue;
            }

            filtered.Add((route, lastModified));
        }

        SitemapGenerator.Generate(context.OutputDir, siteUrl, context.BaseUrl, filtered);
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

            var url = NormalizeRoute(cfg.ListRoute);
            var output = BuildOutputPath(url);
            routes.Add(new RouteInfo(url, output, "pages/list.html"));
        }

        return routes;
    }

    private static string NormalizeRoute(string route)
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

    private static string BuildOutputPath(string route)
    {
        var trimmed = route.Trim('/');
        return string.IsNullOrWhiteSpace(trimmed)
            ? "index.html"
            : Path.Combine(trimmed.Replace('/', Path.DirectorySeparatorChar), "index.html");
    }
}
