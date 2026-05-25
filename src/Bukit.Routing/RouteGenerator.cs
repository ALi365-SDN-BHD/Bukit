using Bukit.Content;

namespace Bukit.Routing;

public static class RouteGenerator
{
    public sealed record CollectionRouteRule(string Permalink, string Template);

    public enum RouteSource
    {
        FullOverride,
        PartialOverride,
        Collection,
        Permalink,
        BuiltinFallback
    }

    public sealed record RouteGenerationResult(RouteInfo Route, RouteSource Source);

    public static RouteInfo Generate(
        ContentItem item,
        string outputPathEncoding = "none",
        IReadOnlyDictionary<string, string>? permalinks = null,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections = null)
    {
        return GenerateWithSource(item, outputPathEncoding, permalinks, collections).Route;
    }

    public static RouteGenerationResult GenerateWithSource(
        ContentItem item,
        string outputPathEncoding = "none",
        IReadOnlyDictionary<string, string>? permalinks = null,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections = null)
    {
        if (TryReadFullRouteOverride(item, outputPathEncoding, out var overridden))
        {
            return new RouteGenerationResult(ValidateRoute(overridden, item), RouteSource.FullOverride);
        }

        var (baseRoute, baseSource) = GenerateBaseRouteWithSource(item, outputPathEncoding, permalinks, collections);
        if (TryApplyPartialRouteOverride(item, outputPathEncoding, baseRoute, out var partialOverride))
        {
            return new RouteGenerationResult(ValidateRoute(partialOverride, item), RouteSource.PartialOverride);
        }

        return new RouteGenerationResult(ValidateRoute(baseRoute, item), baseSource);
    }

    private static RouteInfo BuildFromPattern(ContentItem item, string pattern, string template, string outputPathEncoding)
    {
        var url = ExpandPermalinkPattern(pattern, item);
        url = RoutePathBuilder.NormalizeUrl(url);
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);

        return new RouteInfo(url, outputPath, template);
    }

    private static RouteInfo ValidateRoute(RouteInfo route, ContentItem item)
    {
        RouteSecurityValidator.ValidateInternalUrl(route.Url, $"route.url for {item.Slug}");
        RouteSecurityValidator.ValidateOutputPath(route.OutputPath, $"route.outputPath for {item.Slug}");
        return route;
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

    private static (RouteInfo Route, RouteSource Source) GenerateBaseRouteWithSource(
        ContentItem item,
        string outputPathEncoding,
        IReadOnlyDictionary<string, string>? permalinks,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections)
    {
        var collectionKey = GetCollection(item);

        if (collections is not null && collections.TryGetValue(collectionKey, out var rule))
        {
            return (BuildFromPattern(item, rule.Permalink, rule.Template, outputPathEncoding), RouteSource.Collection);
        }

        var type = GetType(item);

        if (permalinks is not null && permalinks.TryGetValue(type, out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            var template = type.Equals("post", StringComparison.OrdinalIgnoreCase) ? "pages/post.html" : "pages/page.html";
            return (BuildFromPattern(item, pattern, template, outputPathEncoding), RouteSource.Permalink);
        }

        var (url, outputBase, templateName) = type switch
        {
            "post" => ($"/blog/{item.Slug}/", "blog", "pages/post.html"),
            _ => ($"/pages/{item.Slug}/", "pages", "pages/page.html")
        };

        var route = new RouteInfo(
            Url: url,
            OutputPath: Path.Combine(outputBase, item.Slug, "index.html"),
            Template: templateName
        );

        return (route with
        {
            OutputPath = RoutePathBuilder.NormalizeOutputPath(route.OutputPath, outputPathEncoding)
        }, RouteSource.BuiltinFallback);
    }

    private static bool TryReadFullRouteOverride(ContentItem item, string outputPathEncoding, out RouteInfo route)
    {
        if (TryGetRouteFields(item.Meta, out var url, out var outputPath, out var template))
        {
            if (!string.IsNullOrWhiteSpace(url) &&
                !string.IsNullOrWhiteSpace(outputPath) &&
                !string.IsNullOrWhiteSpace(template))
            {
                RouteSecurityValidator.ValidateInternalUrl(url, $"route.url for {item.Slug}");
                var normalizedOutputPath = RoutePathBuilder.NormalizeOutputPath(outputPath, outputPathEncoding);
                RouteSecurityValidator.ValidateOutputPath(normalizedOutputPath, $"route.outputPath for {item.Slug}");
                route = new RouteInfo(
                    RoutePathBuilder.NormalizeUrl(url),
                    normalizedOutputPath,
                    template.Trim());
                return true;
            }
        }

        route = default!;
        return false;
    }

    private static bool TryApplyPartialRouteOverride(ContentItem item, string outputPathEncoding, RouteInfo baseRoute, out RouteInfo route)
    {
        route = default!;
        if (!TryGetPartialRouteFields(item.Meta, out var url, out var outputPathOverride, out var template))
        {
            return false;
        }

        var useOutputPathOverride = !string.IsNullOrWhiteSpace(outputPathOverride) && HasNestedRouteMap(item.Meta);
        var normalizedUrl = string.IsNullOrWhiteSpace(url)
            ? baseRoute.Url
            : RoutePathBuilder.NormalizeUrl(url);
        var outputPath = useOutputPathOverride
            ? RoutePathBuilder.NormalizeOutputPath(outputPathOverride, outputPathEncoding)
            : string.IsNullOrWhiteSpace(url)
                ? baseRoute.OutputPath
                : RoutePathBuilder.BuildOutputPathFromUrl(normalizedUrl, outputPathEncoding);
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

    private static bool HasNestedRouteMap(IReadOnlyDictionary<string, object> meta)
        => meta.TryGetValue("route", out var routeObj) && routeObj is IReadOnlyDictionary<string, object>;

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
