using System.Collections.Concurrent;
using System.Diagnostics;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RenderStageTimingTests
{
    [Fact]
    public void AddElapsedTicks_AccumulatesSubMillisecondDurations()
    {
        var collector = new BuildStageMetricsCollector();
        var tenthOfMillisecond = Stopwatch.Frequency / 10_000;

        Parallel.For(0, 20_000, _ => collector.AddElapsedTicks("renderWrite", tenthOfMillisecond));
        collector.AddDuration("renderWrite", 5);

        var milliseconds = collector.Snapshot().DurationsMs["renderWrite"];
        Assert.InRange(milliseconds, 2_000 + 5 - 1, 2_000 + 5);
    }

    [Fact]
    public async Task DispatchAsync_TimesContentListAndStaticRenders()
    {
        var outputDir = CreateOutputDir();
        try
        {
            var staticDir = Path.Combine(outputDir, "input");
            Directory.CreateDirectory(staticDir);
            await File.WriteAllTextAsync(Path.Combine(staticDir, "about.html"), "static");
            var item = ContentDocument.Create("post", "Post", "post", DateTimeOffset.UnixEpoch, "content");
            var pageRoute = new RouteInfo("/post/", "post/index.html", "page.html");
            var entries = new List<RenderEntry>
            {
                RenderEntry.ForPage(item, pageRoute),
                RenderEntry.ForList(new RouteInfo("/", "index.html", "list.html"), [new RoutedContentDocument(item, pageRoute)], false)
            };
            entries.AddRange(RenderEntry.ForStaticDir(staticDir, "static.html", _ => { }, false));
            var renderer = new SlowRenderer(modelBindTicks: Stopwatch.Frequency / 100);

            var result = await Dispatch(entries, renderer, outputDir, new HtmlTransformPipeline([new SlowTransform()]));

            var durations = result.StageMetrics.DurationsMs;
            foreach (var stage in new[] { "pageRender", "listBuild", "staticRender", "renderCall", "htmlTransform", "renderDispatch" })
            {
                Assert.True(durations.TryGetValue(stage, out var value) && value > 0, $"{stage} should be greater than zero");
            }

            // Three tiny writes can total under one millisecond; sub-millisecond summing is covered above.
            Assert.True(durations.TryGetValue("renderWrite", out var write) && write >= 0);

            Assert.Equal(10, durations["renderModelBind"]);
            Assert.Equal(1, renderer.CompletedPasses);
            Assert.True(durations["renderCall"] >= 3 * SlowRenderer.DelayMs);
            Assert.True(durations["renderDispatch"] >= durations["pageRender"]);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DispatchAsync_CompletesRenderPassWhenAPageFails()
    {
        var outputDir = CreateOutputDir();
        try
        {
            var item = ContentDocument.Create("post", "Post", "post", DateTimeOffset.UnixEpoch, "content");
            var renderer = new SlowRenderer(modelBindTicks: 0, fail: true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatch(
                [RenderEntry.ForPage(item, new RouteInfo("/post/", "post/index.html", "page.html"))],
                renderer,
                outputDir,
                null));

            Assert.Equal(1, renderer.CompletedPasses);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DispatchAsync_OmitsModelBindForRenderersWithoutPassCompletion()
    {
        var outputDir = CreateOutputDir();
        try
        {
            var item = ContentDocument.Create("post", "Post", "post", DateTimeOffset.UnixEpoch, "content");

            var result = await Dispatch(
                [RenderEntry.ForPage(item, new RouteInfo("/post/", "post/index.html", "page.html"))],
                new PlainRenderer(),
                outputDir,
                null);

            Assert.False(result.StageMetrics.DurationsMs.ContainsKey("renderModelBind"));
            Assert.True(result.StageMetrics.DurationsMs.ContainsKey("renderDispatch"));
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    private static Task<PageRenderDispatcher.DispatchResult> Dispatch(
        IReadOnlyList<RenderEntry> entries,
        ITemplateRenderer renderer,
        string outputDir,
        HtmlTransformPipeline? pipeline) =>
        PageRenderDispatcher.DispatchAsync(
            entries,
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "s", Title = "s", BaseUrl = "/", Language = "en" },
            outputDir,
            string.Empty,
            string.Empty,
            false,
            new BuildManifest(),
            null,
            new ConcurrentDictionary<string, byte>(),
            2,
            new ConsoleLogger(LogLevel.Error),
            CancellationToken.None,
            htmlTransformPipeline: pipeline);

    private static string CreateOutputDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "bukit-render-timing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class SlowRenderer(long modelBindTicks, bool fail = false) : ITemplateRenderer, IRenderPassCompletion
    {
        internal const int DelayMs = 3;
        private int _completedPasses;

        public int CompletedPasses => Volatile.Read(ref _completedPasses);

        public string RenderPage(string templateRelativePath, PageModel model) =>
            Render(model.Page.Content);

        public string RenderList(string templateRelativePath, ListPageModel model) =>
            Render("list");

        public long CompleteRenderPass()
        {
            Interlocked.Increment(ref _completedPasses);
            return modelBindTicks;
        }

        private string Render(string body)
        {
            if (fail)
            {
                throw new InvalidOperationException("render failed");
            }

            Thread.Sleep(DelayMs);
            return $"<html><head></head><body>{body}</body></html>";
        }
    }

    private sealed class PlainRenderer : ITemplateRenderer
    {
        public string RenderPage(string templateRelativePath, PageModel model) => model.Page.Content;

        public string RenderList(string templateRelativePath, ListPageModel model) => "list";
    }

    private sealed class SlowTransform : IHtmlTransform
    {
        public string Name => "slow";

        public string Transform(HtmlTransformContext context, string html)
        {
            Thread.Sleep(2);
            return html;
        }
    }
}
