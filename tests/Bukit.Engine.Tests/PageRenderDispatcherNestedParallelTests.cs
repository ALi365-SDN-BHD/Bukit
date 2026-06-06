using System.Collections.Concurrent;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PageRenderDispatcherNestedParallelTests
{
    [Fact]
    public async Task RenderSpecialListsAsync_NonIncremental_MultipleLists_PeakConcurrencyBoundedByOuterMDoP()
    {
        var routed = CreateRoutedItems(10);
        var bodyStore = new ConcurrencyProbeBodyStore(holdDurationMs: 30);
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(outputDir, "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "index.html"), "{{ for p in pages }}{{ p.content }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ p.content }}{{ end }}");

        var result = await PageRenderDispatcher.RenderSpecialListsAsync(
            routed.ToRoutedDocuments(),
            bodyStore,
            new CaptureRenderer(),
            CreateSiteModel(),
            collections: null,
            layoutsDir: layoutsDir,
            listPageContentMode: "always",
            outputPathEncoding: "none",
            outputDir: outputDir,
            templateHash: "th",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 4,
            cancellationToken: CancellationToken.None);

        Assert.True(result.RenderedCount > 0);
        Assert.True(bodyStore.PeakConcurrency <= 4,
            $"Peak concurrency {bodyStore.PeakConcurrency} should not exceed outer MDoP=4");

        try { Directory.Delete(outputDir, true); } catch { }
    }

    [Fact]
    public async Task RenderSpecialListsAsync_Incremental_MultipleLists_PeakConcurrencyBoundedByOuterMDoP()
    {
        var routed = CreateRoutedItems(10);
        var bodyStore = new ConcurrencyProbeBodyStore(holdDurationMs: 30);
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(outputDir, "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "index.html"), "{{ for p in pages }}{{ p.content }}{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ p.content }}{{ end }}");

        var result = await PageRenderDispatcher.RenderSpecialListsAsync(
            routed.ToRoutedDocuments(),
            bodyStore,
            new CaptureRenderer(),
            CreateSiteModel(),
            collections: null,
            layoutsDir: layoutsDir,
            listPageContentMode: "always",
            outputPathEncoding: "none",
            outputDir: outputDir,
            templateHash: "th",
            renderDependencyHash: string.Empty,
            incrementalEnabled: true,
            manifest: new BuildManifest(),
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            renderReasons: new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 4,
            cancellationToken: CancellationToken.None);

        Assert.True(result.RenderedCount > 0);
        Assert.True(bodyStore.PeakConcurrency <= 4,
            $"Peak concurrency {bodyStore.PeakConcurrency} should not exceed outer MDoP=4");

        try { Directory.Delete(outputDir, true); } catch { }
    }

    private static List<(ContentDocument Item, RouteInfo Route)> CreateRoutedItems(int count)
    {
        var list = new List<(ContentDocument Item, RouteInfo Route)>(count);
        for (var i = 0; i < count; i++)
        {
            var item = ContentDocument.Create(
                id: $"id-{i}",
                title: $"Post {i}",
                slug: $"post-{i}",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                bodyKey: $"body-{i}");
            var route = new RouteInfo($"/blog/post-{i}/", $"blog/post-{i}/index.html", "pages/post.html");
            list.Add((item, route));
        }
        return list;
    }

    private static SiteModel CreateSiteModel()
    {
        return new SiteModel
        {
            Name = "site",
            Title = "site",
            BaseUrl = "/",
            Language = "zh-CN"
        };
    }

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public string RenderPage(string templateRelativePath, PageModel model) => model.Page.Content ?? string.Empty;
        public string RenderList(string templateRelativePath, ListPageModel model) => string.Empty;
    }

    internal sealed class ConcurrencyProbeBodyStore : IContentBodyStore
    {
        private readonly int _holdDurationMs;
        private int _current;
        private int _peak;
        private int _total;

        public ConcurrencyProbeBodyStore(int holdDurationMs = 0)
        {
            _holdDurationMs = holdDurationMs;
        }

        public int PeakConcurrency => Volatile.Read(ref _peak);
        public int TotalCalls => Volatile.Read(ref _total);

        public async Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _total);
            var entered = Interlocked.Increment(ref _current);
            UpdatePeak(entered);
            try
            {
                if (_holdDurationMs > 0)
                {
                    await Task.Delay(_holdDurationMs, cancellationToken);
                }
                else
                {
                    await Task.Yield();
                }
                return new ContentBody($"<p>{item.Id}</p>");
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }

        private void UpdatePeak(int observed)
        {
            int current;
            do
            {
                current = Volatile.Read(ref _peak);
                if (observed <= current) return;
            }
            while (Interlocked.CompareExchange(ref _peak, observed, current) != current);
        }
    }
}
