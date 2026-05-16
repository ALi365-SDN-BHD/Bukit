using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionRelationTargetCacheTests
{
    [Fact]
    public void Create_ReturnsNull_WhenOffOrRootMissing()
    {
        Assert.Null(NotionRelationTargetCache.Create("off", Path.GetTempPath()));
        Assert.Null(NotionRelationTargetCache.Create("readwrite", ""));
    }

    [Fact]
    public async Task WriteAndTryRead_RoundTripsRelationTarget()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-relation-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        var cache = NotionRelationTargetCache.Create("readwrite", rootDir);
        var target = new RelationTargetInfo("page-1", "Visa", "visa", "page", "https://example.com/visa");

        await cache!.WriteAsync(target, CancellationToken.None);
        var cached = await cache.TryReadAsync("page-1", CancellationToken.None);

        Assert.NotNull(cached);
        Assert.Equal(target.PageId, cached!.PageId);
        Assert.Equal(target.Title, cached.Title);
        Assert.Equal(target.Slug, cached.Slug);
        Assert.Equal(target.Type, cached.Type);
        Assert.Equal(target.Url, cached.Url);
    }

    [Fact]
    public async Task TryReadAsync_ReturnsNull_WhenCacheFileIsInvalid()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-relation-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rootDir, "relations"));
        var cacheFile = Path.Combine(rootDir, "relations", "page-1.json");
        await File.WriteAllTextAsync(cacheFile, "{not-json}");
        var cache = NotionRelationTargetCache.Create("readwrite", rootDir);

        var cached = await cache!.TryReadAsync("page-1", CancellationToken.None);

        Assert.Null(cached);
    }

    [Fact]
    public async Task TryReadAsync_ReturnsNull_WhenFileMissingMalformedOrIncomplete()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-relation-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rootDir, "relations"));
        var cache = NotionRelationTargetCache.Create("readwrite", rootDir);

        Assert.Null(await cache!.TryReadAsync("missing", CancellationToken.None));

        await File.WriteAllTextAsync(Path.Combine(rootDir, "relations", "array.json"), "[]");
        await File.WriteAllTextAsync(Path.Combine(rootDir, "relations", "incomplete.json"), """{"pageId":"page-1","title":"Title","slug":"","type":"page"}""");

        Assert.Null(await cache.TryReadAsync("array", CancellationToken.None));
        Assert.Null(await cache.TryReadAsync("incomplete", CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_InReadonlyMode_DoesNotPersist()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-relation-cache-" + Guid.NewGuid().ToString("N"));
        var cache = NotionRelationTargetCache.Create("readonly", rootDir);
        var target = new RelationTargetInfo("page-1", "Visa", "visa", "page", null);

        await cache!.WriteAsync(target, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(rootDir, "relations", "page-1.json")));
    }

    [Fact]
    public async Task WriteAndTryRead_PreservesNullUrl()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-relation-cache-" + Guid.NewGuid().ToString("N"));
        var cache = NotionRelationTargetCache.Create("readwrite", rootDir);
        var target = new RelationTargetInfo("page-1", "Visa", "visa", "page", null);

        await cache!.WriteAsync(target, CancellationToken.None);
        var cached = await cache.TryReadAsync("page-1", CancellationToken.None);

        Assert.NotNull(cached);
        Assert.Null(cached!.Url);
    }
}
