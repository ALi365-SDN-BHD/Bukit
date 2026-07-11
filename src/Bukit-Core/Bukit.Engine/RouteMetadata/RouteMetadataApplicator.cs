using Bukit.Rendering;

namespace Bukit.Engine.RouteMetadata;

internal static class RouteMetadataApplicator
{
    internal static PageInfo ApplyToPage(
        PageInfo page,
        string routeUrl,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        ArgumentNullException.ThrowIfNull(page);
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
}
