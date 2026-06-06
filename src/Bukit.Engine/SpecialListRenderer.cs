using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class SpecialListRenderer
{
    internal static int ComputeNestedDegreeOfParallelism(int outerCount, int requestedMDoP)
    {
        if (outerCount > 1) return 1;
        return requestedMDoP > 0 ? requestedMDoP : Environment.ProcessorCount;
    }

    internal static async Task<BuildStageMetrics> RenderSpecialListAlwaysAsync(
        RouteInfo listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        ConcurrentDictionary<string, SemaphoreSlim> writeLocks,
        int maxDegreeOfParallelism,
        int outerCount,
        bool includeContent,
        CancellationToken cancellationToken,
        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder,
        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var listBuildStopwatch = Stopwatch.StartNew();
        var pageInfos = await BuildPageInfosAsync(source, bodyStore, includeContent, maxDegreeOfParallelism, outerCount, cancellationToken, stageMetrics, "listBodyLoad", seoBuilder);
        var listPage = CreateListPageInfo(siteModel, listRoute);
        listPage = listPage with { Seo = listSeoBuilder?.Invoke(listRoute, listPage) };

        var html = renderer.RenderList(listRoute.Template, new ListPageModel
        {
            Site = siteModel,
            Page = listPage,
            Pages = pageInfos
        });
        if (listHtmlPostProcessor is not null)
        {
            html = listHtmlPostProcessor(listRoute, listPage, html);
        }

        listBuildStopwatch.Stop();
        stageMetrics.Increment("listBuild");
        stageMetrics.AddDuration("listBuild", listBuildStopwatch.ElapsedMilliseconds);
        await PageRenderDispatcher.WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, html, writeLocks, cancellationToken);
        return stageMetrics.Snapshot();
    }

    internal static async Task<PageRenderDispatcher.SpecialListRenderResult> RenderSpecialListIfNeededAsync(
        RouteInfo listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        string templateHash,
        string renderDependencyHash,
        BuildManifest manifest,
        ConcurrentDictionary<string, int> renderReasons,
        int maxDegreeOfParallelism,
        int outerCount,
        bool includeContent,
        CancellationToken cancellationToken,
        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder,
        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var key = BuildPathUtils.NormalizeRelPath(listRoute.OutputPath);
        var routeHash = IncrementalBuildEngine.ComputeRouteHash(listRoute);
        var listHashStopwatch = Stopwatch.StartNew();
#pragma warning disable CS0618
        var contentHash = IncrementalBuildEngine.ComputeListContentHash(templateHash, listRoute.Template, source, manifest, bodyStore, includeContent);
#pragma warning restore CS0618
        listHashStopwatch.Stop();
        stageMetrics.Increment("listHash");
        stageMetrics.AddDuration("listHash", listHashStopwatch.ElapsedMilliseconds);
        var outputPath = Path.Combine(outputDir, listRoute.OutputPath);
        var outputExists = File.Exists(outputPath);
        var hasExisting = manifest.Entries.TryGetValue(key, out var existing) && existing is not null;

        var canSkip = hasExisting &&
            outputExists &&
            existing!.TemplateHash == templateHash &&
            existing.ContentHash == contentHash &&
            existing.RouteHash == routeHash &&
            existing.RenderDependencyHash == renderDependencyHash;

        if (canSkip)
        {
            renderReasons.AddOrUpdate("list_unchanged", 1, (_, v) => v + 1);
            return new PageRenderDispatcher.SpecialListRenderResult(0, 1, stageMetrics.Snapshot());
        }

        var listBuildStopwatch = Stopwatch.StartNew();
        var pageInfos = await BuildPageInfosAsync(source, bodyStore, includeContent, maxDegreeOfParallelism, outerCount, cancellationToken, stageMetrics, "listBodyLoad", seoBuilder);
        var listPage = CreateListPageInfo(siteModel, listRoute);
        listPage = listPage with { Seo = listSeoBuilder?.Invoke(listRoute, listPage) };

        var html = renderer.RenderList(listRoute.Template, new ListPageModel
        {
            Site = siteModel,
            Page = listPage,
            Pages = pageInfos
        });
        if (listHtmlPostProcessor is not null)
        {
            html = listHtmlPostProcessor(listRoute, listPage, html);
        }

        listBuildStopwatch.Stop();
        stageMetrics.Increment("listBuild");
        stageMetrics.AddDuration("listBuild", listBuildStopwatch.ElapsedMilliseconds);
        FileWriter.WriteUtf8(outputDir, listRoute.OutputPath, html);
        renderReasons.AddOrUpdate("list_render", 1, (_, v) => v + 1);

        lock (manifest)
        {
            manifest.Entries[key] = new BuildManifestEntry
            {
                OutputPath = key,
                Url = listRoute.Url,
                Template = listRoute.Template,
                ContentHash = contentHash,
                RouteHash = routeHash,
                TemplateHash = templateHash,
                RenderDependencyHash = renderDependencyHash
            };
        }

        return new PageRenderDispatcher.SpecialListRenderResult(1, 0, stageMetrics.Snapshot());
    }

    internal static async Task<List<PageInfo>> BuildPageInfosAsync(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        IContentBodyStore bodyStore,
        bool includeContent,
        int maxDegreeOfParallelism,
        int outerCount,
        CancellationToken cancellationToken,
        BuildStageMetricsCollector? stageMetrics = null,
        string bodyLoadMetricName = "listBodyLoad",
        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder = null)
    {
        var pageInfos = new PageInfo[source.Count];

        if (!includeContent)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var contentRecord = CanonicalContentGraphBuilder.ToRecord(source[i].Item);
                pageInfos[i] = new PageInfo
                {
                    Title = source[i].Item.Title,
                    Url = source[i].Route.Url,
                    Content = string.Empty,
                    Summary = contentRecord.Presentation.Summary ?? ContentFieldReader.GetSummary(source[i].Item.Fields),
                    TableOfContents = GetTableOfContents(source[i].Item),
                    PublishDate = source[i].Item.PublishAt,
                    Fields = source[i].Item.Fields,
                    ContentRecord = contentRecord,
                    Entities = contentRecord.Entities,
                    Provenance = contentRecord.Provenance,
                    Trust = contentRecord.Trust,
                    Representations = PublishRepresentationRegistry.DocumentKinds(),
                    Seo = seoBuilder?.Invoke(source[i].Item, source[i].Route)
                };
            }

            return new List<PageInfo>(pageInfos);
        }

        var effectiveMDoP = ComputeNestedDegreeOfParallelism(outerCount, maxDegreeOfParallelism);
        var metricsLock = new object();
        await Parallel.ForAsync(0, source.Count,
            new ParallelOptions { MaxDegreeOfParallelism = effectiveMDoP, CancellationToken = cancellationToken },
            async (i, ct) =>
            {
                var bodyLoadStopwatch = Stopwatch.StartNew();
                var content = await ContentBodyResolver.GetHtmlAsync(source[i].Item, bodyStore, ct);
                bodyLoadStopwatch.Stop();
                if (stageMetrics is not null)
                {
                    lock (metricsLock)
                    {
                        stageMetrics.Increment(bodyLoadMetricName);
                        stageMetrics.AddDuration(bodyLoadMetricName, bodyLoadStopwatch.ElapsedMilliseconds);
                    }
                }

                var contentRecord = CanonicalContentGraphBuilder.ToRecord(source[i].Item);
                pageInfos[i] = new PageInfo
                {
                    Title = source[i].Item.Title,
                    Url = source[i].Route.Url,
                    Content = content,
                    Summary = contentRecord.Presentation.Summary ?? ContentFieldReader.GetSummary(source[i].Item.Fields),
                    TableOfContents = GetTableOfContents(source[i].Item),
                    PublishDate = source[i].Item.PublishAt,
                    Fields = source[i].Item.Fields,
                    ContentRecord = contentRecord,
                    Entities = contentRecord.Entities,
                    Provenance = contentRecord.Provenance,
                    Trust = contentRecord.Trust,
                    Representations = PublishRepresentationRegistry.DocumentKinds(),
                    Seo = seoBuilder?.Invoke(source[i].Item, source[i].Route)
                };
            });

        return new List<PageInfo>(pageInfos);
    }

    internal static IReadOnlyList<TableOfContentsEntry>? GetTableOfContents(ContentItem item)
        => item.Fields is not null &&
           item.Fields.TryGetValue("tableOfContents", out var toc) &&
           toc.Value is IReadOnlyList<TableOfContentsEntry> entries
            ? entries
            : null;

    internal static PageInfo CreateListPageInfo(SiteModel siteModel, RouteInfo listRoute)
    {
        return new PageInfo
        {
            Title = listRoute.Url == "/" ? siteModel.Title : BuildListTitle(listRoute.Url),
            Url = listRoute.Url,
            Content = string.Empty,
            Summary = BuildListSummary(siteModel, listRoute),
            Representations = PublishRepresentationRegistry.DocumentKinds()
        };
    }

    internal static string BuildListSummary(SiteModel siteModel, RouteInfo listRoute)
    {
        if (!string.IsNullOrWhiteSpace(siteModel.Description) && listRoute.Url == "/")
        {
            return siteModel.Description!;
        }

        var siteTitle = string.IsNullOrWhiteSpace(siteModel.Title) ? siteModel.Name : siteModel.Title;
        if (listRoute.Url == "/")
        {
            return $"Browse the latest content from {siteTitle}.";
        }

        return $"Browse {BuildListTitle(listRoute.Url)} from {siteTitle}.";
    }

    internal static string BuildListTitle(string url)
    {
        var lastSegment = (url ?? string.Empty)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastSegment))
        {
            return "Index";
        }

        return char.ToUpperInvariant(lastSegment[0]) + lastSegment[1..].Replace('-', ' ');
    }

    internal static BuildStageMetricsCollector MergeCollectors(BuildStageMetricsCollector collector, BuildStageMetrics metrics)
    {
        foreach (var kv in metrics.DurationsMs)
        {
            collector.AddDuration(kv.Key, kv.Value);
        }

        foreach (var kv in metrics.Counts)
        {
            collector.Increment(kv.Key, kv.Value);
        }

        return collector;
    }
}
