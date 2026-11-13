using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;

namespace Bukit.Engine;

/// <summary>Builds the one derived redirect document shape used by aliases and legacy home pages.</summary>
internal static class RedirectPageDocument
{
    internal static RoutedContentDocument Create(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        string targetUrl,
        string routeUrl,
        string outputPath)
    {
        var document = DerivedContentDocumentFactory.Create(
            id: id,
            title: title,
            slug: slug,
            publishAt: publishAt,
            body: new ContentBodyRef(Html: RedirectPageHtml.Build(targetUrl)),
            customFields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "redirect",
                ["collection"] = "redirect",
                ["excludeFromSitemap"] = true,
                ["excludeFromSearch"] = true,
                ["excludeFromFeed"] = true,
                ["robots"] = "noindex"
            }));
        var route = new RouteInfo(routeUrl, outputPath, RedirectPageHtml.RouteTemplate);
        return new RoutedContentDocument(document, route, document.PublishAt);
    }
}
