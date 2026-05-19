using Bukit.Content;

namespace Bukit.Routing;

public static class RouteGenerator
{
    public sealed record CollectionRouteRule(string Permalink, string Template);

    public static RouteInfo Generate(
        ContentItem item,
        string outputPathEncoding = "none",
        IReadOnlyDictionary<string, string>? permalinks = null,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections = null)
    {
        if (TryReadFullRouteOverride(item, outputPathEncoding, out var overridden))
        {
            return overridden;
        }

        var baseRoute = GenerateBaseRoute(item, outputPathEncoding, permalinks, collections);
        return TryApplyPartialRouteOverride(item, outputPathEncoding, baseRoute, out var partialOverride)
            ? partialOverride
            : baseRoute;
    }

    private static RouteInfo BuildFromPattern(ContentItem item, string pattern, string template, string outputPathEncoding)
    {
        var url = ExpandPermalinkPattern(pattern, item);
        url = RoutePathBuilder.NormalizeUrl(url);
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);

        return new RouteInfo(url, outputPath, template);
    }

    public static string ExpandPermalinkPattern(string pattern, ContentItem item)
    {
        var result = pattern;
        result = result.Replace("{slug}", item.Slug, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{title}", RoutePathBuilder.Slugify(item.Title), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{year}", item.PublishAt.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{month}", item.PublishAt.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{day}", item.PublishAt.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase);

        var typeVal = item.Meta.TryGetValue("type", out var t) && t is not null ? (t.ToString() ?? "page") : "page";
        result = result.Replace("{type}", typeVal, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static RouteInfo GenerateBaseRoute(
        ContentItem item,
        string outputPathEncoding,
        IReadOnlyDictionary<string, string>? permalinks,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections)
    {
        var collectionKey = GetCollection(item);

        if (collections is not null && collections.TryGetValue(collectionKey, out var rule))
        {
            return BuildFromPattern(item, rule.Permalink, rule.Template, outputPathEncoding);
        }

        var type = GetType(item);

        if (permalinks is not null && permalinks.TryGetValue(type, out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            var template = type.Equals("post", StringComparison.OrdinalIgnoreCase) ? "pages/post.html" : "pages/page.html";
            return BuildFromPattern(item, pattern, template, outputPathEncoding);
        }

        var route = type switch
        {
            "post" => new RouteInfo(
                Url: $"/blog/{item.Slug}/",
                OutputPath: Path.Combine("blog", item.Slug, "index.html"),
                Template: "pages/post.html"
            ),
            "page" => new RouteInfo(
                Url: $"/pages/{item.Slug}/",
                OutputPath: Path.Combine("pages", item.Slug, "index.html"),
                Template: "pages/page.html"
            ),
            _ => new RouteInfo(
                Url: $"/pages/{item.Slug}/",
                OutputPath: Path.Combine("pages", item.Slug, "index.html"),
                Template: "pages/page.html"
            )
        };

        return route with
        {
            OutputPath = RoutePathBuilder.NormalizeOutputPath(route.OutputPath, outputPathEncoding)
        };
    }

    private static bool TryReadFullRouteOverride(ContentItem item, string outputPathEncoding, out RouteInfo route)
    {
        if (TryGetRouteFields(item.Meta, out var url, out var outputPath, out var template))
        {
            url = RoutePathBuilder.NormalizeUrl(url);
            outputPath = RoutePathBuilder.NormalizeOutputPath(outputPath, outputPathEncoding);
            template = template.Trim();

            if (!string.IsNullOrWhiteSpace(url) &&
                !string.IsNullOrWhiteSpace(outputPath) &&
                !string.IsNullOrWhiteSpace(template))
            {
                route = new RouteInfo(url, outputPath, template);
                return true;
            }
        }

        route = default!;
        return false;
    }

    private static bool TryApplyPartialRouteOverride(ContentItem item, string outputPathEncoding, RouteInfo baseRoute, out RouteInfo route)
    {
        route = default!;
        if (!TryGetPartialRouteFields(item.Meta, out var url, out _, out var template))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var normalizedUrl = RoutePathBuilder.NormalizeUrl(url);
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(normalizedUrl, outputPathEncoding);
        var effectiveTemplate = string.IsNullOrWhiteSpace(template) ? baseRoute.Template : template.Trim();
        route = new RouteInfo(normalizedUrl, outputPath, effectiveTemplate);
        return true;
    }

    private static bool TryGetRouteFields(
        IReadOnlyDictionary<string, object> meta,
        out string url,
        out string outputPath,
        out string template)
    {
        url = string.Empty;
        outputPath = string.Empty;
        template = string.Empty;

        if (meta.TryGetValue("route", out var routeObj) && routeObj is IReadOnlyDictionary<string, object> routeMap)
        {
            url = GetOptionalString(routeMap, "url");
            outputPath = GetOptionalString(routeMap, "outputPath");
            template = GetOptionalString(routeMap, "template");
            return !(string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(template));
        }

        if (meta.TryGetValue("url", out var u) && u is string us) url = us;
        if (meta.TryGetValue("outputPath", out var o) && o is string os) outputPath = os;
        if (meta.TryGetValue("template", out var t) && t is string ts) template = ts;

        return !(string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(template));
    }

    private static bool TryGetPartialRouteFields(
        IReadOnlyDictionary<string, object> meta,
        out string url,
        out string outputPath,
        out string template)
    {
        url = string.Empty;
        outputPath = string.Empty;
        template = string.Empty;

        if (meta.TryGetValue("route", out var routeObj) && routeObj is IReadOnlyDictionary<string, object> routeMap)
        {
            url = GetOptionalString(routeMap, "url");
            outputPath = GetOptionalString(routeMap, "outputPath");
            template = GetOptionalString(routeMap, "template");
            return !(string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(outputPath) && string.IsNullOrWhiteSpace(template));
        }

        if (meta.TryGetValue("url", out var u) && u is string us) url = us;
        if (meta.TryGetValue("outputPath", out var o) && o is string os) outputPath = os;
        if (meta.TryGetValue("template", out var t) && t is string ts) template = ts;

        return !(string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(outputPath) && string.IsNullOrWhiteSpace(template));
    }

    private static string GetOptionalString(IReadOnlyDictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var v) && v is string s ? s : string.Empty;
    }

    private static string GetType(ContentItem item)
    {
        if (item.Meta.TryGetValue("type", out var v) && v is not null)
        {
            return v.ToString() ?? "page";
        }

        return "page";
    }

    private static string GetCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        return GetType(item);
    }
}
