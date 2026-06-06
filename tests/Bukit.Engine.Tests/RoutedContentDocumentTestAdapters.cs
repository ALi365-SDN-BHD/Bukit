using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Tests;

internal static class RoutedContentDocumentTestAdapters
{
    internal static RoutedContentDocument ToRoutedDocument(this (ContentDocument Item, RouteInfo Route) value)
        => new(value.Item, value.Route);

    internal static IReadOnlyList<RoutedContentDocument> ToRoutedDocuments(this IEnumerable<(ContentDocument Item, RouteInfo Route)> routed)
        => routed.Select(x => x.ToRoutedDocument()).ToArray();

    internal static ContentDocument ToDocument(this ContentDocument item)
        => item;
}
