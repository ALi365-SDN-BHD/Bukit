using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
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
        var routeResult = new RoutePipeline().Execute(
            context.Config,
            context.Documents,
            templateResolver);
        routeGenerationStopwatch.Stop();
        metrics.AddDuration("routeGeneration", routeGenerationStopwatch.ElapsedMilliseconds);

        var pluginContext = new BuildContext
        {
            Config = context.Config,
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            LayoutsDir = context.LayoutsDir,
            RoutedDocuments = routeResult.RoutedDocuments,
            StaticHtmlRoutes = Array.Empty<RouteInfo>(),
            ContentGraph = context.ContentGraph,
            BodyStore = context.BodyStore,
            TemplateResolver = templateResolver.ResolveKindTemplate,
            Logger = logger
        };
        pluginContext.Data[ListRouteGraphBuilder.BuildContextDataKey] = routeResult.ListRouteGraph;

        var taxonomyStopwatch = Stopwatch.StartNew();
        TaxonomyTermsInjector.InjectFromDataDocuments(pluginContext, dataDocuments);
        await TaxonomyTermsInjector.InjectFromNotionDatabaseOptionsAsync(pluginContext, cancellationToken);
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
}
