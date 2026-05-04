using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class PageRenderDispatcher
{
    internal sealed record RenderResult(
        int RenderedCount,
        int SkippedCount,
        IReadOnlyDictionary<string, int> RenderReasons,
        BuildStageMetrics StageMetrics);

    internal sealed record SpecialListRenderResult(
        int RenderedCount,
        int SkippedCount,
        BuildStageMetrics StageMetrics);

    internal static async Task<RenderResult> RenderPagesAsync(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> renderQueue,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        string templateHash,
        bool incrementalEnabled,
        BuildManifest manifest,
        ConcurrentDictionary<string, BuildManifestEntry>? manifestEntries,
        HashSet<string> currentKeys,
        int maxDegreeOfParallelism,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var workItems = new List<(ContentItem Item, RouteInfo Route, string Key)>(renderQueue.Count);
        var warnedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (item, route) in renderQueue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            BuildPathUtils.WarnIfWindowsIncompatible(route.OutputPath, warnedOutputPaths, logger);
            currentKeys.Add(key);
            workItems.Add((item, route, key));
        }

        var renderedCount = 0;
        var skippedCount = 0;
        var renderReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stageMetrics = new BuildStageMetricsCollector();

        if (maxDegreeOfParallelism <= 0)
        {
            maxDegreeOfParallelism = Environment.ProcessorCount;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };
        var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(workItems, parallelOptions, async (work, ct) =>
        {
            var item = work.Item;
            var route = work.Route;
            var key = work.Key;
            var metadataHashStopwatch = Stopwatch.StartNew();
            var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
            metadataHashStopwatch.Stop();
            stageMetrics.Increment("metadataHash");
            stageMetrics.AddDuration("metadataHash", metadataHashStopwatch.ElapsedMilliseconds);
            var routeHash = IncrementalBuildEngine.ComputeRouteHash(route);
            var outputPath = Path.Combine(outputDir, route.OutputPath);
            var outputExists = File.Exists(outputPath);

            BuildManifestEntry? existing = null;
            var hasExisting = incrementalEnabled && manifestEntries is not null && manifestEntries.TryGetValue(key, out existing) && existing is not null;

            var canEvaluateSkip = incrementalEnabled &&
                hasExisting &&
                outputExists &&
                existing!.TemplateHash == templateHash &&
                existing.MetadataHash == metadataHash &&
                existing.RouteHash == routeHash;

            string? contentHash = null;
            if (canEvaluateSkip)
            {
                var stableFingerprintStopwatch = Stopwatch.StartNew();
                if (IncrementalBuildEngine.TryComputeStableContentHash(item, bodyStore, metadataHash, out var stableContentHash))
                {
                    stableFingerprintStopwatch.Stop();
                    stageMetrics.Increment("stableContentHash");
                    stageMetrics.AddDuration("stableContentHash", stableFingerprintStopwatch.ElapsedMilliseconds);
                    contentHash = stableContentHash;
                }
                else
                {
                    stableFingerprintStopwatch.Stop();

                    var contentHashStopwatch = Stopwatch.StartNew();
                    contentHash = IncrementalBuildEngine.ComputeContentHash(item, bodyStore);
                    contentHashStopwatch.Stop();
                    stageMetrics.Increment("contentHash");
                    stageMetrics.AddDuration("contentHash", contentHashStopwatch.ElapsedMilliseconds);
                }
            }

            var canSkip = canEvaluateSkip &&
                existing!.ContentHash == contentHash;

            if (canSkip)
            {
                Interlocked.Increment(ref skippedCount);
                renderReasons.AddOrUpdate("unchanged", 1, (_, v) => v + 1);
                return;
            }

            if (incrementalEnabled)
            {
                var reason = !hasExisting ? "new_page"
                    : !outputExists ? "output_missing"
                    : existing!.TemplateHash != templateHash ? "template_changed"
                    : existing.MetadataHash != metadataHash ? "content_changed"
                    : existing.ContentHash != contentHash ? "content_changed"
                    : existing.RouteHash != routeHash ? "route_changed"
                    : "render";
                renderReasons.AddOrUpdate(reason, 1, (_, v) => v + 1);
            }
            else
            {
                renderReasons.AddOrUpdate("full_render", 1, (_, v) => v + 1);
            }

            var bodyLoadStopwatch = Stopwatch.StartNew();
            var content = await ContentBodyResolver.GetHtmlAsync(item, bodyStore, ct);
            bodyLoadStopwatch.Stop();
            stageMetrics.Increment("bodyLoad");
            stageMetrics.AddDuration("bodyLoad", bodyLoadStopwatch.ElapsedMilliseconds);

            var pageModel = new PageModel
            {
                Site = siteModel,
                Page = new PageInfo
                {
                    Title = item.Title,
                    Url = route.Url,
                    Content = content,
                    Summary = item.Meta.TryGetValue("summary", out var summary) ? summary?.ToString() : null,
                    PublishDate = item.PublishAt,
                    Fields = item.Fields
                }
            };

            var pageRenderStopwatch = Stopwatch.StartNew();
            var html = renderer.RenderPage(route.Template, pageModel);
            pageRenderStopwatch.Stop();
            stageMetrics.Increment("pageRender");
            stageMetrics.AddDuration("pageRender", pageRenderStopwatch.ElapsedMilliseconds);
            await WriteUtf8LockedAsync(outputDir, route.OutputPath, html, writeLocks, ct);
            Interlocked.Increment(ref renderedCount);

            if (incrementalEnabled && manifestEntries is not null)
            {
                manifestEntries[key] = new BuildManifestEntry
                {
                    OutputPath = key,
                    Url = route.Url,
                    Template = route.Template,
                    MetadataHash = metadataHash,
                    ContentHash = contentHash ?? IncrementalBuildEngine.ComputeContentHash(item, metadataHash, content),
                    RouteHash = routeHash,
                    TemplateHash = templateHash
                };
            }
        });

        return new RenderResult(
            renderedCount,
            skippedCount,
            new Dictionary<string, int>(renderReasons, StringComparer.OrdinalIgnoreCase),
            stageMetrics.Snapshot());
    }

    internal static async Task<SpecialListRenderResult> RenderSpecialListsAsync(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        string layoutsDir,
        string listPageContentMode,
        string outputDir,
        string templateHash,
        bool incrementalEnabled,
        BuildManifest manifest,
        HashSet<string> currentKeys,
        ConcurrentDictionary<string, int> renderReasons)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var specialLists = BuildSpecialListDefinitions(routed, collections, layoutsDir, listPageContentMode);
        foreach (var x in specialLists)
        {
            currentKeys.Add(BuildPathUtils.NormalizeRelPath(x.Route.OutputPath));
        }
        if (incrementalEnabled)
        {
            var rendered = 0;
            var skipped = 0;
            foreach (var x in specialLists)
            {
                var result = await RenderSpecialListIfNeededAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, templateHash, manifest, renderReasons, x.IncludeContent);
                rendered += result.RenderedCount;
                skipped += result.SkippedCount;
                stageMetrics = MergeCollectors(stageMetrics, result.StageMetrics);
            }

            return new SpecialListRenderResult(rendered, skipped, stageMetrics.Snapshot());
        }

        var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in specialLists)
        {
            var metrics = await RenderSpecialListAlwaysAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, writeLocks, x.IncludeContent);
            stageMetrics = MergeCollectors(stageMetrics, metrics);
        }

        return new SpecialListRenderResult(specialLists.Count, 0, stageMetrics.Snapshot());
    }

    private static async Task<BuildStageMetrics> RenderSpecialListAlwaysAsync(
        RouteInfo listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        ConcurrentDictionary<string, SemaphoreSlim> writeLocks,
        bool includeContent)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var listBuildStopwatch = Stopwatch.StartNew();
        var pageInfos = await BuildPageInfosAsync(source, bodyStore, includeContent, CancellationToken.None, stageMetrics, "listBodyLoad");

        var html = renderer.RenderList(listRoute.Template, new ListPageModel
        {
            Site = siteModel,
            Pages = pageInfos
        });

        listBuildStopwatch.Stop();
        stageMetrics.Increment("listBuild");
        stageMetrics.AddDuration("listBuild", listBuildStopwatch.ElapsedMilliseconds);
        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, html, writeLocks, CancellationToken.None);
        return stageMetrics.Snapshot();
    }

    private static async Task<SpecialListRenderResult> RenderSpecialListIfNeededAsync(
        RouteInfo listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        string templateHash,
        BuildManifest manifest,
        ConcurrentDictionary<string, int> renderReasons,
        bool includeContent)
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
            existing.RouteHash == routeHash;

        if (canSkip)
        {
            renderReasons.AddOrUpdate("list_unchanged", 1, (_, v) => v + 1);
            return new SpecialListRenderResult(0, 1, stageMetrics.Snapshot());
        }

        var listBuildStopwatch = Stopwatch.StartNew();
        var pageInfos = await BuildPageInfosAsync(source, bodyStore, includeContent, CancellationToken.None, stageMetrics, "listBodyLoad");

        var html = renderer.RenderList(listRoute.Template, new ListPageModel
        {
            Site = siteModel,
            Pages = pageInfos
        });

        listBuildStopwatch.Stop();
        stageMetrics.Increment("listBuild");
        stageMetrics.AddDuration("listBuild", listBuildStopwatch.ElapsedMilliseconds);
        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, html, new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase), CancellationToken.None);
        renderReasons.AddOrUpdate("list_render", 1, (_, v) => v + 1);

        manifest.Entries[key] = new BuildManifestEntry
        {
            OutputPath = key,
            Url = listRoute.Url,
            Template = listRoute.Template,
            ContentHash = contentHash,
            RouteHash = routeHash,
            TemplateHash = templateHash
        };

        return new SpecialListRenderResult(1, 0, stageMetrics.Snapshot());
    }

    private static async Task<List<PageInfo>> BuildPageInfosAsync(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        IContentBodyStore bodyStore,
        bool includeContent,
        CancellationToken cancellationToken,
        BuildStageMetricsCollector? stageMetrics = null,
        string bodyLoadMetricName = "listBodyLoad")
    {
        var pageInfos = new List<PageInfo>(source.Count);
        foreach (var entry in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string content = string.Empty;
            if (includeContent)
            {
                var bodyLoadStopwatch = Stopwatch.StartNew();
                content = await ContentBodyResolver.GetHtmlAsync(entry.Item, bodyStore, cancellationToken);
                bodyLoadStopwatch.Stop();
                stageMetrics?.Increment(bodyLoadMetricName);
                stageMetrics?.AddDuration(bodyLoadMetricName, bodyLoadStopwatch.ElapsedMilliseconds);
            }

            pageInfos.Add(new PageInfo
            {
                Title = entry.Item.Title,
                Url = entry.Route.Url,
                Content = content,
                Summary = entry.Item.Meta.TryGetValue("summary", out var summary) ? summary?.ToString() : null,
                PublishDate = entry.Item.PublishAt,
                Fields = entry.Item.Fields
            });
        }

        return pageInfos;
    }

    private static List<SpecialListDefinition> BuildSpecialListDefinitions(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        string layoutsDir,
        string listPageContentMode)
    {
        var list = new List<SpecialListDefinition>();
        var homeRoute = new RouteInfo("/", "index.html", "pages/index.html");
        list.Add(new SpecialListDefinition(
            homeRoute,
            routed.OrderByDescending(x => x.Item.PublishAt).ToList(),
            TemplateCapabilitiesResolver.ShouldIncludeListPageContent(homeRoute.Template, layoutsDir, listPageContentMode)));

        if (collections is null || collections.Count == 0)
        {
            AddLegacyList("/blog/");
            AddLegacyList("/pages/");
            return list;
        }

        foreach (var (key, collection) in collections)
        {
            if (string.IsNullOrWhiteSpace(collection.ListRoute))
            {
                continue;
            }

            var url = NormalizeListUrl(collection.ListRoute);
            var outputPath = BuildListOutputPath(url);
            var route = new RouteInfo(url, outputPath, "pages/list.html");
            var items = routed
                .Where(x => string.Equals(GetCollection(x.Item), key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Item.PublishAt)
                .ToList();
            list.Add(new SpecialListDefinition(
                route,
                items,
                TemplateCapabilitiesResolver.ShouldIncludeListPageContent(route.Template, layoutsDir, listPageContentMode)));
        }

        return list;

        void AddLegacyList(string url)
        {
            var route = new RouteInfo(url, BuildListOutputPath(url), "pages/list.html");
            var items = routed.Where(x => x.Route.Url.StartsWith(url, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Item.PublishAt).ToList();
            list.Add(new SpecialListDefinition(
                route,
                items,
                TemplateCapabilitiesResolver.ShouldIncludeListPageContent(route.Template, layoutsDir, listPageContentMode)));
        }
    }

    private static string NormalizeListUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (!trimmed.EndsWith('/'))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    private static string BuildListOutputPath(string listUrl)
    {
        var normalized = NormalizeListUrl(listUrl).Trim('/');
        return string.IsNullOrWhiteSpace(normalized)
            ? "index.html"
            : Path.Combine(normalized.Replace('/', Path.DirectorySeparatorChar), "index.html");
    }

    private static string GetCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        if (item.Meta.TryGetValue("type", out var type) && type is not null && !string.IsNullOrWhiteSpace(type.ToString()))
        {
            return type.ToString()!;
        }

        return "page";
    }

    private sealed record SpecialListDefinition(
        RouteInfo Route,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> Items,
        bool IncludeContent);

    private static BuildStageMetricsCollector MergeCollectors(BuildStageMetricsCollector collector, BuildStageMetrics metrics)
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

    private static async Task WriteUtf8LockedAsync(
        string outputRoot,
        string relativePath,
        string html,
        ConcurrentDictionary<string, SemaphoreSlim> writeLocks,
        CancellationToken cancellationToken)
    {
        var gate = writeLocks.GetOrAdd(relativePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            FileWriter.WriteUtf8(outputRoot, relativePath, html);
        }
        finally
        {
            gate.Release();
        }
    }
}
