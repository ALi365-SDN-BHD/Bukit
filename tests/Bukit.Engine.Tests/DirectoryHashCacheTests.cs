using Bukit.Engine.Incremental;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DirectoryHashCacheTests
{
    private static readonly ILogger Logger = new ConsoleLogger(LogLevel.Warn);
    [Fact]
    public void GetOrAdd_ReusesHashForSameDirectory()
    {
        var calls = 0;
        var cache = new DirectoryHashCache(Logger, path =>
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
        var cache = new DirectoryHashCache(Logger, path =>
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
    public void GetOrAdd_OverBudget_DoesNotCacheIncompleteHashAndCanRecover()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-dhc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            for (var i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file-{i:D4}.txt"), $"content-{i}");
            }

            var cache = new DirectoryHashCache(Logger, maxFiles: 5, maxTotalSize: 1024);
            Assert.ThrowsAny<InvalidOperationException>(() => cache.GetOrAdd(tempDir));
            for (var i = 5; i < 10; i++) File.Delete(Path.Combine(tempDir, $"file-{i:D4}.txt"));

            var result = cache.GetOrAdd(tempDir);
            Assert.NotNull(result);
            Assert.Equal(64, result.Length);
            Assert.Equal(HashUtil.Sha256HexForDirectory(tempDir, Logger, 5, 1024), result);

            var result2 = cache.GetOrAdd(tempDir);
            Assert.Equal(result, result2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetOrAdd_OperationalFactoryFailure_PropagatesAndIsNotCached()
    {
        var calls = 0;
        var failure = new IOException("test I/O failure");
        var cache = new DirectoryHashCache(Logger, _ => ++calls == 1 ? throw failure : "complete");

        Assert.Same(failure, Record.Exception(() => cache.GetOrAdd("layouts")));
        Assert.Equal("complete", cache.GetOrAdd("layouts"));
        Assert.Equal("complete", cache.GetOrAdd("layouts"));
        Assert.Equal(2, calls);
    }
}
