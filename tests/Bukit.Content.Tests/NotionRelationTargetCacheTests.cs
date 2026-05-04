using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionRelationTargetCacheTests
{
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
}
