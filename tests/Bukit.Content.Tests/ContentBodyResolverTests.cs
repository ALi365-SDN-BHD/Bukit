using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ContentBodyResolverTests
{
    [Fact]
    public void GetHtml_WithBodyStoreReturningContentBody_ReturnsHtml()
    {
        var store = new TestBodyStore(item => new ContentBody("<p>resolved body</p>"));
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: "test");

        var html = ContentBodyResolver.GetHtml(item, store);

        Assert.Equal("<p>resolved body</p>", html);
    }

    [Fact]
    public void GetHtml_WithContentHtml_ReturnsContentHtmlDirectly()
    {
        var store = new TestBodyStore(_ => new ContentBody("<p>should not be used</p>"));
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>inlined content</p>",
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: null);

        var html = ContentBodyResolver.GetHtml(item, store);

        Assert.Equal("<p>inlined content</p>", html);
    }

    [Fact]
    public async Task GetHtmlAsync_WithContentHtml_ReturnsContentHtmlDirectly()
    {
        var store = new TestBodyStore(_ => new ContentBody("<p>should not be used</p>"));
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>async inline</p>",
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: null);

        var html = await ContentBodyResolver.GetHtmlAsync(item, store);

        Assert.Equal("<p>async inline</p>", html);
    }

    [Fact]
    public async Task GetHtmlAsync_DelegatesToBodyStore()
    {
        var store = new TestBodyStore(item => new ContentBody($"<p>{item.Id}</p>"));
        var item = new ContentItem(
            Id: "resolved",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: "resolved");

        var html = await ContentBodyResolver.GetHtmlAsync(item, store);

        Assert.Equal("<p>resolved</p>", html);
    }

    [Fact]
    public async Task GetHtmlAsync_CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var store = new TestBodyStore(_ => new ContentBody("<p>body</p>"));
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: null);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ContentBodyResolver.GetHtmlAsync(item, store, cts.Token));
    }

    private sealed class TestBodyStore : IContentBodyStore
    {
        private readonly Func<ContentItem, ContentBody> _factory;

        public TestBodyStore(Func<ContentItem, ContentBody> factory)
        {
            _factory = factory;
        }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_factory(item));
        }
    }
}
