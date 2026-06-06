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
}
