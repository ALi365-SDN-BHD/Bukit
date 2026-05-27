using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Markdown;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class MarkdownBodyStoreTests
{
    [Fact]
    public async Task GetAsync_WithContentHtml_ReturnsContentHtmlDirectly()
    {
        var store = new MarkdownBodyStore();
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>inlined content</p>",
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: null);

        var body = await store.GetAsync(item);

        Assert.Equal("<p>inlined content</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_WithoutBodyKey_ThrowsInvalidOperationException()
    {
        var store = new MarkdownBodyStore();
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(item));
    }

    [Fact]
    public async Task GetAsync_WithEmptyBodyKey_ThrowsInvalidOperationException()
    {
        var store = new MarkdownBodyStore();
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: "   ");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(item));
    }

    [Fact]
    public async Task GetAsync_FileNotFound_Throws()
    {
        var store = new MarkdownBodyStore();
        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: Path.Combine(Path.GetTempPath(), "nonexistent_markdown_file_test.md"));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            store.GetAsync(item));
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_Propagates()
    {
        var store = new MarkdownBodyStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var item = new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: "some-file");

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.GetAsync(item, cts.Token));
    }
}
