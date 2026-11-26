using Bukit.Theme;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.DerivedFields;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record BuildRoutePipelineResult(
    RoutePipelineResult RouteResult,
    IReadOnlyList<RouteInfo> StaticHtmlRoutes,
    IReadOnlyList<RenderEntry>? StaticEntries,
    BuildContext PluginContext);

internal static class VariantRouteStage
{
    internal static async Task<BuildRoutePipelineResult> ExecuteAsync(
        BuildVariantContext context,
        IReadOnlyList<ContentDocument> dataDocuments,
        ThemeTemplateResolver templateResolver,
        ILogger logger,
        BuildStageMetricsCollector metrics,
        CancellationToken cancellationToken)
    {
        var routeGenerationStopwatch = Stopwatch.StartNew();
        // The variant documents are one language; the content graph holds the whole site.
        var routeResult = new RoutePipeline { SiteDocuments = context.ContentGraph.Documents }.Execute(
            context.Config,
            context.Documents,
            templateResolver);
        routeGenerationStopwatch.Stop();
        DerivedFieldResolver.Report(context.Config, context.Documents, routeResult.DerivedFieldIssues, logger);
        WarnDecodedRouteUrls(routeResult.RoutedDocuments, logger);
        metrics.AddDuration("routeGeneration", routeGenerationStopwatch.ElapsedMilliseconds);

        var pluginContext = new BuildContext
        {
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            LayoutsDir = context.LayoutsDir,
            RoutedDocuments = routeResult.RoutedDocuments,
            StaticHtmlRoutes = Array.Empty<RouteInfo>(),
            ContentGraph = DerivedFieldResolver.ProjectRecords(context.ContentGraph, routeResult.RoutedDocuments, routeResult.DerivedKeys),
            BodyStore = context.BodyStore,
            TemplateResolver = templateResolver.ResolveKindTemplate,
            Logger = logger
        };
        SetSiteProjections(pluginContext, routeResult, context.Config);
        pluginContext.Set(BuildDataKeys.MediaDownloadDir, ContentProviderFactory
            .BuildEffectiveMediaConfig(context.Config.Content.Media, context.RootDir, context.MediaDownloadDir)
            .DownloadDir);

        var taxonomyStopwatch = Stopwatch.StartNew();
        TaxonomyTermsInjector.InjectFromDataDocuments(pluginContext, dataDocuments);
        await TaxonomyTermsInjector.InjectFromNotionDatabaseOptionsAsync(
            pluginContext,
            context.Config,
            cancellationToken);
        taxonomyStopwatch.Stop();
        metrics.AddDuration("taxonomySetup", taxonomyStopwatch.ElapsedMilliseconds);

        var hasStaticDir = Directory.Exists(context.StaticDir);
        var staticRouteTemplate = !string.IsNullOrWhiteSpace(context.Config.Theme.StaticTemplate)
            ? context.Config.Theme.StaticTemplate
            : null;
        IReadOnlyList<RenderEntry>? staticEntries = null;
        IReadOnlyList<RouteInfo> staticHtmlRoutes = Array.Empty<RouteInfo>();
        if (hasStaticDir && staticRouteTemplate is not null)
        {
            staticEntries = RenderEntry.ForStaticDir(
                context.StaticDir,
                staticRouteTemplate,
                message => logger.Warn(message),
                context.Config.Build.PublishDotFiles);
            staticHtmlRoutes = staticEntries.Select(entry => entry.Route).ToList();
        }
        else if (hasStaticDir &&
                 SafeFileEnumerator.EnumerateFiles(context.StaticDir, "*.html").Any())
        {
            logger.Warn("Static HTML files in static dir are skipped because no static template is configured (theme.staticTemplate).");
        }

        pluginContext.StaticHtmlRoutes = staticHtmlRoutes;
        RouteInventoryValidator.ValidateFinalRoutes(
            routeResult.RoutedDocuments,
            pluginContext.DerivedDocuments,
            routeResult.ListRoutes,
            staticHtmlRoutes);

        return new BuildRoutePipelineResult(
            routeResult,
            staticHtmlRoutes,
            staticEntries,
            pluginContext);
    }

    /// <summary>The route results templates read through <c>site</c>: list routes, queries and enum labels.</summary>
    private static void SetSiteProjections(BuildContext pluginContext, RoutePipelineResult routeResult, AppConfig config)
    {
        pluginContext.Set(BuildDataKeys.ListRouteGraph, routeResult.ListRouteGraph);
        if (routeResult.Queries is { } queries)
        {
            pluginContext.Set(BuildDataKeys.Queries, queries);
        }

        if (FieldEnumProjection.Build(config.Content.ModelSchema) is { } fieldEnums)
        {
            pluginContext.Set(BuildDataKeys.FieldEnums, fieldEnums);
        }
    }

    private static void WarnDecodedRouteUrls(IReadOnlyList<RoutedContentDocument> routedDocuments, ILogger logger)
    {
        foreach (var routed in routedDocuments)
        {
            var authored = RouteGenerator.GetAuthoredRouteUrl(routed.Document);
            if (string.IsNullOrWhiteSpace(authored) || !authored.Contains('%', StringComparison.Ordinal))
            {
                continue;
            }

            var decoded = RoutePathBuilder.DecodePercentEncodedUrl(authored.Trim());
            if (!string.Equals(decoded, authored.Trim(), StringComparison.Ordinal))
            {
                logger.Warn($"event=route.url.decoded id={routed.Document.Id} raw=\"{authored.Trim()}\" normalized=\"{decoded}\" hint=fix_url_in_source");
            }
        }
    }
}
