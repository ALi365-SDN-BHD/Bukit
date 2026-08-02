using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SpecialListRendererNestedParallelTests
{
    [Theory]
    [InlineData(0, 4, 4)]
    [InlineData(1, 4, 4)]
    [InlineData(1, 0, 0)]
    [InlineData(2, 4, 1)]
    [InlineData(5, 8, 1)]
    public void ComputeNestedDegreeOfParallelism_FollowsContract(int outerCount, int requestedMDoP, int expectedOrProcessorCountWhenZero)
    {
        var actual = SpecialListRenderer.ComputeNestedDegreeOfParallelism(outerCount, requestedMDoP);
        if (expectedOrProcessorCountWhenZero == 0)
        {
            Assert.Equal(Environment.ProcessorCount, actual);
        }
        else
        {
            Assert.Equal(expectedOrProcessorCountWhenZero, actual);
        }
    }

    [Fact]
    public async Task BuildPageInfosAsync_OuterCountGreaterThanOne_DegradesInnerParallelismToOne()
    {
        var source = CreateSource(10);
        var bodyStore = new ConcurrencyProbeBodyStore(holdDurationMs: 30);

        var infos = await SpecialListRenderer.BuildPageInfosAsync(
            source.ToRoutedDocuments(),
            bodyStore,
            includeContent: true,
            maxDegreeOfParallelism: 4,
            outerCount: 4,
            cancellationToken: CancellationToken.None);

        Assert.Equal(10, infos.Count);
        Assert.Equal(1, bodyStore.PeakConcurrency);
        Assert.Equal(10, bodyStore.TotalCalls);
    }

    [Fact]
    public async Task BuildPageInfosAsync_OuterCountOne_AllowsInnerParallelism()
    {
        var source = CreateSource(8);
        var bodyStore = new ConcurrencyProbeBodyStore(holdDurationMs: 50);

        var infos = await SpecialListRenderer.BuildPageInfosAsync(
            source.ToRoutedDocuments(),
            bodyStore,
            includeContent: true,
            maxDegreeOfParallelism: 4,
            outerCount: 1,
            cancellationToken: CancellationToken.None);

        Assert.Equal(8, infos.Count);
        Assert.True(bodyStore.PeakConcurrency > 1,
            $"Expected peak concurrency > 1 when outerCount=1, got {bodyStore.PeakConcurrency}");
        Assert.True(bodyStore.PeakConcurrency <= 4,
            $"Peak concurrency {bodyStore.PeakConcurrency} should not exceed mdop=4");
    }

    [Fact]
    public async Task BuildPageInfosAsync_PreservesSourceOrder()
    {
        var source = CreateSource(50);
        var bodyStore = new ConcurrencyProbeBodyStore(holdDurationMs: 1);

        var infos = await SpecialListRenderer.BuildPageInfosAsync(
            source.ToRoutedDocuments(),
            bodyStore,
            includeContent: true,
            maxDegreeOfParallelism: 4,
            outerCount: 1,
            cancellationToken: CancellationToken.None);

        Assert.Equal(50, infos.Count);
        for (var i = 0; i < source.Count; i++)
        {
            Assert.Equal(source[i].Item.Title, infos[i].Title);
            Assert.Equal(source[i].Route.Url, infos[i].Url);
        }
    }

    [Fact]
    public async Task BuildPageInfosAsync_IncludeContentFalse_DoesNotLoadBodies()
    {
        var source = CreateSource(5);
        var bodyStore = new ConcurrencyProbeBodyStore();

        var infos = await SpecialListRenderer.BuildPageInfosAsync(
            source.ToRoutedDocuments(),
            bodyStore,
            includeContent: false,
            maxDegreeOfParallelism: 4,
            outerCount: 4,
            cancellationToken: CancellationToken.None);

        Assert.Equal(5, infos.Count);
        Assert.Equal(0, bodyStore.TotalCalls);
        Assert.All(infos, info => Assert.Equal(string.Empty, info.Content));
    }

    [Fact]
    public async Task BuildPageInfosAsync_PrefersCanonicalSummaryWhenMetaMissing()
    {
        var item = ContentDocument.Create(
            id: "id-1",
            title: "Item 1",
            slug: "item-1",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = new("text", "Canonical list summary")
            },
            bodyKey: "body-1");
        var route = new RouteInfo("/posts/item-1/", "posts/item-1/index.html", "pages/post.html");

        var infos = await SpecialListRenderer.BuildPageInfosAsync(
            new List<(ContentDocument Item, RouteInfo Route)> { (item, route) }.ToRoutedDocuments(),
            new ConcurrencyProbeBodyStore(),
            includeContent: false,
            maxDegreeOfParallelism: 1,
            outerCount: 1,
            cancellationToken: CancellationToken.None);

        Assert.Single(infos);
        Assert.Equal("Canonical list summary", infos[0].Summary);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_Incremental_DoesNotExposeUpdatesUntilBatchCompletes()
    {
        string root = CreateListFixture();
        var renderer = new FirstCompletesThenBlocksRenderer();
        var manifest = new BuildManifest();
        var renderReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Task<PageRenderDispatcher.SpecialListRenderResult>? renderTask = null;

        try
        {
            renderTask = PageRenderDispatcher.RenderSpecialListsAsync(
                CreateSource(8).ToRoutedDocuments(),
                EmptyContentBodyStore.Instance,
                renderer,
                CreateSiteModel(),
                CreateCollections(8),
                Path.Combine(root, "layouts"),
                "never",
                "none",
                Path.Combine(root, "output"),
                "template-hash",
                "dependency-hash",
                incrementalEnabled: true,
                manifest,
                new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
                renderReasons,
                maxDegreeOfParallelism: 4,
                CancellationToken.None);

            await renderer.Blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForRenderedListAsync(renderReasons);
            await Task.Delay(50);

            Assert.Empty(manifest.Entries);
        }
        finally
        {
            renderer.Release();
            if (renderTask is not null)
            {
                await renderTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            TestCleanup.DeleteDirectory(root, true);
        }

        Assert.Equal(9, manifest.Entries.Count);
    }

    [Fact]
    public async Task RenderSpecialListsAsync_Incremental_CancellationDoesNotMergePartialUpdates()
    {
        string root = CreateListFixture();
        var renderer = new BlockingRenderer();
        var manifest = new BuildManifest();
        using var cancellation = new CancellationTokenSource();

        Task<PageRenderDispatcher.SpecialListRenderResult> renderTask =
            PageRenderDispatcher.RenderSpecialListsAsync(
                CreateSource(8).ToRoutedDocuments(),
                EmptyContentBodyStore.Instance,
                renderer,
                CreateSiteModel(),
                CreateCollections(8),
                Path.Combine(root, "layouts"),
                "never",
                "none",
                Path.Combine(root, "output"),
                "template-hash",
                "dependency-hash",
                incrementalEnabled: true,
                manifest,
                new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
                new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                maxDegreeOfParallelism: 4,
                cancellation.Token);

        try
        {
            await renderer.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            renderer.Release();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => renderTask.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(manifest.Entries);
        }
        finally
        {
            renderer.Release();
            TestCleanup.DeleteDirectory(root, true);
        }
    }

    [Fact]
    public async Task RenderSpecialListsAsync_Incremental_ProducesDeterministicManifestAcrossOneHundredRuns()
    {
        string root = CreateListFixture();
        string? expectedManifest = null;

        try
        {
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var manifest = new BuildManifest();
                string outputDir = Path.Combine(root, "output", iteration.ToString(System.Globalization.CultureInfo.InvariantCulture));

                await PageRenderDispatcher.RenderSpecialListsAsync(
                    CreateSource(8).ToRoutedDocuments(),
                    EmptyContentBodyStore.Instance,
                    new DeterministicRenderer(),
                    CreateSiteModel(),
                    CreateCollections(8),
                    Path.Combine(root, "layouts"),
                    "never",
                    "none",
                    outputDir,
                    "template-hash",
                    "dependency-hash",
                    incrementalEnabled: true,
                    manifest,
                    new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
                    new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    maxDegreeOfParallelism: 4,
                    CancellationToken.None);

                string manifestPath = Path.Combine(root, $"manifest-{iteration}.json");
                manifest.Save(manifestPath);
                string serialized = await File.ReadAllTextAsync(manifestPath);
                expectedManifest ??= serialized;
                Assert.Equal(expectedManifest, serialized);
                Assert.Equal(9, manifest.Entries.Count);
            }
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, true);
        }
    }

    private static async Task WaitForRenderedListAsync(
        ConcurrentDictionary<string, int> renderReasons)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (renderReasons.TryGetValue("list_render", out int count) && count > 0)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("No special list completed within the test deadline.");
    }

    private static string CreateListFixture()
    {
        string root = Path.Combine(Path.GetTempPath(), "bukit-special-list-manifest", Guid.NewGuid().ToString("N"));
        string pages = Path.Combine(root, "layouts", "pages");
        Directory.CreateDirectory(pages);
        File.WriteAllText(Path.Combine(pages, "index.html"), "home");
        File.WriteAllText(Path.Combine(pages, "list.html"), "list");
        return root;
    }

    private static IReadOnlyDictionary<string, CollectionConfig> CreateCollections(int count)
    {
        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count; i++)
        {
            collections[$"collection-{i}"] = new CollectionConfig
            {
                Permalink = $"/collection-{i}/{{slug}}/",
                ListRoute = $"/collection-{i}/",
                ListTemplate = "pages/list.html"
            };
        }

        return collections;
    }

    private static SiteModel CreateSiteModel()
        => new()
        {
            Name = "site",
            Title = "site",
            BaseUrl = "/",
            Language = "en"
        };

    private static IReadOnlyList<(ContentDocument Item, RouteInfo Route)> CreateSource(int count)
    {
        var list = new List<(ContentDocument Item, RouteInfo Route)>(count);
        for (var i = 0; i < count; i++)
        {
            var item = ContentDocument.Create(
                id: $"id-{i}",
                title: $"Item {i}",
                slug: $"item-{i}",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                bodyKey: $"body-{i}");
            var route = new RouteInfo($"/posts/item-{i}/", $"posts/item-{i}/index.html", "pages/post.html");
            list.Add((item, route));
        }
        return list;
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

    private sealed class FirstCompletesThenBlocksRenderer : ITemplateRenderer
    {
        private readonly ManualResetEventSlim _release = new(initialState: false);
        private int _calls;

        public Task Blocked => _blocked.Task;

        private readonly TaskCompletionSource _blocked =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string RenderPage(string templateRelativePath, PageModel model) => string.Empty;

        public string RenderList(string templateRelativePath, ListPageModel model)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                return model.Page?.Url ?? string.Empty;
            }

            _blocked.TrySetResult();
            _release.Wait(TimeSpan.FromSeconds(10));
            return model.Page?.Url ?? string.Empty;
        }

        public void Release() => _release.Set();
    }

    private sealed class BlockingRenderer : ITemplateRenderer
    {
        private readonly ManualResetEventSlim _release = new(initialState: false);
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public string RenderPage(string templateRelativePath, PageModel model) => string.Empty;

        public string RenderList(string templateRelativePath, ListPageModel model)
        {
            _entered.TrySetResult();
            _release.Wait(TimeSpan.FromSeconds(10));
            return model.Page?.Url ?? string.Empty;
        }

        public void Release() => _release.Set();
    }

    private sealed class DeterministicRenderer : ITemplateRenderer
    {
        public string RenderPage(string templateRelativePath, PageModel model) => model.Page.Url;
        public string RenderList(string templateRelativePath, ListPageModel model) => model.Page?.Url ?? string.Empty;
    }
}
