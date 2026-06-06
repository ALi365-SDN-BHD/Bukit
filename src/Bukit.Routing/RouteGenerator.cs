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

        var topOutputPath = ContentFieldReader.GetText(item.Fields, "outputPath");
        if (!HasNestedRouteMap(item.Fields) && !string.IsNullOrWhiteSpace(topOutputPath))
        {
            throw new ConfigException($"Top-level outputPath is deprecated. Use route.outputPath instead. Found in front matter: outputPath: '{topOutputPath}'");
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

        var typeVal = ContentFieldReader.GetContentType(item);
        result = result.Replace("{type}", typeVal, StringComparison.OrdinalIgnoreCase);

        var collectionVal = ContentFieldReader.GetCollection(item);
        result = result.Replace("{collection}", collectionVal, StringComparison.OrdinalIgnoreCase);

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
        if (TryGetRouteFields(item.Fields, out var url, out var outputPath, out var template))
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
        if (!TryGetPartialRouteFields(item.Fields, out var url, out var outputPathOverride, out var template))
        {
            return false;
        }

        var useOutputPathOverride = !string.IsNullOrWhiteSpace(outputPathOverride) && HasNestedRouteMap(item.Fields);
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
        IReadOnlyDictionary<string, ContentField>? fields,
        out string url,
        out string outputPath,
        out string template)
    {
        url = string.Empty;
        outputPath = string.Empty;
        template = string.Empty;

        if (TryGetRouteMap(fields, out var routeMap))
        {
            url = GetOptionalString(routeMap, "url");
            outputPath = GetOptionalString(routeMap, "outputPath");
            template = GetOptionalString(routeMap, "template");
            return !(string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(template));
        }

        url = ContentFieldReader.GetText(fields, "url") ?? string.Empty;
        outputPath = ContentFieldReader.GetText(fields, "outputPath") ?? string.Empty;
        template = ContentFieldReader.GetText(fields, "template") ?? string.Empty;

        return !(string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(template));
    }

    private static bool TryGetPartialRouteFields(
        IReadOnlyDictionary<string, ContentField>? fields,
        out string url,
        out string outputPath,
        out string template)
    {
        url = string.Empty;
        outputPath = string.Empty;
        template = string.Empty;

        if (TryGetRouteMap(fields, out var routeMap))
        {
            url = GetOptionalString(routeMap, "url");
            outputPath = GetOptionalString(routeMap, "outputPath");
            template = GetOptionalString(routeMap, "template");
            return !(string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(outputPath) && string.IsNullOrWhiteSpace(template));
        }

        url = ContentFieldReader.GetText(fields, "url") ?? string.Empty;
        outputPath = ContentFieldReader.GetText(fields, "outputPath") ?? string.Empty;
        template = ContentFieldReader.GetText(fields, "template") ?? string.Empty;

        return !(string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(outputPath) && string.IsNullOrWhiteSpace(template));
    }

    private static string GetOptionalString(IReadOnlyDictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var v) && v is string s ? s : string.Empty;
    }

    private static bool HasNestedRouteMap(IReadOnlyDictionary<string, ContentField>? fields)
        => TryGetRouteMap(fields, out _);

    private static bool TryGetRouteMap(
        IReadOnlyDictionary<string, ContentField>? fields,
        out IReadOnlyDictionary<string, object> routeMap)
    {
        routeMap = default!;
        return ContentFieldReader.TryGetField(fields, "route", out var routeField) &&
               routeField.Value is IReadOnlyDictionary<string, object> map &&
               (routeMap = map) is not null;
    }

    private static string GetType(ContentItem item)
    {
        return ContentFieldReader.GetContentType(item);
    }

    private static string GetCollection(ContentItem item)
    {
        return ContentFieldReader.GetCollection(item);
    }
}
