using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Markdown;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Content.Tests;

public sealed class MarkdownBodyStoreTests
{
    [Fact]
    public async Task GetAsync_WithContentHtml_ReturnsContentHtmlDirectly()
    {
        var store = new MarkdownBodyStore(Path.GetTempPath());
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
        var store = new MarkdownBodyStore(Path.GetTempPath());
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
        var store = new MarkdownBodyStore(Path.GetTempPath());
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
        var root = Path.Combine(Path.GetTempPath(), "bukit-mdstore-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new MarkdownBodyStore(root);
            var item = ContentDocument.Create(
                id: "test",
                title: "Test",
                slug: "test",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: null,
                bodyKey: Path.Combine(root, "nonexistent_markdown_file_test.md"));

            await Assert.ThrowsAsync<IOException>(() => store.GetAsync(item.ToDocument()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_Propagates()
    {
        var store = new MarkdownBodyStore(Path.GetTempPath());
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

    [Fact]
    public async Task GetAsync_FileReplacedBySymlinkAfterEnumeration_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-mdstore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-mdstore-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "secret.md");
        await File.WriteAllTextAsync(outsideFile, "# secret");

        var candidate = Path.Combine(root, "post.md");
        await File.WriteAllTextAsync(candidate, "# real body");

        try
        {
            var store = new MarkdownBodyStore(root);
            var item = ContentDocument.Create(
                id: "post",
                title: "Post",
                slug: "post",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: null,
                bodyKey: candidate);

            // Enumeration validated the regular file; replace it before body load.
            File.Delete(candidate);
            try
            {
                File.CreateSymbolicLink(candidate, outsideFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
            }

            await Assert.ThrowsAsync<IOException>(() => store.GetAsync(item.ToDocument()));
        }
        finally
        {
            File.Delete(candidate);
            File.Delete(outsideFile);
            Directory.Delete(root, recursive: true);
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_WithChangedMarkdownBody_FailsBeforeReturningContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-markdown-body-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "page.md");
        const string original = "---\ntitle: Page\n---\n# Heading\n\nBody A";
        File.WriteAllText(file, original);
        try
        {
            var item = ContentDocument.Create(
                id: "page",
                title: "Page",
                slug: "page",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bodyFingerprint"] = MarkdownTextHelper.ComputeBodyFingerprint(original)
                }),
                bodyKey: file);
            var store = new MarkdownBodyStore(root);

            File.WriteAllText(file, "---\ntitle: Page\n---\n# Heading\n\nBody B");
            var exception = await Assert.ThrowsAsync<ContentException>(() => store.GetAsync(item.ToDocument()));

            Assert.Contains("changed", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Body B", exception.Message, StringComparison.Ordinal);

            File.WriteAllText(file, original);
            var body = await store.GetAsync(item.ToDocument());
            Assert.Contains("Heading", body.Html, StringComparison.Ordinal);
            Assert.Contains("Body A", body.Html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_WithoutCapturedFingerprint_KeepsLegacyRendering()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-markdown-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "legacy.md");
        File.WriteAllText(file, "---\ntitle: Legacy\n---\n# Legacy");
        try
        {
            var item = ContentDocument.Create(
                id: "legacy",
                title: "Legacy",
                slug: "legacy",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: null,
                bodyKey: file);

            var body = await new MarkdownBodyStore(root).GetAsync(item.ToDocument());

            Assert.Contains("Legacy", body.Html, StringComparison.Ordinal);
            Assert.DoesNotContain("title:", body.Html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
