using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class NotionReadonlyTransactionTests
{
    [Theory]
    [InlineData("readonly")]
    [InlineData("off")]
    public async Task CacheFactoriesDoNotCreateReadOnlyOrDisabledDirectories(string mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-readonly-" + Guid.NewGuid().ToString("N"));
        var pages = NotionCacheManager.CreatePageHtmlCache(new NotionContentSourceOptions { DatabaseId = "test", Token = "test", CacheDir = root, CacheMode = mode });
        var relations = NotionRelationTargetCache.Create(mode, root);
        Assert.False(Directory.Exists(root));
        if (relations is not null) Assert.Null(await relations.TryReadAsync("missing", default));
        Assert.False(Directory.Exists(root));
        if (mode == "off") Assert.Null(pages); else Assert.NotNull(pages);
    }
    [Fact]
    public void ReadonlyCacheDoesNotRequireWritableParent()
    {
        if (OperatingSystem.IsWindows()) return;
        var parent = Path.Combine(Path.GetTempPath(), "bukit-readonly-parent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var original = File.GetUnixFileMode(parent);
        try
        {
            File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var path = Path.Combine(parent, "missing");
            Assert.NotNull(NotionCacheManager.CreatePageHtmlCache(new() { DatabaseId = "test", Token = "test", CacheDir = path, CacheMode = "readonly" }));
            Assert.NotNull(NotionRelationTargetCache.Create("readonly", path));
            Assert.False(Directory.Exists(path));
        }
        finally { File.SetUnixFileMode(parent, original); Directory.Delete(parent, true); }
    }
}
