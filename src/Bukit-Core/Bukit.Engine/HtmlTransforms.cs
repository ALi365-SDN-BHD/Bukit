using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal enum HtmlDocumentKind
{
    Content = 0,
    List = 1,
    Static = 2
}

internal sealed record HtmlTransformContext(
    string RouteUrl,
    string OutputPath,
    HtmlDocumentKind DocumentKind,
    BuildExecutionMode ExecutionMode,
    ILogger Logger,
    PageInfo? Page = null,
    ContentDocument? Document = null);

internal interface IHtmlTransform
{
    string Name { get; }

    string Transform(HtmlTransformContext context, string html);
}

internal sealed class HtmlTransformPipeline
{
    private readonly IReadOnlyList<IHtmlTransform> _transforms;

    internal HtmlTransformPipeline(
        IReadOnlyList<IHtmlTransform> transforms,
        BuildExecutionMode executionMode = BuildExecutionMode.Production)
    {
        _transforms = transforms;
        ExecutionMode = executionMode;
    }

    internal BuildExecutionMode ExecutionMode { get; }

    internal string Transform(HtmlTransformContext context, string html)
    {
        foreach (var transform in _transforms)
        {
            html = transform.Transform(context, html);
        }

        return html;
    }

    internal string Transform(
        RouteInfo route,
        HtmlDocumentKind documentKind,
        PageInfo page,
        ContentDocument? document,
        ILogger logger,
        string html)
        => Transform(new HtmlTransformContext(
            route.Url,
            route.OutputPath,
            documentKind,
            ExecutionMode,
            logger,
            page,
            document), html);
}

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
