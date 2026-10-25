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
        return SeoDiagnostics.AnalyzeHtml(config, route, page.Seo, html, logger);
    }
}
