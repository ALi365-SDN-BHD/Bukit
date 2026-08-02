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
using Bukit.Engine.RouteMetadata;

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
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = null,
        Func<RouteInfo, string>? renderDependencyHashResolver = null,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata = null,
        HtmlTransformPipeline? htmlTransformPipeline = null)
    {
        var renderedCount = 0;
        var skippedCount = 0;
        var renderReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stageMetrics = new BuildStageMetricsCollector();

        var effectiveParallelism = ComputeOptimalParallelism(entries.Count, maxDegreeOfParallelism);
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = effectiveParallelism, CancellationToken = cancellationToken };
        var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            currentKeys.TryAdd(BuildPathUtils.NormalizeRelPath(entry.Route.OutputPath), 0);
        }

        var needsIncrementalMode = incrementalEnabled && manifestEntries is not null;
        var pageIndex = entries
            .Where(e => e.Kind == RenderEntryKind.Page && e.Document is not null)
            .Select(e =>
            {
                var document = e.Document!;
                var pageInfo = new PageInfo
                {
                    Title = document.Title,
                    Url = e.Route.Url,
                    Content = string.Empty,
                    Summary = document.Record.Presentation.Summary ?? ContentFieldReader.GetSummary(document),
                    PublishDate = document.PublishAt,
                    UpdatedAt = document.Record.Lifecycle.UpdatedAt,
                    Fields = document.CustomFields,
                    ContentRecord = document.Record,
                    Entities = document.Record.Entities,
                    Provenance = document.Record.Provenance,
                    Trust = document.Record.Trust,
                    Representations = PublishRepresentationRegistry.DocumentKinds(),
                    Seo = seoBuilder?.Invoke(document, e.Route)
                };
                return RouteMetadataApplicator.ApplyToPage(pageInfo, e.Route.Url, routeMetadata, document);
            })
            .ToArray();

        await Parallel.ForEachAsync(entries, parallelOptions, async (entry, ct) =>
        {
            var entryRenderDependencyHash = renderDependencyHashResolver?.Invoke(entry.Route) ?? renderDependencyHash;
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
                            existing.RouteHash == rh && existing.RenderDependencyHash == entryRenderDependencyHash;

                        string? contentHash = null;
                        if (canEvaluateSkip)
                        {
                            if (IncrementalBuildEngine.TryComputeStableContentHash(document, bodyStore, mh, out var sch))
                                contentHash = sch;
                            else
                                contentHash = await IncrementalBuildEngine.ComputeContentHashAsync(document, bodyStore, ct).ConfigureAwait(false);
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
                                : existing.RenderDependencyHash != entryRenderDependencyHash ? "render_dependency_changed" : "render";
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
                            UpdatedAt = contentRecord.Lifecycle.UpdatedAt,
                            Fields = document.CustomFields,
                            ContentRecord = contentRecord,
                            Entities = contentRecord.Entities,
                            Provenance = contentRecord.Provenance,
                            Trust = contentRecord.Trust,
                            Representations = PublishRepresentationRegistry.DocumentKinds(),
                            Seo = seoBuilder?.Invoke(document, route)
                        };
                        if (entry.MetadataListRoute is { } metadataListRoute)
                        {
                            var pagination = ListPageMetadataBuilder.BuildPagination(metadataListRoute);
                            pageInfo = pageInfo with
                            {
                                Title = ListPageMetadataBuilder.BuildTitle(
                                    metadataListRoute, pagination, siteModel.Language),
                                Summary = ListPageMetadataBuilder.BuildSummary(
                                    metadataListRoute, pagination, siteModel.Language)
                            };
                        }
                        else
                        {
                            pageInfo = RouteMetadataApplicator.ApplyToPage(
                                pageInfo, route.Url, routeMetadata, document);
                        }
                        var pageModel = new PageModel { Site = siteModel, Page = pageInfo, Pages = pageIndex };
                        var html = renderer.RenderPage(route.Template, pageModel);
                        if (htmlTransformPipeline is not null)
                        {
                            html = htmlTransformPipeline.Transform(
                                route, HtmlDocumentKind.Content, pageInfo, document, logger, html);
                        }
                        await WriteUtf8LockedAsync(outputDir, route.OutputPath, html, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        stageMetrics.Increment("pageRender");
                        stageMetrics.AddDuration("pageRender", 0);
                        if (needsIncrementalMode) manifestEntries![key] = new BuildManifestEntry { OutputPath = key, Url = route.Url, Template = route.Template, MetadataHash = mh, ContentHash = contentHash ?? IncrementalBuildEngine.ComputeContentHash(document, mh, content), RouteHash = rh, TemplateHash = templateHash, RenderDependencyHash = entryRenderDependencyHash };
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
                            le.RouteHash == rh && le.RenderDependencyHash == entryRenderDependencyHash;

                        if (canSkip)
                        {
                            Interlocked.Increment(ref skippedCount);
                            renderReasons.AddOrUpdate("list_unchanged", 1, (_, v) => v + 1);
                            return;
                        }

                        var pageInfos = await SpecialListRenderer.BuildPageInfosAsync(source, bodyStore, includeContent, maxDegreeOfParallelism, entries.Count, ct, stageMetrics, "listBodyLoad", seoBuilder);
                        var listPage = SpecialListRenderer.CreateListPageInfo(siteModel, listRoute, entry.ListPageContext);
                        if (entry.ListPageFields is not null)
                        {
                            listPage = listPage with { Fields = entry.ListPageFields };
                            listPage = SpecialListRenderer.ApplyListPageFieldOverrides(listPage, entry.ListPageFields);
                        }

                        listPage = listPage with { Seo = listSeoBuilder?.Invoke(listRoute, listPage) };
                        var listModel = SpecialListRenderer.CreateListPageModel(siteModel, listPage, pageInfos, entry.ListPageContext);
                        var listHtml = renderer.RenderList(listRoute.Template, listModel);
                        if (htmlTransformPipeline is not null)
                        {
                            listHtml = htmlTransformPipeline.Transform(
                                listRoute, HtmlDocumentKind.List, listPage, null, logger, listHtml);
                        }
                        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, listHtml, writeLocks, ct);
                        Interlocked.Increment(ref renderedCount);
                        renderReasons.AddOrUpdate("list_render", 1, (_, v) => v + 1);
                        stageMetrics.Increment("listBuild");
                        stageMetrics.AddDuration("listBuild", 0);

                        if (needsIncrementalMode)
                        {
                            manifestEntries![key] = new BuildManifestEntry { OutputPath = key, Url = listRoute.Url, Template = listRoute.Template, ContentHash = ch, RouteHash = rh, TemplateHash = templateHash, RenderDependencyHash = entryRenderDependencyHash };
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
                        pageInfo = RouteMetadataApplicator.ApplyToPage(pageInfo, route.Url, routeMetadata);
                        var pageModel = new PageModel { Site = siteModel, Page = pageInfo, Pages = pageIndex };
                        var staticHtml = renderer.RenderPage(route.Template, pageModel);
                        if (htmlTransformPipeline is not null)
                        {
                            staticHtml = htmlTransformPipeline.Transform(
                                route, HtmlDocumentKind.Static, pageInfo, null, logger, staticHtml);
                        }
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
        HtmlTransformPipeline? htmlTransformPipeline = null,
        ThemeTemplateResolver? templateResolver = null,
        ILogger? logger = null)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var specialLists = SpecialListRouteBuilder.Build(routed, collections, layoutsDir, listPageContentMode, outputPathEncoding, templateResolver);
        var transformLogger = logger ?? new ConsoleLogger(LogLevel.Error);
        foreach (var x in specialLists)
        {
            currentKeys.TryAdd(BuildPathUtils.NormalizeRelPath(x.Route.OutputPath), 0);
        }

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken };

        if (incrementalEnabled)
        {
            var rendered = 0;
            var skipped = 0;
            Dictionary<string, BuildManifestEntry> baselineEntries;
            lock (manifest)
            {
                baselineEntries = manifest.Entries.ToDictionary(
                    static entry => entry.Key,
                    static entry => entry.Value,
                    StringComparer.Ordinal);
            }

            var updates = new ConcurrentDictionary<string, BuildManifestEntry>(StringComparer.Ordinal);
            await Parallel.ForEachAsync(specialLists, parallelOptions, async (x, ct) =>
            {
                var result = await SpecialListRenderer.RenderSpecialListIfNeededAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, templateHash, renderDependencyHash, baselineEntries, updates, renderReasons, maxDegreeOfParallelism, specialLists.Count, x.IncludeContent, x.PageFields, x.PageContext, ct, transformLogger, seoBuilder, listSeoBuilder, htmlTransformPipeline);
                Interlocked.Add(ref rendered, result.RenderedCount);
                Interlocked.Add(ref skipped, result.SkippedCount);
                stageMetrics.Merge(result.StageMetrics);
            });

            lock (manifest)
            {
                foreach (var update in updates.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
                {
                    manifest.Entries[update.Key] = update.Value;
                }
            }

            return new SpecialListRenderResult(rendered, skipped, stageMetrics.Snapshot());
        }

        var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(specialLists, parallelOptions, async (x, ct) =>
        {
            var metrics = await SpecialListRenderer.RenderSpecialListAlwaysAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, writeLocks, maxDegreeOfParallelism, specialLists.Count, x.IncludeContent, x.PageFields, x.PageContext, ct, transformLogger, seoBuilder, listSeoBuilder, htmlTransformPipeline);
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

    // ── Adaptive parallelism ─────────────────────────────────────────────

    /// <summary>
    /// Computes optimal parallelism based on workload size and user configuration.
    /// <list type="bullet">
    ///   <item>&lt;100 pages: min(2, CPU) — avoids thread pool overhead for small sites</item>
    ///   <item>100–1000 pages: min(CPU, count) — standard CPU-bound scaling</item>
    ///   <item>&gt;1000 pages: min(CPU×1.5, count) — exploits I/O-bound template rendering</item>
    /// </list>
    /// </summary>
    internal static int ComputeOptimalParallelism(int itemCount, int requestedMaxDegreeOfParallelism)
    {
        var processorCount = Environment.ProcessorCount;

        int workloadBased;
        if (itemCount < 100)
        {
            workloadBased = Math.Min(2, processorCount);
        }
        else if (itemCount <= 1000)
        {
            workloadBased = Math.Min(processorCount, itemCount);
        }
        else
        {
            // I/O-bound rendering benefits from slight over-subscription
            workloadBased = Math.Min((int)Math.Ceiling(processorCount * 1.5), itemCount);
        }

        if (requestedMaxDegreeOfParallelism > 0)
        {
            return Math.Clamp(requestedMaxDegreeOfParallelism, 1, Math.Max(1, processorCount * 2));
        }

        return Math.Clamp(workloadBased, 1, Math.Max(1, processorCount * 2));
    }
}
