using System.Text;
using System.Text.Json;
using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class SyncCacheManagerTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _cacheDir;
    private readonly string _cachePath;
    private readonly ILogger _logger = new ConsoleLogger(LogLevel.Error);

    public SyncCacheManagerTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-cache-tests-" + Guid.NewGuid().ToString("N"));
        _cacheDir = Path.Combine(_rootDir, ".cache", "wechat-sync");
        _cachePath = Path.Combine(_cacheDir, "sync-cache.json");
        Directory.CreateDirectory(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void LoadCache_MissingFileReturnsEmptyV3Cache()
    {
        var cache = SyncCacheManager.LoadCache(_cachePath, _logger);

        Assert.Equal(3, cache.Version);
        Assert.Empty(cache.Records);
        Assert.Empty(cache.ThumbMediaIds);
        AssertOperations(cache, expectedCount: 0);
    }

    [Fact]
    public void LoadCache_MigratesV2AndPreservesRecordsAndThumbsWithOrdinalKeys()
    {
        File.WriteAllText(_cachePath, """
        {
          "Version": 2,
          "Records": {
            "Notion:Page-1": {
              "LastSuccessAt": "2026-07-21T00:00:00+00:00",
              "WechatDraftId": "draft-1",
              "ContentHash": "hash-1",
              "SourceKey": "notion",
              "SourceId": "page-1",
              "Title": "Title"
            }
          },
          "ThumbMediaIds": {
            "HTTPS://EXAMPLE.COM/COVER.PNG": "thumb-1"
          }
        }
        """);

        var cache = SyncCacheManager.LoadCache(_cachePath, _logger);

        Assert.Equal(3, cache.Version);
        Assert.Equal("draft-1", cache.Records["Notion:Page-1"].WechatDraftId);
        Assert.False(cache.Records.ContainsKey("notion:page-1"));
        Assert.Equal("thumb-1", cache.ThumbMediaIds["HTTPS://EXAMPLE.COM/COVER.PNG"]);
        Assert.False(cache.ThumbMediaIds.ContainsKey("https://example.com/cover.png"));
        AssertOperations(cache, expectedCount: 0);
    }

    [Fact]
    public void LoadCache_LoadsValidV3OperationsWithoutChangingTheirShape()
    {
        File.WriteAllText(_cachePath, """
        {
          "Version": 3,
          "Records": {},
          "ThumbMediaIds": {},
          "Operations": {
            "Notion:Page-1": {
              "State": "DraftCreated",
              "ContentHash": "hash-1",
              "Target": "publish",
              "DraftId": "draft-1",
              "PublishId": null,
              "UpdatedAt": "2026-07-21T00:00:00+00:00"
            }
          }
        }
        """);

        var cache = SyncCacheManager.LoadCache(_cachePath, _logger);

        Assert.Equal(3, cache.Version);
        var serialized = JsonSerializer.Serialize(cache, WechatSyncJsonContext.Default.SyncCache);
        using var document = JsonDocument.Parse(serialized);
        var operation = document.RootElement.GetProperty("Operations").GetProperty("Notion:Page-1");
        Assert.Equal("DraftCreated", operation.GetProperty("State").GetString());
        Assert.Equal("draft-1", operation.GetProperty("DraftId").GetString());
    }

    [Fact]
    public void LoadCache_CorruptJsonFailsClosedAndPreservesOriginalBytes()
    {
        var original = Encoding.UTF8.GetBytes("{ \"Version\": 3, \"Records\":");
        File.WriteAllBytes(_cachePath, original);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SyncCacheManager.LoadCache(_cachePath, _logger));

        Assert.Contains("repair or remove", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(_cachePath));
    }

    [Theory]
    [InlineData("{\"Version\":1,\"Records\":{},\"ThumbMediaIds\":{}}")]
    [InlineData("{\"Version\":4,\"Records\":{},\"ThumbMediaIds\":{},\"Operations\":{}}")]
    [InlineData("{\"Version\":3,\"Records\":null,\"ThumbMediaIds\":{},\"Operations\":{}}")]
    [InlineData("{\"Version\":3,\"Records\":{},\"ThumbMediaIds\":null,\"Operations\":{}}")]
    [InlineData("{\"Version\":3,\"Records\":{},\"ThumbMediaIds\":{},\"Operations\":null}")]
    public void LoadCache_UnsupportedOrStructurallyInvalidCacheFailsClosed(string json)
    {
        var original = Encoding.UTF8.GetBytes(json);
        File.WriteAllBytes(_cachePath, original);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SyncCacheManager.LoadCache(_cachePath, _logger));

        Assert.Contains("repair or remove", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(_cachePath));
    }

    [Fact]
    public void SaveCache_AtomicallyReplacesAnOpenReadableCacheAndLeavesNoTempFile()
    {
        File.WriteAllText(_cachePath, "{\"Version\":2,\"Records\":{},\"ThumbMediaIds\":{}}");
        using var originalReader = new FileStream(
            _cachePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete);
        var cache = new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            ThumbMediaIds = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["cover"] = "thumb-1"
            }
        };

        SyncCacheManager.SaveCache(_cachePath, cache);

        var loaded = SyncCacheManager.LoadCache(_cachePath, _logger);
        Assert.Equal(3, loaded.Version);
        Assert.Equal("thumb-1", loaded.ThumbMediaIds["cover"]);
        Assert.Empty(Directory.GetFiles(_cacheDir, ".sync-cache.json.*.tmp"));
    }

    [Fact]
    public void SaveCache_RejectsCacheDirectorySymlinkSwapBeforeCreatingTempFile()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-temp-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        Directory.Delete(_cacheDir, recursive: true);
        try
        {
            Directory.CreateSymbolicLink(_cacheDir, outsideDir);
            var cache = new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal));

            Assert.Throws<InvalidOperationException>(() => SyncCacheManager.SaveCache(_cachePath, cache));

            Assert.Empty(Directory.GetFiles(outsideDir));
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public void SaveCache_WhenAtomicMoveFails_CleansOnlyItsOwnedTempFile()
    {
        Directory.CreateDirectory(_cachePath);
        var unownedTemp = Path.Combine(_cacheDir, ".sync-cache.json.unowned.tmp");
        File.WriteAllText(unownedTemp, "sentinel");
        var cache = new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal));

        Assert.ThrowsAny<IOException>(() => SyncCacheManager.SaveCache(_cachePath, cache));

        Assert.True(Directory.Exists(_cachePath));
        Assert.Equal("sentinel", File.ReadAllText(unownedTemp));
        Assert.Equal([unownedTemp], Directory.GetFiles(_cacheDir, ".sync-cache.json.*.tmp"));
    }

    [Fact]
    public void ResolvePath_RejectsCacheFileSymlinkEscapingWechatSyncRoot()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-cache-file-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outsideCache = Path.Combine(outsideDir, "outside.json");
            File.WriteAllText(outsideCache, "{}");
            File.CreateSymbolicLink(_cachePath, outsideCache);

            Assert.Throws<InvalidOperationException>(() =>
                SyncCacheManager.ResolvePath(_rootDir, ".cache/wechat-sync/sync-cache.json"));
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePath_RejectsDanglingCacheFileSymlinkEscapingWechatSyncRoot()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-dangling-cache-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outsideCache = Path.Combine(outsideDir, "not-created.json");
            File.CreateSymbolicLink(_cachePath, outsideCache);

            Assert.Throws<InvalidOperationException>(() =>
                SyncCacheManager.ResolvePath(_rootDir, ".cache/wechat-sync/sync-cache.json"));

            Assert.False(File.Exists(outsideCache));
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    private static void AssertOperations(SyncCache cache, int expectedCount)
    {
        var serialized = JsonSerializer.Serialize(cache, WechatSyncJsonContext.Default.SyncCache);
        using var document = JsonDocument.Parse(serialized);
        var operations = document.RootElement.GetProperty("Operations");
        Assert.Equal(JsonValueKind.Object, operations.ValueKind);
        Assert.Equal(expectedCount, operations.EnumerateObject().Count());
    }
}
