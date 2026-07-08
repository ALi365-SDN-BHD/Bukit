using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
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
        Func<ContentDocument, RouteInfo, SeoModel>? seoBuilder = null,
        Func<ContentDocument, RouteInfo, PageInfo, string, string>? htmlPostProcessor = null,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = null,
        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor = null)
    {
        var renderedCount = 0;
        var skippedCount = 0;
        var renderReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stageMetrics = new BuildStageMetricsCollector();

        if (maxDegreeOfParallelism <= 0) maxDegreeOfParallelism = Environment.ProcessorCount;
        maxDegreeOfParallelism = Math.Clamp(maxDegreeOfParallelism, 1, Math.Max(1, Environment.ProcessorCount * 2));
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
                        var document = entry.Document!;
                        var route = entry.Route;
                        var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
                        var metadataHashSw = Stopwatch.StartNew();
                        var mh = IncrementalBuildEngine.ComputeMetadataHash(document);
                        metadataHashSw.Stop();
                        stageMetrics.Increment("metadataHash");
                        stageMetrics.AddDuration("metadataHash", metadataHashSw.ElapsedMilliseconds);
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
                            if (IncrementalBuildEngine.TryComputeStableContentHash(document, bodyStore, mh, out var sch))
                                contentHash = sch;
                            else
                                contentHash = IncrementalBuildEngine.ComputeContentHash(document, bodyStore);
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

                        var contentRecord = document.Record;
                        var bodyLoadSw = Stopwatch.StartNew();
                        var content = await ContentBodyResolver.GetHtmlAsync(document, bodyStore, ct);
                        bodyLoadSw.Stop();
                        stageMetrics.Increment("bodyLoad");
                        stageMetrics.AddDuration("bodyLoad", bodyLoadSw.ElapsedMilliseconds);
                        var pageInfo = new PageInfo
                        {
                            Title = document.Title,
                            Url = route.Url,
                            Content = content,
                            Summary = contentRecord.Presentation.Summary ?? ContentFieldReader.GetSummary(document),
                            TableOfContents = SpecialListRenderer.GetTableOfContents(document),
                            PublishDate = document.PublishAt,
                            Fields = document.CustomFields,
                            ContentRecord = contentRecord,
                            Entities = contentRecord.Entities,
                            Provenance = contentRecord.Provenance,
                            Trust = contentRecord.Trust,
                            Representations = PublishRepresentationRegistry.DocumentKinds(),
                            Seo = seoBuilder?.Invoke(document, route)
                        };
                        var pageModel = new PageModel { Site = siteModel, Page = pageInfo };
                        var html = renderer.RenderPage(route.Template, pageModel);
                        if (htmlPostProcessor is not null) html = htmlPostProcessor(document, route, pageInfo, html);
                        await WriteUtf8LockedAsync(outputDir, route.OutputPath, html, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        stageMetrics.Increment("pageRender");
                        stageMetrics.AddDuration("pageRender", 0);
                        if (needsIncrementalMode) manifestEntries![key] = new BuildManifestEntry { OutputPath = key, Url = route.Url, Template = route.Template, MetadataHash = mh, ContentHash = contentHash ?? IncrementalBuildEngine.ComputeContentHash(document, mh, content), RouteHash = rh, TemplateHash = templateHash, RenderDependencyHash = renderDependencyHash };
                        break;
                    }

                case RenderEntryKind.List:
                    {
                        var listRoute = entry.Route;
                        var source = entry.SourceDocuments!;
                        var includeContent = entry.IncludeContent;
                        var key = BuildPathUtils.NormalizeRelPath(listRoute.OutputPath);
                        var rh = IncrementalBuildEngine.ComputeRouteHash(listRoute);
                        var ch = await IncrementalBuildEngine.ComputeListContentHashAsync(templateHash, listRoute.Template, source, manifest, bodyStore, includeContent, ct);
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

                        var pageInfos = await SpecialListRenderer.BuildPageInfosAsync(source, bodyStore, includeContent, maxDegreeOfParallelism, entries.Count, ct, stageMetrics, "listBodyLoad", seoBuilder);
                        var listPage = SpecialListRenderer.CreateListPageInfo(siteModel, listRoute);
                        if (entry.ListPageFields is not null)
                        {
                            listPage = listPage with { Fields = entry.ListPageFields };
                        }

                        listPage = listPage with { Seo = listSeoBuilder?.Invoke(listRoute, listPage) };
                        var listModel = SpecialListRenderer.CreateListPageModel(siteModel, listPage, pageInfos, entry.ListPageContext);
                        var listHtml = renderer.RenderList(listRoute.Template, listModel);
                        if (listHtmlPostProcessor is not null) listHtml = listHtmlPostProcessor(listRoute, listPage, listHtml);
                        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, listHtml, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        renderReasons.AddOrUpdate("list_render", 1, (_, v) => v + 1);
                        stageMetrics.Increment("listBuild");
                        stageMetrics.AddDuration("listBuild", 0);

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
                            Summary = siteModel.Description,
                            Representations = [PublishRepresentationRegistry.Html.Kind]
                        };
                        var pageModel = new PageModel { Site = siteModel, Page = pageInfo };
                        var staticHtml = renderer.RenderPage(route.Template, pageModel);
                        await WriteUtf8LockedAsync(outputDir, route.OutputPath, staticHtml, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        stageMetrics.Increment("staticRender");
                        stageMetrics.AddDuration("staticRender", 0);
                        break;
                    }
            }
        });

        return new DispatchResult(renderedCount, skippedCount,
            new Dictionary<string, int>(renderReasons, StringComparer.OrdinalIgnoreCase),
            stageMetrics.Snapshot());
    }

    internal static async Task<SpecialListRenderResult> RenderSpecialListsAsync(
        IReadOnlyList<RoutedContentDocument> routed,
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
        Func<ContentDocument, RouteInfo, SeoModel>? seoBuilder = null,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = null,
        Func<RouteInfo, PageInfo, string, string>? listHtmlPostProcessor = null,
        ThemeTemplateResolver? templateResolver = null)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var specialLists = SpecialListRouteBuilder.Build(routed, collections, layoutsDir, listPageContentMode, outputPathEncoding, templateResolver);
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
                var result = await SpecialListRenderer.RenderSpecialListIfNeededAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, templateHash, renderDependencyHash, manifest, renderReasons, maxDegreeOfParallelism, specialLists.Count, x.IncludeContent, x.PageFields, x.PageContext, ct, seoBuilder, listSeoBuilder, listHtmlPostProcessor);
                Interlocked.Add(ref rendered, result.RenderedCount);
                Interlocked.Add(ref skipped, result.SkippedCount);
                stageMetrics.Merge(result.StageMetrics);
            });

            return new SpecialListRenderResult(rendered, skipped, stageMetrics.Snapshot());
        }

        var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(specialLists, parallelOptions, async (x, ct) =>
        {
            var metrics = await SpecialListRenderer.RenderSpecialListAlwaysAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, writeLocks, maxDegreeOfParallelism, specialLists.Count, x.IncludeContent, x.PageFields, x.PageContext, ct, seoBuilder, listSeoBuilder, listHtmlPostProcessor);
            stageMetrics.Merge(metrics);
        });

        return new SpecialListRenderResult(specialLists.Count, 0, stageMetrics.Snapshot());
    }

    internal static async Task WriteUtf8LockedAsync(
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
