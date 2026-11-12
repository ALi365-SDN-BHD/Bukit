using Bukit.Theme;
using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Engine.RouteMetadata;
using Bukit.Engine.Primitives;

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
        HtmlTransformPipeline? htmlTransformPipeline = null,
        RenderDependencySnapshot? dependencySnapshot = null)
    {
        var renderedCount = 0;
        var skippedCount = 0;
        var renderReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stageMetrics = new BuildStageMetricsCollector();
        var dispatchStarted = Stopwatch.GetTimestamp();

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
                var seo = seoBuilder?.Invoke(document, e.Route);
                var pageInfo = new PageInfo
                {
                    Title = document.Title,
                    Url = e.Route.Url,
                    Content = string.Empty,
                    Summary = document.Record.Presentation.Summary ?? ContentFieldReader.GetSummary(document),
                    PublishDate = document.PublishAt,
                    UpdatedAt = document.Record.Lifecycle.UpdatedAt,
                    Fields = CrossPageFields.Visible(document.CustomFields),
                    ContentRecord = document.Record,
                    Entities = document.Record.Entities,
                    Provenance = document.Record.Provenance,
                    Trust = document.Record.Trust,
                    Representations = PublishRepresentationRegistry.DocumentKinds(),
                    RepresentationUrls = ResolveRepresentationUrls(document, e.Route, seo, siteModel.BaseUrl),
                    Seo = seo
                };
                return RouteMetadataApplicator.ApplyToPage(pageInfo, e.Route.Url, routeMetadata, document);
            })
            .ToArray();

        var dispatchTask = Parallel.ForEachAsync(entries, parallelOptions, async (entry, ct) =>
        {
            // Site-wide inputs plus this route's metadata and breadcrumb; shared collections are added per route.
            var routeBaseHash = renderDependencyHashResolver?.Invoke(entry.Route) ?? renderDependencyHash;
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
                        // Compare against the collections the previous render read.
                        var entryRenderDependencyHash = RenderReadTracking.HashFor(dependencySnapshot, routeBaseHash, existing?.RenderReads);

                        var canEvaluateSkip = incrementalEnabled && hasExisting && outputExists &&
                            existing!.TemplateHash == templateHash && existing.MetadataHash == mh &&
                            existing.RouteHash == rh && existing.RenderDependencyHash == entryRenderDependencyHash;

                        string? contentHash = null;
                        if (IncrementalBuildEngine.TryComputeStableContentHash(document, bodyStore, mh, out var stableContentHash))
                        {
                            contentHash = stableContentHash;
                        }
                        else if (canEvaluateSkip)
                        {
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
                            // "metadata_changed" covers record and custom-field changes; "content_changed" is the body alone.
                            var reason = !hasExisting ? "new_page" : !outputExists ? "output_missing"
                                : existing!.TemplateHash != templateHash ? "template_changed"
                                : existing.MetadataHash != mh ? "metadata_changed"
                                : existing.RouteHash != rh ? "route_changed"
                                : existing.RenderDependencyHash != entryRenderDependencyHash ? RenderReadTracking.ChangeReason(dependencySnapshot, existing, routeBaseHash)
                                : contentHash is not null && existing.ContentHash != contentHash ? "content_changed" : "render";
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
                        var pageRenderStarted = Stopwatch.GetTimestamp();
                        var seo = seoBuilder?.Invoke(document, route);
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
                            RepresentationUrls = ResolveRepresentationUrls(document, route, seo, siteModel.BaseUrl),
                            Seo = seo
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
                        var renderCallStarted = Stopwatch.GetTimestamp();
                        IReadOnlyList<string>? pageReads = null;
                        var rawRedirect = route.Template == RedirectPageHtml.RouteTemplate;
                        var html = string.IsNullOrEmpty(route.Template) || rawRedirect
                            ? content
                            : RenderReadTracking.Render(dependencySnapshot, () => renderer.RenderPage(route.Template, pageModel), out pageReads);
                        stageMetrics.AddElapsedTicks("renderCall", Stopwatch.GetTimestamp() - renderCallStarted);
                        if (!rawRedirect && htmlTransformPipeline is not null)
                        {
                            var transformStarted = Stopwatch.GetTimestamp();
                            html = htmlTransformPipeline.Transform(
                                route, HtmlDocumentKind.Content, pageInfo, document, logger, html);
                            stageMetrics.AddElapsedTicks("htmlTransform", Stopwatch.GetTimestamp() - transformStarted);
                        }
                        var writeStarted = Stopwatch.GetTimestamp();
                        await WriteUtf8LockedAsync(outputDir, route.OutputPath, html, writeLocks, ct);
                        stageMetrics.AddElapsedTicks("renderWrite", Stopwatch.GetTimestamp() - writeStarted);
                        Interlocked.Increment(ref renderedCount);
                        stageMetrics.Increment("pageRender");
                        stageMetrics.AddElapsedTicks("pageRender", Stopwatch.GetTimestamp() - pageRenderStarted);
                        if (needsIncrementalMode) manifestEntries![key] = new BuildManifestEntry { OutputPath = key, Url = route.Url, Template = route.Template, MetadataHash = mh, ContentHash = contentHash ?? IncrementalBuildEngine.ComputeContentHash(document, mh, content), RouteHash = rh, TemplateHash = templateHash, RenderDependencyHash = RenderReadTracking.HashFor(dependencySnapshot, routeBaseHash, pageReads), RenderBaseHash = dependencySnapshot is null ? null : routeBaseHash, RenderReads = pageReads };
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
                        var entryRenderDependencyHash = RenderReadTracking.HashFor(dependencySnapshot, routeBaseHash, le?.RenderReads);

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
                        var listRenderStarted = Stopwatch.GetTimestamp();
                        var listPage = SpecialListRenderer.CreateListPageInfo(siteModel, listRoute, entry.ListPageContext);
                        if (entry.ListPageFields is not null)
                        {
                            listPage = listPage with { Fields = entry.ListPageFields };
                            listPage = SpecialListRenderer.ApplyListPageFieldOverrides(listPage, entry.ListPageFields);
                        }

                        listPage = listPage with { Seo = listSeoBuilder?.Invoke(listRoute, listPage) };
                        var listModel = SpecialListRenderer.CreateListPageModel(siteModel, listPage, pageInfos, entry.ListPageContext);
                        var listCallStarted = Stopwatch.GetTimestamp();
                        var listHtml = RenderReadTracking.Render(dependencySnapshot, () => renderer.RenderList(listRoute.Template, listModel), out var listReads);
                        stageMetrics.AddElapsedTicks("renderCall", Stopwatch.GetTimestamp() - listCallStarted);
                        if (htmlTransformPipeline is not null)
                        {
                            var transformStarted = Stopwatch.GetTimestamp();
                            listHtml = htmlTransformPipeline.Transform(
                                listRoute, HtmlDocumentKind.List, listPage, null, logger, listHtml);
                            stageMetrics.AddElapsedTicks("htmlTransform", Stopwatch.GetTimestamp() - transformStarted);
                        }
                        var listWriteStarted = Stopwatch.GetTimestamp();
                        await WriteUtf8LockedAsync(outputDir, listRoute.OutputPath, listHtml, writeLocks, ct);
                        stageMetrics.AddElapsedTicks("renderWrite", Stopwatch.GetTimestamp() - listWriteStarted);
                        Interlocked.Increment(ref renderedCount);
                        renderReasons.AddOrUpdate("list_render", 1, (_, v) => v + 1);
                        stageMetrics.Increment("listBuild");
                        stageMetrics.AddElapsedTicks("listBuild", Stopwatch.GetTimestamp() - listRenderStarted);

                        if (needsIncrementalMode)
                        {
                            manifestEntries![key] = new BuildManifestEntry { OutputPath = key, Url = listRoute.Url, Template = listRoute.Template, ContentHash = ch, RouteHash = rh, TemplateHash = templateHash, RenderDependencyHash = RenderReadTracking.HashFor(dependencySnapshot, routeBaseHash, listReads), RenderBaseHash = dependencySnapshot is null ? null : routeBaseHash, RenderReads = listReads };
                        }
                        break;
                    }

                case RenderEntryKind.Static:
                    {
                        var staticRenderStarted = Stopwatch.GetTimestamp();
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
                        var staticCallStarted = Stopwatch.GetTimestamp();
                        var staticHtml = renderer.RenderPage(route.Template, pageModel);
                        stageMetrics.AddElapsedTicks("renderCall", Stopwatch.GetTimestamp() - staticCallStarted);
                        if (htmlTransformPipeline is not null)
                        {
                            var transformStarted = Stopwatch.GetTimestamp();
                            staticHtml = htmlTransformPipeline.Transform(
                                route, HtmlDocumentKind.Static, pageInfo, null, logger, staticHtml);
                            stageMetrics.AddElapsedTicks("htmlTransform", Stopwatch.GetTimestamp() - transformStarted);
                        }
                        var staticWriteStarted = Stopwatch.GetTimestamp();
                        await WriteUtf8LockedAsync(outputDir, route.OutputPath, staticHtml, writeLocks, ct);
                        stageMetrics.AddElapsedTicks("renderWrite", Stopwatch.GetTimestamp() - staticWriteStarted);
                        Interlocked.Increment(ref renderedCount);
                        stageMetrics.Increment("staticRender");
                        stageMetrics.AddElapsedTicks("staticRender", Stopwatch.GetTimestamp() - staticRenderStarted);
                        break;
                    }
            }
        });

        try
        {
            await dispatchTask.ConfigureAwait(false);
        }
        finally
        {
            // Release shared template models and collect their conversion time, even when a page fails.
            if (renderer is IRenderPassCompletion completion)
            {
                stageMetrics.AddElapsedTicks("renderModelBind", completion.CompleteRenderPass());
            }

            stageMetrics.AddElapsedTicks("renderDispatch", Stopwatch.GetTimestamp() - dispatchStarted);
        }

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
        ContentTieBreak? tieBreak,
        CancellationToken cancellationToken,
        Func<ContentDocument, RouteInfo, SeoModel>? seoBuilder = null,
        Func<RouteInfo, PageInfo, SeoModel>? listSeoBuilder = null,
        HtmlTransformPipeline? htmlTransformPipeline = null,
        ThemeTemplateResolver? templateResolver = null,
        ILogger? logger = null,
        RenderDependencySnapshot? dependencySnapshot = null)
    {
        var stageMetrics = new BuildStageMetricsCollector();
        var specialLists = SpecialListRouteBuilder.Build(routed, collections, layoutsDir, listPageContentMode, outputPathEncoding, tieBreak, templateResolver);
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
                var result = await SpecialListRenderer.RenderSpecialListIfNeededAsync(x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, templateHash, renderDependencyHash, baselineEntries, updates, renderReasons, maxDegreeOfParallelism, specialLists.Count, x.IncludeContent, x.PageFields, x.PageContext, ct, transformLogger, seoBuilder, listSeoBuilder, htmlTransformPipeline, dependencySnapshot);
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

    // Only pages that may be indexed get their Markdown/JSON URLs, matching the links inject mode adds.
    // The .md/.json files carry no noindex marker, so linking them from a noindex page would let
    // search engines index the copy. Expiry is ignored so page HTML does not depend on the build clock.
    // Without an SEO model (site.seo.enabled: false), the page's own robots field decides.
    private static IReadOnlyDictionary<string, string>? ResolveRepresentationUrls(
        ContentDocument document, RouteInfo route, SeoModel? seo, string baseUrl)
        => SeoIndexBuilder.IsIndexableIgnoringExpiry(document, seo?.Robots ?? SeoModelBuilder.FirstTextField(document.CustomFields, "robots"))
            ? ContentProjectionPaths.GetRepresentationUrls(route, baseUrl)
            : null;

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
