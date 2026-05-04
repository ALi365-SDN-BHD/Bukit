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
}
