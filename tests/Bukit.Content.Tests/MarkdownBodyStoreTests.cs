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
        var item = ContentDocument.Create(
            id: "test",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>inlined content</p>",
            fields: null,
            bodyKey: null);

        var body = await store.GetAsync(item.ToDocument());

        Assert.Equal("<p>inlined content</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_WithoutBodyKey_ThrowsInvalidOperationException()
    {
        var store = new MarkdownBodyStore();
        var item = ContentDocument.Create(
            id: "test",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(item.ToDocument()));
    }

    [Fact]
    public async Task GetAsync_WithEmptyBodyKey_ThrowsInvalidOperationException()
    {
        var store = new MarkdownBodyStore();
        var item = ContentDocument.Create(
            id: "test",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "   ");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(item.ToDocument()));
    }

    [Fact]
    public async Task GetAsync_FileNotFound_Throws()
    {
        var store = new MarkdownBodyStore();
        var item = ContentDocument.Create(
            id: "test",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: Path.Combine(Path.GetTempPath(), "nonexistent_markdown_file_test.md"));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            store.GetAsync(item.ToDocument()));
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_Propagates()
    {
        var store = new MarkdownBodyStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var item = ContentDocument.Create(
            id: "test",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "some-file");

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.GetAsync(item.ToDocument(), cts.Token));
    }
}
