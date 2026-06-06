using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Routing;

public static class RouteGenerator
{
    public sealed record CollectionRouteRule(string Permalink, string Template);

    public enum RouteSource
    {
        FullOverride,
        PartialOverride,
        Collection,
        Permalink
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

    public static RouteInfo Generate(
        ContentDocument document,
        string outputPathEncoding = "none",
        IReadOnlyDictionary<string, string>? permalinks = null,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections = null)
    {
        return GenerateWithSource(document, outputPathEncoding, permalinks, collections).Route;
    }

    public static RouteGenerationResult GenerateWithSource(
        ContentDocument document,
        string outputPathEncoding = "none",
        IReadOnlyDictionary<string, string>? permalinks = null,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections = null)
    {
        if (!string.IsNullOrWhiteSpace(document.Route.Url) &&
            !string.IsNullOrWhiteSpace(document.Route.OutputPath) &&
            !string.IsNullOrWhiteSpace(document.Route.Template))
        {
            var route = ValidateRoute(
                new RouteInfo(
                    RoutePathBuilder.NormalizeUrl(document.Route.Url),
                    RoutePathBuilder.NormalizeOutputPath(document.Route.OutputPath, outputPathEncoding),
                    document.Route.Template.Trim()),
                document.Record.Identity.Slug);
            return new RouteGenerationResult(route, RouteSource.FullOverride);
        }

        var collectionKey = !string.IsNullOrWhiteSpace(document.Route.ListGroup)
            ? document.Route.ListGroup
            : document.Record.Classification.Collection;
        var type = !string.IsNullOrWhiteSpace(document.Record.Identity.ContentType)
            ? document.Record.Identity.ContentType
            : document.Record.Classification.Type;

        if (collections is not null && collections.TryGetValue(collectionKey, out var rule))
        {
            var route = BuildFromPattern(document, rule.Permalink, rule.Template, outputPathEncoding);
            return new RouteGenerationResult(ApplyPartialRoutePolicy(document, outputPathEncoding, route), RouteSource.Collection);
        }

        if (permalinks is not null && permalinks.TryGetValue(type, out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            var route = BuildFromPattern(document, pattern, string.Empty, outputPathEncoding);
            return new RouteGenerationResult(ApplyPartialRoutePolicy(document, outputPathEncoding, route), RouteSource.Permalink);
        }

        throw new ConfigException(
            $"No route rule matches content document '{document.Record.Identity.Id}' (collection='{collectionKey}', type='{type}'). " +
            "Add an explicit collection rule, site.permalinks.<type>, or typed route policy.");
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

        if (!HasNestedRouteMap(item.Meta) &&
            item.Meta.TryGetValue("outputPath", out var topOutputPath) &&
            topOutputPath is string ops &&
            !string.IsNullOrWhiteSpace(ops))
        {
            throw new ConfigException($"Top-level outputPath is deprecated. Use route.outputPath instead. Found in front matter: outputPath: '{ops}'");
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

    private static RouteInfo BuildFromPattern(ContentDocument document, string pattern, string template, string outputPathEncoding)
    {
        var url = ExpandPermalinkPattern(pattern, document);
        url = RoutePathBuilder.NormalizeUrl(url);
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);

        return ValidateRoute(new RouteInfo(url, outputPath, template), document.Record.Identity.Slug);
    }

    private static RouteInfo ApplyPartialRoutePolicy(
        ContentDocument document,
        string outputPathEncoding,
        RouteInfo baseRoute)
    {
        if (string.IsNullOrWhiteSpace(document.Route.Url) &&
            string.IsNullOrWhiteSpace(document.Route.OutputPath) &&
            string.IsNullOrWhiteSpace(document.Route.Template))
        {
            return baseRoute;
        }

        var normalizedUrl = string.IsNullOrWhiteSpace(document.Route.Url)
            ? baseRoute.Url
            : RoutePathBuilder.NormalizeUrl(document.Route.Url);
        var outputPath = string.IsNullOrWhiteSpace(document.Route.OutputPath)
            ? string.IsNullOrWhiteSpace(document.Route.Url)
                ? baseRoute.OutputPath
                : RoutePathBuilder.BuildOutputPathFromUrl(normalizedUrl, outputPathEncoding)
            : RoutePathBuilder.NormalizeOutputPath(document.Route.OutputPath, outputPathEncoding);
        var template = string.IsNullOrWhiteSpace(document.Route.Template)
            ? baseRoute.Template
            : document.Route.Template.Trim();

        return ValidateRoute(new RouteInfo(normalizedUrl, outputPath, template), document.Record.Identity.Slug);
    }

    private static RouteInfo ValidateRoute(RouteInfo route, ContentItem item)
    {
        RouteSecurityValidator.ValidateInternalUrl(route.Url, $"route.url for {item.Slug}");
        RouteSecurityValidator.ValidateOutputPath(route.OutputPath, $"route.outputPath for {item.Slug}");
        return route;
    }

    private static RouteInfo ValidateRoute(RouteInfo route, string slug)
    {
        RouteSecurityValidator.ValidateInternalUrl(route.Url, $"route.url for {slug}");
        RouteSecurityValidator.ValidateOutputPath(route.OutputPath, $"route.outputPath for {slug}");
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

        var typeVal = item.GetContentType();
        result = result.Replace("{type}", typeVal, StringComparison.OrdinalIgnoreCase);

        var collectionVal = item.GetCollection();
        result = result.Replace("{collection}", collectionVal, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    public static string ExpandPermalinkPattern(string pattern, ContentDocument document)
    {
        var result = pattern;
        result = result.Replace("{slug}", document.Record.Identity.Slug, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{title}", RoutePathBuilder.Slugify(document.Record.Presentation.Title), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{year}", document.Record.Lifecycle.PublishedAt.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{month}", document.Record.Lifecycle.PublishedAt.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{day}", document.Record.Lifecycle.PublishedAt.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{type}", document.Record.Identity.ContentType, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{collection}", document.Record.Classification.Collection, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static (RouteInfo Route, RouteSource Source) GenerateBaseRouteWithSource(
        ContentItem item,
        string outputPathEncoding,
        IReadOnlyDictionary<string, string>? permalinks,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections)
    {
        var collectionKey = GetCollection(item);
        var type = GetType(item);

        if (collections is not null && collections.TryGetValue(collectionKey, out var rule))
        {
            return (BuildFromPattern(item, rule.Permalink, rule.Template, outputPathEncoding), RouteSource.Collection);
        }

        if (permalinks is not null && permalinks.TryGetValue(type, out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            return (BuildFromPattern(item, pattern, string.Empty, outputPathEncoding), RouteSource.Permalink);
        }

        throw new ConfigException(
            $"No route rule matches content item '{item.Id}' (collection='{collectionKey}', type='{type}'). " +
            "Add an explicit collection rule, site.permalinks.<type>, or route front matter.");
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
        return item.GetContentType();
    }

    private static string GetCollection(ContentItem item)
    {
        return item.GetCollection();
    }
}
