using System.Collections.Concurrent;
using System.Diagnostics;
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

    internal sealed record DispatchResult(
        int RenderedCount,
        int SkippedCount,
        IReadOnlyDictionary<string, int> RenderReasons,
        BuildStageMetrics StageMetrics);

    internal static async Task<DispatchResult> DispatchAsync(
        IReadOnlyList<RenderEntry> entries,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        string templateHash,
        string renderDependencyHash,
        bool incrementalEnabled,
        BuildManifest manifest,
        ConcurrentDictionary<string, BuildManifestEntry>? manifestEntries,
        ConcurrentDictionary<string, byte> currentKeys,
        int maxDegreeOfParallelism,
        ILogger logger,
        CancellationToken cancellationToken,
        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder = null,
        Func<ContentItem, RouteInfo, PageInfo, string, string>? htmlPostProcessor = null,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = null,
        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor = null)
    {
        var renderedCount = 0;
        var skippedCount = 0;
        var renderReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stageMetrics = new BuildStageMetricsCollector();
        var stageMetricsLock = new object();

        if (maxDegreeOfParallelism <= 0) maxDegreeOfParallelism = Environment.ProcessorCount;
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken };
        var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            currentKeys.TryAdd(BuildPathUtils.NormalizeRelPath(entry.Route.OutputPath), 0);
        }

        var needsIncrementalMode = incrementalEnabled && manifestEntries is not null;

        await Parallel.ForEachAsync(entries, parallelOptions, async (entry, ct) =>
        {
            switch (entry.Kind)
            {
                case RenderEntryKind.Page:
                    {
                        var item = entry.Item!;
                        var route = entry.Route;
                        var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
                        var mh = IncrementalBuildEngine.ComputeMetadataHash(item);
                        var rh = IncrementalBuildEngine.ComputeRouteHash(route);
                        var outputPath = Path.Combine(outputDir, route.OutputPath);
                        var outputExists = File.Exists(outputPath);

                        BuildManifestEntry? existing = null;
                        var hasExisting = needsIncrementalMode && manifestEntries!.TryGetValue(key, out existing) && existing is not null;

                        var canEvaluateSkip = incrementalEnabled && hasExisting && outputExists &&
                            existing!.TemplateHash == templateHash && existing.MetadataHash == mh &&
                            existing.RouteHash == rh && existing.RenderDependencyHash == renderDependencyHash;

                        string? contentHash = null;
                        if (canEvaluateSkip)
                        {
                            if (IncrementalBuildEngine.TryComputeStableContentHash(item, bodyStore, mh, out var sch))
                                contentHash = sch;
                            else
                                contentHash = IncrementalBuildEngine.ComputeContentHash(item, bodyStore);
                        }

                        var canSkip = canEvaluateSkip && existing!.ContentHash == contentHash;
                        if (canSkip)
                        {
                            Interlocked.Increment(ref skippedCount);
                            renderReasons.AddOrUpdate("unchanged", 1, (_, v) => v + 1);
                            return;
                        }

                        if (incrementalEnabled)
                        {
                            var reason = !hasExisting ? "new_page" : !outputExists ? "output_missing"
                                : existing!.TemplateHash != templateHash ? "template_changed"
                                : existing.MetadataHash != mh ? "content_changed"
                                : existing.ContentHash != contentHash ? "content_changed"
                                : existing.RouteHash != rh ? "route_changed"
                                : existing.RenderDependencyHash != renderDependencyHash ? "render_dependency_changed" : "render";
                            renderReasons.AddOrUpdate(reason, 1, (_, v) => v + 1);
                        }
                        else
                        {
                            renderReasons.AddOrUpdate("full_render", 1, (_, v) => v + 1);
                        }

                        var content = await ContentBodyResolver.GetHtmlAsync(item, bodyStore, ct);
                        var pageInfo = new PageInfo
                        {
                            Title = item.Title,
                            Url = route.Url,
                            Content = content,
                            Summary = item.Meta.TryGetValue("summary", out var s) ? s?.ToString() : null,
                            TableOfContents = GetTableOfContents(item),
                            PublishDate = item.PublishAt,
                            Fields = item.Fields,
                            Seo = seoBuilder?.Invoke(item, route)
                        };
                        var pageModel = new PageModel { Site = siteModel, Page = pageInfo };
                        var html = renderer.RenderPage(route.Template, pageModel);
                        if (htmlPostProcessor is not null) html = htmlPostProcessor(item, route, pageInfo, html);
                        await WriteUtf8LockedAsync(outputDir, route.OutputPath, html, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        lock (stageMetricsLock) { stageMetrics.Increment("pageRender"); stageMetrics.AddDuration("pageRender", 0); }
                        if (needsIncrementalMode) manifestEntries![key] = new BuildManifestEntry { OutputPath = key, Url = route.Url, Template = route.Template, MetadataHash = mh, ContentHash = contentHash ?? IncrementalBuildEngine.ComputeContentHash(item, mh, content), RouteHash = rh, TemplateHash = templateHash, RenderDependencyHash = renderDependencyHash };
                        break;
                    }

                case RenderEntryKind.List:
                    {
                        var listRoute = entry.Route;
                        var source = entry.SourceItems!;
                        var includeContent = entry.IncludeContent;
                        var key = BuildPathUtils.NormalizeRelPath(listRoute.OutputPath);
                        var rh = IncrementalBuildEngine.ComputeRouteHash(listRoute);
                        var ch = IncrementalBuildEngine.ComputeListContentHash(templateHash, listRoute.Template, source, manifest, bodyStore, includeContent);
                        var outputPath = Path.Combine(outputDir, listRoute.OutputPath);
                        var outputExists = File.Exists(outputPath);
                        manifest.Entries.TryGetValue(key, out var le);
                        var hasExisting = incrementalEnabled && le is not null;

                        var canSkip = incrementalEnabled && hasExisting && outputExists &&
                            le!.TemplateHash == templateHash && le.ContentHash == ch &&
                            le.RouteHash == rh && le.RenderDependencyHash == renderDependencyHash;

                        if (canSkip)
                        {
                            Interlocked.Increment(ref skippedCount);
                            renderReasons.AddOrUpdate("list_unchanged", 1, (_, v) => v + 1);
                            return;
                        }

                        var pageInfos = await BuildPageInfosAsync(source, bodyStore, includeContent, maxDegreeOfParallelism, ct, stageMetrics, "listBodyLoad", seoBuilder);
                        var listPage = CreateListPageInfo(siteModel, listRoute);
                        listPage = listPage with { Seo = listSeoBuilder?.Invoke(listRoute, listPage) };
                        var listModel = new ListPageModel { Site = siteModel, Page = listPage, Pages = pageInfos };
                        var listHtml = renderer.RenderList(listRoute.Template, listModel);
                        if (listHtmlPostProcessor is not null) listHtml = listHtmlPostProcessor(listRoute, listPage, listHtml);
                        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, listHtml, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        renderReasons.AddOrUpdate("list_render", 1, (_, v) => v + 1);
                        lock (stageMetricsLock) { stageMetrics.Increment("listBuild"); stageMetrics.AddDuration("listBuild", 0); }

                        if (needsIncrementalMode)
                        {
                            manifestEntries![key] = new BuildManifestEntry { OutputPath = key, Url = listRoute.Url, Template = listRoute.Template, ContentHash = ch, RouteHash = rh, TemplateHash = templateHash, RenderDependencyHash = renderDependencyHash };
                        }
                        break;
                    }

                case RenderEntryKind.Static:
                    {
                        var route = entry.Route;
                        var pageInfo = new PageInfo
                        {
                            Title = entry.Title,
                            Url = route.Url,
                            Content = entry.RawContent ?? string.Empty,
                            Summary = siteModel.Description
                        };
                        var pageModel = new PageModel { Site = siteModel, Page = pageInfo };
                        var staticHtml = renderer.RenderPage(route.Template, pageModel);
                        await WriteUtf8LockedAsync(outputDir, route.OutputPath, staticHtml, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        lock (stageMetricsLock) { stageMetrics.Increment("staticRender"); stageMetrics.AddDuration("staticRender", 0); }
                        break;
                    }
            }
        });

        return new DispatchResult(renderedCount, skippedCount,
            new Dictionary<string, int>(renderReasons, StringComparer.OrdinalIgnoreCase),
            stageMetrics.Snapshot());
    }

    internal static async Task<RenderResult> RenderPagesAsync(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> renderQueue,
        IContentBodyStore bodyStore,
        ITemplateRenderer renderer,
        SiteModel siteModel,
        string outputDir,
        string templateHash,
        string renderDependencyHash,
        bool incrementalEnabled,
        BuildManifest manifest,
        ConcurrentDictionary<string, BuildManifestEntry>? manifestEntries,
        ConcurrentDictionary<string, byte> currentKeys,
        int maxDegreeOfParallelism,
        ILogger logger,
        CancellationToken cancellationToken,
        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder = null,
        Func<ContentItem, RouteInfo, PageInfo, string, string>? htmlPostProcessor = null)
    {
        var workItems = new List<(ContentItem Item, RouteInfo Route, string Key)>(renderQueue.Count);
        var warnedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (item, route) in renderQueue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            BuildPathUtils.WarnIfWindowsIncompatible(route.OutputPath, warnedOutputPaths, logger);
            currentKeys.TryAdd(key, 0);
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
                existing.RouteHash == routeHash &&
                existing.RenderDependencyHash == renderDependencyHash;

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
                    : existing.RenderDependencyHash != renderDependencyHash ? "render_dependency_changed"
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

            var pageInfo = new PageInfo
            {
                Title = item.Title,
                Url = route.Url,
                Content = content,
                Summary = item.Meta.TryGetValue("summary", out var summary) ? summary?.ToString() : null,
                TableOfContents = GetTableOfContents(item),
                PublishDate = item.PublishAt,
                Fields = item.Fields,
                Seo = seoBuilder?.Invoke(item, route)
            };

            var pageModel = new PageModel
            {
                Site = siteModel,
                Page = pageInfo
            };

            var pageRenderStopwatch = Stopwatch.StartNew();
            var html = renderer.RenderPage(route.Template, pageModel);
            if (htmlPostProcessor is not null)
            {
                html = htmlPostProcessor(item, route, pageInfo, html);
            }
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
                    TemplateHash = templateHash,
                    RenderDependencyHash = renderDependencyHash
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
        string outputPathEncoding,
        string outputDir,
        string templateHash,
        string renderDependencyHash,
        bool incrementalEnabled,
        BuildManifest manifest,
        ConcurrentDictionary<string, byte> currentKeys,
        ConcurrentDictionary<string, int> renderReasons,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken,
        Func<ContentItem, RouteInfo, SeoModel>? seoBuilder = null,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = null,
        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor = null)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var stageMetricsLock = new object();
        var specialLists = SpecialListRouteBuilder.Build(routed, collections, layoutsDir, listPageContentMode, outputPathEncoding);
        foreach (var x in specialLists)
        {
            currentKeys.TryAdd(BuildPathUtils.NormalizeRelPath(x.Route.OutputPath), 0);
        }

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken };

        if (incrementalEnabled)
        {
            var rendered = 0;
            var skipped = 0;
            await Parallel.ForEachAsync(specialLists, parallelOptions, async (x, ct) =>
            {
                var result = await RenderSpecialListIfNeededAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, templateHash, renderDependencyHash, manifest, renderReasons, maxDegreeOfParallelism, x.IncludeContent, ct, seoBuilder, listSeoBuilder, listHtmlPostProcessor);
                Interlocked.Add(ref rendered, result.RenderedCount);
                Interlocked.Add(ref skipped, result.SkippedCount);
                lock (stageMetricsLock) { stageMetrics = MergeCollectors(stageMetrics, result.StageMetrics); }
            });

            return new SpecialListRenderResult(rendered, skipped, stageMetrics.Snapshot());
        }

        var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(specialLists, parallelOptions, async (x, ct) =>
        {
            var metrics = await RenderSpecialListAlwaysAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, writeLocks, maxDegreeOfParallelism, x.IncludeContent, ct, seoBuilder, listSeoBuilder, listHtmlPostProcessor);
            lock (stageMetricsLock) { stageMetrics = MergeCollectors(stageMetrics, metrics); }
        });

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
        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, html, writeLocks, cancellationToken);
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
            return new SpecialListRenderResult(0, 1, stageMetrics.Snapshot());
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
        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, html, new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase), cancellationToken);
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

        return new SpecialListRenderResult(1, 0, stageMetrics.Snapshot());
    }

    private static async Task<List<PageInfo>> BuildPageInfosAsync(
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

    private static IReadOnlyList<TableOfContentsEntry>? GetTableOfContents(ContentItem item)
        => item.Meta.TryGetValue("tableOfContents", out var toc) && toc is IReadOnlyList<TableOfContentsEntry> entries
            ? entries
            : null;

    private static PageInfo CreateListPageInfo(SiteModel siteModel, RouteInfo listRoute)
    {
        return new PageInfo
        {
            Title = listRoute.Url == "/" ? siteModel.Title : BuildListTitle(listRoute.Url),
            Url = listRoute.Url,
            Content = string.Empty,
            Summary = BuildListSummary(siteModel, listRoute)
        };
    }

    private static string BuildListSummary(SiteModel siteModel, RouteInfo listRoute)
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

    private static string BuildListTitle(string url)
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
