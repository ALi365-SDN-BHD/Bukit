using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class SeoHtmlTransform(
    AppConfig config,
    bool shouldInjectSeo,
    ILogger logger) : IHtmlTransform
{
    public string Name => "seo";

    public string Transform(HtmlTransformContext context, string html)
    {
        var page = context.Page;
        if (page is null)
        {
            return html;
        }

        var skipSeo = context.Document is not null &&
                      SeoInjectionPolicy.ShouldSkip(context.Document.CustomFields);
        if (shouldInjectSeo && !skipSeo)
        {
            html = SeoHtmlRenderer.InjectIntoHead(html, page.Seo);
        }

        var route = new RouteInfo(context.RouteUrl, context.OutputPath, string.Empty);
        if (!skipSeo && ShouldLinkRepresentations(context, page.Seo))
        {
            var urls = ContentProjectionPaths.GetRepresentationUrls(route, config.Site.BaseUrl);
            html = SeoHtmlRenderer.InjectRepresentationLinks(html, urls["markdown"], urls["json"], config.Site.Url);
        }

        return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, logger);
    }

    // Every routed content document gets content/<output path>.md and .json projections.
    // Only inject mode adds the links: in theme and off modes the theme owns the head.
    // Noindex and unpublished pages are not advertised. Expiry is ignored on purpose: page HTML
    // and its .md/.json versions do not depend on the build clock (only aggregates such as the
    // sitemap and agent-manifest.json drop expired pages), so incremental builds match clean ones.
    private bool ShouldLinkRepresentations(HtmlTransformContext context, SeoModel? seo)
        => shouldInjectSeo &&
           config.Site.Seo.Geo.Enabled &&
           context.DocumentKind == HtmlDocumentKind.Content &&
           context.Document is not null &&
           SeoIndexBuilder.IsIndexableIgnoringExpiry(context.Document, seo?.Robots);
}
