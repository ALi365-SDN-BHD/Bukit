using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class CompositeContentProviderTests
{
    [Fact]
    public async Task LoadAsync_ErrorFromOneProvider_DoesNotBreakOthers()
    {
        var goodProvider = new TestProvider(new ContentLoadResult(
            new[]
            {
                new ContentItem(
                    Id: "good-1",
                    Title: "Good",
                    Slug: "good",
                    PublishAt: DateTimeOffset.UtcNow,
                    ContentHtml: null,
                    Meta: new Dictionary<string, object>(),
                    Fields: null)
            },
            new NullBodyStore()));

        var failingProvider = new FailingProvider(new ContentException("provider failed"));

        var composite = new CompositeContentProvider(new[]
        {
            ("src1", "content", (IContentProvider)goodProvider),
            ("src2", "content", (IContentProvider)failingProvider)
        });

        await Assert.ThrowsAsync<ContentException>(() =>
            composite.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_WithCancellationToken_Propagates()
    {
        var provider = new TestProvider(new ContentLoadResult(
            Array.Empty<ContentItem>(),
            new NullBodyStore()));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var composite = new CompositeContentProvider(new[]
        {
            ("src1", "content", (IContentProvider)provider)
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            composite.LoadAsync(cts.Token));
    }

    [Fact]
    public async Task LoadAsync_MultipleProviders_AssignsSourceKeys()
    {
        var p1 = new TestProvider(new ContentLoadResult(
            new[]
            {
                new ContentItem(
                    Id: "item-1",
                    Title: "Item 1",
                    Slug: "item-1",
                    PublishAt: DateTimeOffset.UtcNow,
                    ContentHtml: null,
                    Meta: new Dictionary<string, object>(),
                    Fields: null)
            },
            new NullBodyStore()));

        var p2 = new TestProvider(new ContentLoadResult(
            new[]
            {
                new ContentItem(
                    Id: "item-2",
                    Title: "Item 2",
                    Slug: "item-2",
                    PublishAt: DateTimeOffset.UtcNow,
                    ContentHtml: null,
                    Meta: new Dictionary<string, object>(),
                    Fields: null)
            },
            new NullBodyStore()));

        var composite = new CompositeContentProvider(new[]
        {
            ("notion", "content", (IContentProvider)p1),
            ("markdown", "content", (IContentProvider)p2)
        });

        var result = await composite.LoadAsync();

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("notion:item-1", result.Items[0].Id);
        Assert.Equal("markdown:item-2", result.Items[1].Id);

        Assert.True(result.Items[0].Meta.TryGetValue("sourceKey", out var srcKey1));
        Assert.Equal("notion", srcKey1);

        Assert.True(result.Items[1].Meta.TryGetValue("sourceKey", out var srcKey2));
        Assert.Equal("markdown", srcKey2);
    }

    [Fact]
    public async Task LoadAsync_MultipleProvidersWithSameSource_MergeItems()
    {
        var p1 = new TestProvider(new ContentLoadResult(
            new[]
            {
                new ContentItem(
                    Id: "a",
                    Title: "A",
                    Slug: "a",
                    PublishAt: DateTimeOffset.UtcNow,
                    ContentHtml: null,
                    Meta: new Dictionary<string, object>(),
                    Fields: null)
            },
            new NullBodyStore()));

        var p2 = new TestProvider(new ContentLoadResult(
            new[]
            {
                new ContentItem(
                    Id: "b",
                    Title: "B",
                    Slug: "b",
                    PublishAt: DateTimeOffset.UtcNow,
                    ContentHtml: null,
                    Meta: new Dictionary<string, object>(),
                    Fields: null)
            },
            new NullBodyStore()));

        var composite = new CompositeContentProvider(new[]
        {
            ("notion", "content", (IContentProvider)p1),
            ("notion", "content", (IContentProvider)p2)
        });

        var result = await composite.LoadAsync();

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("notion:a", result.Items[0].Id);
        Assert.Equal("notion:b", result.Items[1].Id);
    }

    [Fact]
    public async Task LoadAsync_EmptyProviderList_ReturnsEmpty()
    {
        var composite = new CompositeContentProvider(Array.Empty<(string, string, IContentProvider)>());

        var result = await composite.LoadAsync();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.NotNull(result.BodyStore);
    }

    private sealed class TestProvider : IContentProvider
    {
        private readonly ContentLoadResult _result;

        public TestProvider(ContentLoadResult result)
        {
            _result = result;
        }

        public Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }
    }

    private sealed class FailingProvider : IContentProvider
    {
        private readonly Exception _exception;

        public FailingProvider(Exception exception)
        {
            _exception = exception;
        }

        public Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromException<ContentLoadResult>(_exception);
        }
    }

    private sealed class NullBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentBody(string.Empty));
        }
    }
}
