using Bukit.Engine.Abstractions.Content;
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

        Assert.Equal("notion", result.Items[0].Fields!["sourceKey"].Value);

        Assert.Equal("markdown", result.Items[1].Fields!["sourceKey"].Value);
    }

    [Fact]
    public async Task LoadAsync_SourceCollectionMapping_SetsCollectionAndDuplicatesAdditionalCollections()
    {
        var provider = new TestProvider(new ContentLoadResult(
            new[]
            {
                new ContentItem(
                    Id: "company-1",
                    Title: "Company 1",
                    Slug: "company-1",
                    PublishAt: DateTimeOffset.UtcNow,
                    ContentHtml: null,
                    Fields: null)
            },
            new NullBodyStore()));

        (string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)[] providers =
        {
            ("companies-db", "content", "companies", (IReadOnlyList<string>)new[] { "china_companies", "malaysia_companies" }, (IContentProvider)provider)
        };
        var composite = new CompositeContentProvider(providers);

        var result = await composite.LoadAsync();

        Assert.Equal(3, result.Items.Count);
        Assert.Equal("companies-db:company-1", result.Items[0].Id);
        Assert.Equal("companies", result.Items[0].Fields!["collection"].Value);
        Assert.Equal("companies-db:company-1:china_companies", result.Items[1].Id);
        Assert.Equal("china_companies", result.Items[1].Fields!["collection"].Value);
        Assert.Equal("companies-db:company-1:malaysia_companies", result.Items[2].Id);
        Assert.Equal("malaysia_companies", result.Items[2].Fields!["collection"].Value);
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

    [Fact]
    public async Task LoadRawAsync_MultipleProviders_AssignsSourceInfoAndCollection()
    {
        var provider = new RawTestProvider(new RawContentLoadResult(
            new[]
            {
                new RawContentDocument(
                    SourceId: "item-1",
                    SourceKind: "markdown",
                    Title: "Item 1",
                    Slug: "item-1",
                    PublishedAt: DateTimeOffset.UtcNow,
                    Body: new RawBody(null, "item-1.md", "# Item", "Item"),
                    Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = new("text", "post")
                    },
                    Source: new ContentSourceInfo("markdown", null, "content/item-1.md", null, null, null, "loaded"),
                    CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase))
            },
            new NullBodyStore()));
        (string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)[] providers =
        {
            ("markdown", "content", "posts", null, provider)
        };
        var composite = new CompositeContentProvider(providers);

        var result = await ((IRawContentProvider)composite).LoadRawAsync();

        var raw = Assert.Single(result.Documents);
        Assert.Equal("markdown:item-1", raw.SourceId);
        Assert.Equal("markdown", raw.Source.SourceKey);
        Assert.Equal("markdown", raw.Source.Provider);
        Assert.Equal("posts", raw.Properties["collection"].Value);
        Assert.Equal("markdown:item-1.md", raw.Body.BodyKey);
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

    private sealed class RawTestProvider : IContentProvider, IRawContentProvider
    {
        private readonly RawContentLoadResult _rawResult;

        public RawTestProvider(RawContentLoadResult rawResult)
        {
            _rawResult = rawResult;
        }

        public Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentLoadResult(Array.Empty<ContentItem>(), _rawResult.BodyStore));
        }

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_rawResult);
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
