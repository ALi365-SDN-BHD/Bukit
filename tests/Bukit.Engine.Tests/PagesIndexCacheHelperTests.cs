using Xunit;
using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for PagesIndexCacheHelper cache load/save and mode/path resolution.
/// </summary>
public sealed class PagesIndexCacheHelperTests : IDisposable
{
    private readonly string _testDir;

    public PagesIndexCacheHelperTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-pic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_testDir, recursive: true);
    }

    // ── TryLoadCache ────────────────────────────────────────────────

    [Fact]
    public void TryLoadCache_MissingFile_ReturnsNull()
    {
        var result = PagesIndexCacheHelper.TryLoadCache(Path.Combine(_testDir, "missing.json"));
        Assert.Null(result);
    }

    [Fact]
    public void TryLoadCache_ValidJson_ReturnsDictionary()
    {
        var path = Path.Combine(_testDir, "cache.json");
        File.WriteAllText(path, """{"page1": {"title": "Hello", "count": 42}}""");

        var result = PagesIndexCacheHelper.TryLoadCache(path);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("page1"));
    }

    [Fact]
    public void TryLoadCache_InvalidJson_ReturnsNull()
    {
        var path = Path.Combine(_testDir, "bad.json");
        File.WriteAllText(path, "not valid json");

        var result = PagesIndexCacheHelper.TryLoadCache(path);

        Assert.Null(result);
    }

    [Fact]
    public void TryLoadCache_NonObjectRoot_ReturnsNull()
    {
        var path = Path.Combine(_testDir, "array.json");
        File.WriteAllText(path, "[1,2,3]");

        var result = PagesIndexCacheHelper.TryLoadCache(path);

        Assert.Null(result);
    }

    // ── TrySaveCache ────────────────────────────────────────────────

    [Fact]
    public void TrySaveCache_WithExternalUrl_SavesEntry()
    {
        var path = Path.Combine(_testDir, "out.json");
        var index = new Dictionary<string, object>
        {
            ["page1"] = new Dictionary<string, object>
            {
                ["url"] = "",
                ["external_url"] = "https://example.com/page1"
            }
        };

        PagesIndexCacheHelper.TrySaveCache(path, index);

        Assert.True(File.Exists(path));
        var loaded = PagesIndexCacheHelper.TryLoadCache(path);
        Assert.NotNull(loaded);
        Assert.True(loaded!.ContainsKey("page1"));
    }

    [Fact]
    public void TrySaveCache_WithoutExternalUrl_SkipsEntry()
    {
        var path = Path.Combine(_testDir, "out.json");
        var index = new Dictionary<string, object>
        {
            ["page1"] = new Dictionary<string, object>
            {
                ["url"] = "/page1/"
            }
        };

        PagesIndexCacheHelper.TrySaveCache(path, index);

        var loaded = PagesIndexCacheHelper.TryLoadCache(path);
        Assert.NotNull(loaded);
        Assert.Empty(loaded!);
    }

    [Fact]
    public void TrySaveCache_NonDictionaryValue_SkipsEntry()
    {
        var path = Path.Combine(_testDir, "out.json");
        var index = new Dictionary<string, object>
        {
            ["page1"] = "not-a-dictionary"
        };

        PagesIndexCacheHelper.TrySaveCache(path, index);

        var loaded = PagesIndexCacheHelper.TryLoadCache(path);
        Assert.NotNull(loaded);
        Assert.Empty(loaded!);
    }

    // ── NormalizeCacheMode ──────────────────────────────────────────

    [Theory]
    [InlineData("readonly", "readonly")]
    [InlineData("READONLY", "readonly")]
    [InlineData("readwrite", "readwrite")]
    [InlineData("READWRITE", "readwrite")]
    [InlineData("off", "off")]
    [InlineData("", "off")]
    [InlineData(null, "off")]
    [InlineData("other", "off")]
    public void NormalizeCacheMode_VariousInputs(string? mode, string expected)
    {
        Assert.Equal(expected, PagesIndexCacheHelper.NormalizeCacheMode(mode!));
    }

    // ── ResolveCachePath ────────────────────────────────────────────

    [Fact]
    public void ResolveCachePath_NullConfigured_ReturnsDefault()
    {
        var result = PagesIndexCacheHelper.ResolveCachePath(_testDir, null);
        Assert.Equal(Path.Combine(_testDir, ".cache", "notion", "pages-index.json"), result);
    }

    [Fact]
    public void ResolveCachePath_RelativeConfigured_CombinesWithRoot()
    {
        var result = PagesIndexCacheHelper.ResolveCachePath(_testDir, "custom/cache.json");
        Assert.Equal(Path.Combine(_testDir, "custom", "cache.json"), result);
    }

    [Fact]
    public void ResolveCachePath_AbsoluteConfigured_ReturnsAsIs()
    {
        var absolute = Path.Combine(_testDir, "absolute.json");
        var result = PagesIndexCacheHelper.ResolveCachePath(_testDir, absolute);
        Assert.Equal(absolute, result);
    }
}
