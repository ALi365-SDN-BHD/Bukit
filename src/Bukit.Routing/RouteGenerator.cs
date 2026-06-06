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
        return GenerateWithSource(ToDocument(item), outputPathEncoding, permalinks, collections).Route;
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
            RouteSecurityValidator.ValidateInternalUrl(document.Route.Url, $"route.url for {document.Record.Identity.Slug}");
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
        return GenerateWithSource(ToDocument(item), outputPathEncoding, permalinks, collections);
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
            : NormalizeRouteUrl(document.Route.Url, document.Record.Identity.Slug);
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

    private static string NormalizeRouteUrl(string url, string slug)
    {
        RouteSecurityValidator.ValidateInternalUrl(url, $"route.url for {slug}");
        return RoutePathBuilder.NormalizeUrl(url);
    }

    private static RouteInfo ValidateRoute(RouteInfo route, string slug)
    {
        RouteSecurityValidator.ValidateInternalUrl(route.Url, $"route.url for {slug}");
        RouteSecurityValidator.ValidateOutputPath(route.OutputPath, $"route.outputPath for {slug}");
        return route;
    }

    public static string ExpandPermalinkPattern(string pattern, ContentItem item)
    {
        return ExpandPermalinkPattern(pattern, ToDocument(item));
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

    private static ContentDocument ToDocument(ContentItem item)
    {
        var fields = item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        var type = GetFieldString(fields, "type") ?? string.Empty;
        var collection = GetFieldString(fields, "collection") ?? string.Empty;
        var route = new ContentRoutePolicy(
            GetFieldString(fields, "url"),
            GetFieldString(fields, "outputPath"),
            GetFieldString(fields, "template"),
            null,
            collection);

        var record = new ContentRecord(
            new ContentIdentity(item.Id, item.Slug, item.Id, type, "published"),
            new ContentPresentation(item.Title, null, item.ContentHtml, "en", Array.Empty<string>()),
            new ContentClassification(type, collection, Array.Empty<string>(), Array.Empty<string>()),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(item.PublishAt, null, null, null),
            new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            new TrustMetadata(null, "unreviewed", Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef(item.ContentHtml, null, null, null),
            route,
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            fields,
            Array.Empty<ContentDiagnostic>());
    }

    private static string? GetFieldString(IReadOnlyDictionary<string, ContentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        var value = field.Value.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
