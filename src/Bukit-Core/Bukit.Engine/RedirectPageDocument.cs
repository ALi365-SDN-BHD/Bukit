using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;

namespace Bukit.Engine;

/// <summary>Builds the one derived redirect document shape used by aliases and legacy home pages.</summary>
internal static class RedirectPageDocument
{
    /// <summary>Custom field holding the site-relative route URL an alias redirect points to.</summary>
    internal const string RedirectTargetField = "redirectTarget";

    internal static RoutedContentDocument Create(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        string targetUrl,
        string routeUrl,
        string outputPath,
        string? redirectTarget = null)
    {
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "redirect",
            ["collection"] = "redirect",
            ["excludeFromSitemap"] = true,
            ["excludeFromSearch"] = true,
            ["excludeFromFeed"] = true,
            ["robots"] = "noindex"
        };
        if (redirectTarget is not null)
        {
            fields[RedirectTargetField] = redirectTarget;
        }

        var document = DerivedContentDocumentFactory.Create(
            id: id,
            title: title,
            slug: slug,
            publishAt: publishAt,
            body: new ContentBodyRef(Html: RedirectPageHtml.Build(targetUrl)),
            customFields: ContentFieldReader.ToFieldMap(fields));
        var route = new RouteInfo(routeUrl, outputPath, RedirectPageHtml.RouteTemplate);
        return new RoutedContentDocument(document, route, document.PublishAt);
    }
}
