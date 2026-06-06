using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ContentBodyResolverTests
{
    [Fact]
    public void GetHtml_WithBodyStoreReturningContentBody_ReturnsHtml()
    {
        var store = new TestBodyStore(item => new ContentBody("<p>resolved body</p>"));
        var document = ContentDocument.Create("test", "Test", "test", DateTimeOffset.UtcNow, null, null, "test");

#pragma warning disable CS0618
        var html = ContentBodyResolver.GetHtml(document, store);
#pragma warning restore CS0618

        Assert.Equal("<p>resolved body</p>", html);
    }

    [Fact]
    public void GetHtml_WithContentHtml_ReturnsContentHtmlDirectly()
    {
        var store = new TestBodyStore(_ => new ContentBody("<p>should not be used</p>"));
        var document = ContentDocument.Create("test", "Test", "test", DateTimeOffset.UtcNow, "<p>inlined content</p>");

#pragma warning disable CS0618
        var html = ContentBodyResolver.GetHtml(document, store);
#pragma warning restore CS0618

        Assert.Equal("<p>inlined content</p>", html);
    }

    [Fact]
    public async Task GetHtmlAsync_WithContentHtml_ReturnsContentHtmlDirectly()
    {
        var store = new TestBodyStore(_ => new ContentBody("<p>should not be used</p>"));
        var document = ContentDocument.Create("test", "Test", "test", DateTimeOffset.UtcNow, "<p>async inline</p>");

        var html = await ContentBodyResolver.GetHtmlAsync(document, store);

        Assert.Equal("<p>async inline</p>", html);
    }

    [Fact]
    public async Task GetHtmlAsync_DelegatesToBodyStore()
    {
        var store = new TestBodyStore(item => new ContentBody($"<p>{item.Id}</p>"));
        var document = ContentDocument.Create("resolved", "Test", "test", DateTimeOffset.UtcNow, null, null, "resolved");

        var html = await ContentBodyResolver.GetHtmlAsync(document, store);

        Assert.Equal("<p>resolved</p>", html);
    }

    [Fact]
    public async Task GetHtmlAsync_CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var store = new TestBodyStore(_ => new ContentBody("<p>body</p>"));
        var document = ContentDocument.Create("test", "Test", "test", DateTimeOffset.UtcNow, null);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ContentBodyResolver.GetHtmlAsync(document, store, cts.Token));
    }

    private sealed class TestBodyStore : IContentBodyStore
    {
        private readonly Func<ContentDocument, ContentBody> _factory;

        public TestBodyStore(Func<ContentDocument, ContentBody> factory)
        {
            _factory = factory;
        }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_factory(item));
        }
    }
}
