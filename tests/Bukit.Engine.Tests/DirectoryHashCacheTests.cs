using Bukit.Engine.Incremental;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DirectoryHashCacheTests
{
    [Fact]
    public void GetOrAdd_ReusesHashForSameDirectory()
    {
        var calls = 0;
        var cache = new DirectoryHashCache(path =>
        {
            calls++;
            return $"hash:{path}";
        });

        var first = cache.GetOrAdd("e:/site/layouts");
        var second = cache.GetOrAdd("e:/site/layouts");

        Assert.Equal("hash:e:/site/layouts", first);
        Assert.Equal(first, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void GetOrAdd_ComputesHashSeparatelyForDifferentDirectories()
    {
        var calls = 0;
        var cache = new DirectoryHashCache(path =>
        {
            calls++;
            return $"hash:{path}";
        });

        var first = cache.GetOrAdd("e:/site/layouts");
        var second = cache.GetOrAdd("e:/site/layouts-blog");

        Assert.NotEqual(first, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void GetOrAdd_WithLargeDirectory_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-dhc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            for (var i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file-{i:D4}.txt"), $"content-{i}");
            }

            var cache = new DirectoryHashCache(maxFiles: 5, maxTotalSize: 1024);
            var result = cache.GetOrAdd(tempDir);
            Assert.NotNull(result);
            Assert.Equal(64, result.Length);

            var result2 = cache.GetOrAdd(tempDir);
            Assert.Equal(result, result2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
