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
    internal static async Task<BuildStageMetrics> RenderSpecialListAlwaysAsync(
        RouteInfo listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        ConcurrentDictionary<string, SemaphoreSlim> writeLocks,
        int maxDegreeOfParallelism,
        bool includeContent,
        CancellationToken cancellationToken,
        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder,
        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var listBuildStopwatch = Stopwatch.StartNew();
        var pageInfos = await BuildPageInfosAsync(source, bodyStore, includeContent, maxDegreeOfParallelism, cancellationToken, stageMetrics, "listBodyLoad", seoBuilder);
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
        var contentHash = IncrementalBuildEngine.ComputeListContentHash(templateHash, listRoute.Template, source, manifest, bodyStore, includeContent);
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
        var pageInfos = await BuildPageInfosAsync(source, bodyStore, includeContent, maxDegreeOfParallelism, cancellationToken, stageMetrics, "listBodyLoad", seoBuilder);
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
        await PageRenderDispatcher.WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, html, new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase), cancellationToken);
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
                pageInfos[i] = new PageInfo
                {
                    Title = source[i].Item.Title,
                    Url = source[i].Route.Url,
                    Content = string.Empty,
                    Summary = source[i].Item.Meta.TryGetValue("summary", out var summary) ? summary?.ToString() : null,
                    TableOfContents = GetTableOfContents(source[i].Item),
                    PublishDate = source[i].Item.PublishAt,
                    Fields = source[i].Item.Fields,
                    Seo = seoBuilder?.Invoke(source[i].Item, source[i].Route)
                };
            }

            return new List<PageInfo>(pageInfos);
        }

        var metricsLock = new object();
        await Parallel.ForEachAsync(
            source.Select((entry, i) => (entry, i)),
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount, CancellationToken = cancellationToken },
            async (work, ct) =>
            {
                var (entry, i) = work;
                var bodyLoadStopwatch = Stopwatch.StartNew();
                var content = await ContentBodyResolver.GetHtmlAsync(entry.Item, bodyStore, ct);
                bodyLoadStopwatch.Stop();
                if (stageMetrics is not null)
                {
                    lock (metricsLock)
                    {
                        stageMetrics.Increment(bodyLoadMetricName);
                        stageMetrics.AddDuration(bodyLoadMetricName, bodyLoadStopwatch.ElapsedMilliseconds);
                    }
                }

                pageInfos[i] = new PageInfo
                {
                    Title = entry.Item.Title,
                    Url = entry.Route.Url,
                    Content = content,
                    Summary = entry.Item.Meta.TryGetValue("summary", out var summary) ? summary?.ToString() : null,
                    TableOfContents = GetTableOfContents(entry.Item),
                    PublishDate = entry.Item.PublishAt,
                    Fields = entry.Item.Fields,
                    Seo = seoBuilder?.Invoke(entry.Item, entry.Route)
                };
            });

        return new List<PageInfo>(pageInfos);
    }

    internal static IReadOnlyList<TableOfContentsEntry>? GetTableOfContents(ContentItem item)
        => item.Meta.TryGetValue("tableOfContents", out var toc) && toc is IReadOnlyList<TableOfContentsEntry> entries
            ? entries
            : null;

    internal static PageInfo CreateListPageInfo(SiteModel siteModel, RouteInfo listRoute)
    {
        return new PageInfo
        {
            Title = listRoute.Url == "/" ? siteModel.Title : BuildListTitle(listRoute.Url),
            Url = listRoute.Url,
            Content = string.Empty,
            Summary = BuildListSummary(siteModel, listRoute)
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
