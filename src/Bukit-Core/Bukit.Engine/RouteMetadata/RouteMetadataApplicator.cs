using Bukit.Rendering;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.RouteMetadata;

internal static class RouteMetadataApplicator
{
    internal static PageInfo ApplyToPage(
        PageInfo page,
        string routeUrl,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata,
        ContentDocument? document = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (document is not null && !CanApplyToContent(document))
        {
            return page;
        }

        if (routeMetadata is null || !routeMetadata.TryGetValue(routeUrl, out var metadata))
        {
            return page;
        }

        return page with
        {
            Title = metadata.Title,
            Summary = metadata.Summary
        };
    }

    internal static RouteMetadataEntry? Find(
        string routeUrl,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
        => routeMetadata is not null && routeMetadata.TryGetValue(routeUrl, out var metadata)
            ? metadata
            : null;

    internal static RouteMetadataEntry? FindForContent(
        ContentDocument document,
        string routeUrl,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CanApplyToContent(document) ? Find(routeUrl, routeMetadata) : null;
    }

    internal static string? ResolveDependencyRouteUrl(ContentDocument? document, string routeUrl)
        => document is null || CanApplyToContent(document) ? routeUrl : null;

    internal static bool CanApplyToContent(ContentDocument document)
    {
        var contentType = document.Record.Identity.ContentType;
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            return IsSingletonKind(contentType);
        }

        var legacyType = ContentFieldReader.GetContentType(document);
        if (!string.IsNullOrWhiteSpace(legacyType))
        {
            return IsSingletonKind(legacyType);
        }

        var collection = document.Record.Classification.Collection;
        return !string.IsNullOrWhiteSpace(collection)
            ? IsSingletonKind(collection)
            : IsSingletonKind(ContentFieldReader.GetCollection(document));
    }

    private static bool IsSingletonKind(string? value)
        => string.Equals(value?.Trim(), "page", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value?.Trim(), "singleton", StringComparison.OrdinalIgnoreCase);
}
