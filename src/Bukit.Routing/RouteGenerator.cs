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
        => GenerateWithSource(ToSource(document), outputPathEncoding, permalinks, collections);

    private static RouteGenerationResult GenerateWithSource(
        RouteContentSource source,
        string outputPathEncoding = "none",
        IReadOnlyDictionary<string, string>? permalinks = null,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections = null)
    {
        if (TryReadFullRouteOverride(source, outputPathEncoding, out var overridden))
        {
            return new RouteGenerationResult(ValidateRoute(overridden, source), RouteSource.FullOverride);
        }

        var topOutputPath = ContentFieldReader.GetText(source.Fields, "outputPath");
        if (!HasNestedRouteMap(source.Fields) && !string.IsNullOrWhiteSpace(topOutputPath))
        {
            throw new ConfigException($"Top-level outputPath is removed in 1.0. Use route.outputPath instead. Found in front matter: outputPath: '{topOutputPath}'", DiagnosticCode.RouteInvalidPattern);
        }

        var (baseRoute, baseSource) = GenerateBaseRouteWithSource(source, outputPathEncoding, permalinks, collections);
        if (TryApplyPartialRouteOverride(source, outputPathEncoding, baseRoute, out var partialOverride))
        {
            return new RouteGenerationResult(ValidateRoute(partialOverride, source), RouteSource.PartialOverride);
        }

        return new RouteGenerationResult(ValidateRoute(baseRoute, source), baseSource);
    }

    private static RouteInfo BuildFromPattern(RouteContentSource source, string pattern, string template, string outputPathEncoding)
    {
        var url = ExpandPermalinkPattern(pattern, source);
        url = RoutePathBuilder.NormalizeUrl(url);
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);

        return new RouteInfo(url, outputPath, template);
    }

    private static RouteInfo ValidateRoute(RouteInfo route, RouteContentSource source)
    {
        RouteSecurityValidator.ValidateInternalUrl(route.Url, $"route.url for {source.Slug}");
        RouteSecurityValidator.ValidateOutputPath(route.OutputPath, $"route.outputPath for {source.Slug}");
        return route;
    }

    public static string ExpandPermalinkPattern(string pattern, ContentDocument document)
        => ExpandPermalinkPattern(pattern, ToSource(document));

    private static string ExpandPermalinkPattern(string pattern, RouteContentSource source)
    {
        var result = pattern;
        result = result.Replace("{slug}", source.Slug, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{title}", RoutePathBuilder.Slugify(source.Title), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{year}", source.PublishAt.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{month}", source.PublishAt.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{day}", source.PublishAt.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase);

        var typeVal = GetType(source);
        result = result.Replace("{type}", typeVal, StringComparison.OrdinalIgnoreCase);

        var collectionVal = GetCollection(source);
        result = result.Replace("{collection}", collectionVal, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static (RouteInfo Route, RouteSource Source) GenerateBaseRouteWithSource(
        RouteContentSource source,
        string outputPathEncoding,
        IReadOnlyDictionary<string, string>? permalinks,
        IReadOnlyDictionary<string, CollectionRouteRule>? collections)
    {
        var collectionKey = GetCollection(source);
        var type = GetType(source);

        if (collections is not null && collections.TryGetValue(collectionKey, out var rule))
        {
            return (BuildFromPattern(source, rule.Permalink, rule.Template, outputPathEncoding), RouteSource.Collection);
        }

        if (permalinks is not null && permalinks.TryGetValue(type, out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            return (BuildFromPattern(source, pattern, string.Empty, outputPathEncoding), RouteSource.Permalink);
        }

        throw new ConfigException(
            $"No route rule matches content document '{source.Id}' (collection='{collectionKey}', type='{type}'). " +
            "Add an explicit collection rule, site.permalinks.<type>, or route front matter.");
    }

    private static bool TryReadFullRouteOverride(RouteContentSource source, string outputPathEncoding, out RouteInfo route)
    {
        if (TryGetRouteFields(source.Fields, out var url, out var outputPath, out var template))
        {
            if (!string.IsNullOrWhiteSpace(url) &&
                !string.IsNullOrWhiteSpace(outputPath) &&
                !string.IsNullOrWhiteSpace(template))
            {
                RouteSecurityValidator.ValidateInternalUrl(url, $"route.url for {source.Slug}");
                var normalizedOutputPath = RoutePathBuilder.NormalizeOutputPath(outputPath, outputPathEncoding);
                RouteSecurityValidator.ValidateOutputPath(normalizedOutputPath, $"route.outputPath for {source.Slug}");
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

    private static bool TryApplyPartialRouteOverride(RouteContentSource source, string outputPathEncoding, RouteInfo baseRoute, out RouteInfo route)
    {
        route = default!;
        if (!TryGetPartialRouteFields(source.Fields, out var url, out var outputPathOverride, out var template))
        {
            return false;
        }

        var useOutputPathOverride = !string.IsNullOrWhiteSpace(outputPathOverride) && HasNestedRouteMap(source.Fields);
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

    private static string GetType(RouteContentSource source)
    {
        return ContentFieldReader.GetText(source.Fields, "type") ?? source.ContentType;
    }

    private static string GetCollection(RouteContentSource source)
    {
        return ContentFieldReader.GetText(source.Fields, "collection") ?? source.Collection;
    }

    private static RouteContentSource ToSource(ContentDocument document)
        => new(
            document.Id,
            document.Title,
            document.Slug,
            document.PublishAt,
            document.CustomFields,
            document.Record.Identity.ContentType,
            document.Record.Classification.Collection);

    private sealed record RouteContentSource(
        string Id,
        string Title,
        string Slug,
        DateTimeOffset PublishAt,
        IReadOnlyDictionary<string, ContentField>? Fields,
        string ContentType,
        string Collection);
}
