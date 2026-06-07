using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class CompositeContentProviderTests
{
    [Fact]
    public async Task LoadAsync_ErrorFromOneProvider_DoesNotBreakOthers()
    {
        var goodProvider = new TestProvider(new RawContentLoadResult(
            new[]
            {
                Document("good-1", "Good", "good")
            },
            new NullBodyStore()));

        var failingProvider = new FailingProvider(new ContentException("provider failed"));

        var composite = new CompositeContentProvider(new[]
        {
            ("src1", "content", (IContentProvider)goodProvider),
            ("src2", "content", (IContentProvider)failingProvider)
        });

        await Assert.ThrowsAsync<ContentException>(() =>
            composite.LoadRawAsync());
    }

    [Fact]
    public async Task LoadAsync_WithCancellationToken_Propagates()
    {
        var provider = new TestProvider(new RawContentLoadResult(
            Array.Empty<RawContentDocument>(),
            new NullBodyStore()));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var composite = new CompositeContentProvider(new[]
        {
            ("src1", "content", (IContentProvider)provider)
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            composite.LoadRawAsync(cts.Token));
    }

    [Fact]
    public async Task LoadAsync_MultipleProviders_AssignsSourceKeys()
    {
        var p1 = new TestProvider(new RawContentLoadResult(
            new[]
            {
                Document("item-1", "Item 1", "item-1")
            },
            new NullBodyStore()));

        var p2 = new TestProvider(new RawContentLoadResult(
            new[]
            {
                Document("item-2", "Item 2", "item-2")
            },
            new NullBodyStore()));

        var composite = new CompositeContentProvider(new[]
        {
            ("notion", "content", (IContentProvider)p1),
            ("markdown", "content", (IContentProvider)p2)
        });

        var result = await composite.LoadRawAsync();

        Assert.Equal(2, result.Documents.Count);
        Assert.Equal("notion:item-1", result.Documents[0].Id);
        Assert.Equal("markdown:item-2", result.Documents[1].Id);

        Assert.Equal("notion", ContentFieldReader.GetText(result.Documents[0].CustomFields, "sourceKey"));

        Assert.Equal("markdown", ContentFieldReader.GetText(result.Documents[1].CustomFields, "sourceKey"));
    }

    [Fact]
    public async Task LoadAsync_SourceCollectionMapping_SetsCollectionAndDuplicatesAdditionalCollections()
    {
        var provider = new TestProvider(new RawContentLoadResult(
            new[]
            {
                Document("company-1", "Company 1", "company-1")
            },
            new NullBodyStore()));

        (string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)[] providers =
        {
            ("companies-db", "content", "companies", (IReadOnlyList<string>)new[] { "china_companies", "malaysia_companies" }, (IContentProvider)provider)
        };
        var composite = new CompositeContentProvider(providers);

        var result = await composite.LoadRawAsync();

        Assert.Equal(3, result.Documents.Count);
        Assert.Equal("companies-db:company-1", result.Documents[0].Id);
        Assert.Equal("companies", ContentFieldReader.GetText(result.Documents[0].CustomFields, "collection"));
        Assert.Equal("companies-db:company-1:china_companies", result.Documents[1].Id);
        Assert.Equal("china_companies", ContentFieldReader.GetText(result.Documents[1].CustomFields, "collection"));
        Assert.Equal("companies-db:company-1:malaysia_companies", result.Documents[2].Id);
        Assert.Equal("malaysia_companies", ContentFieldReader.GetText(result.Documents[2].CustomFields, "collection"));
    }

    [Fact]
    public async Task LoadAsync_MultipleProvidersWithSameSource_MergeItems()
    {
        var p1 = new TestProvider(new RawContentLoadResult(
            new[]
            {
                Document("a", "A", "a")
            },
            new NullBodyStore()));

        var p2 = new TestProvider(new RawContentLoadResult(
            new[]
            {
                Document("b", "B", "b")
            },
            new NullBodyStore()));

        var composite = new CompositeContentProvider(new[]
        {
            ("notion", "content", (IContentProvider)p1),
            ("notion", "content", (IContentProvider)p2)
        });

        var result = await composite.LoadRawAsync();

        Assert.Equal(2, result.Documents.Count);
        Assert.Equal("notion:a", result.Documents[0].Id);
        Assert.Equal("notion:b", result.Documents[1].Id);
    }

    [Fact]
    public async Task LoadAsync_EmptyProviderList_ReturnsEmpty()
    {
        var composite = new CompositeContentProvider(Array.Empty<(string, string, IContentProvider)>());

        var result = await composite.LoadRawAsync();

        Assert.NotNull(result);
        Assert.Empty(result.Documents);
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

        var result = await composite.LoadRawAsync();

        var raw = Assert.Single(result.Documents);
        Assert.Equal("markdown:item-1", raw.SourceId);
        Assert.Equal("markdown", raw.Source.SourceKey);
        Assert.Equal("markdown", raw.Source.Provider);
        var properties = Assert.IsAssignableFrom<IReadOnlyDictionary<string, RawContentValue>>(raw.Properties);
        Assert.Equal("posts", properties["collection"].Value);
        Assert.Equal("markdown:item-1.md", raw.Body.BodyKey);
    }

    private sealed class TestProvider : IContentProvider
    {
        private readonly RawContentLoadResult _result;

        public TestProvider(RawContentLoadResult result)
        {
            _result = result;
        }

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ToRawResult(_result));
        }
    }

    private sealed class RawTestProvider : IContentProvider
    {
        private readonly RawContentLoadResult _rawResult;

        public RawTestProvider(RawContentLoadResult rawResult)
        {
            _rawResult = rawResult;
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

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromException<RawContentLoadResult>(_exception);
        }
    }

    private static RawContentLoadResult ToRawResult(RawContentLoadResult result) => result;

    private static RawContentDocument Document(string id, string title, string slug)
        => new(
            Id: id,
            Title: title,
            Slug: slug,
            PublishAt: DateTimeOffset.UtcNow,
            Body: new RawBody());

    private sealed class NullBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentBody(string.Empty));
        }
    }
}
