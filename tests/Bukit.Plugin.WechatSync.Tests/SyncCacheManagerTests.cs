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
        _rootDir = Path.Combine(AppContext.BaseDirectory, "bukit-wechat-cache-tests-" + Guid.NewGuid().ToString("N"));
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
    public void LoadCache_MigratesV2AndExactlyPreservesHistoricallyAllowedEmptyMetadata()
    {
        File.WriteAllText(_cachePath, """
        {
          "Version": 2,
          "Records": {
            "item-1": {
              "LastSuccessAt": "2026-07-21T00:00:00+00:00",
              "WechatDraftId": "draft-1",
              "ContentHash": "hash-1",
              "SourceKey": "",
              "SourceId": "",
              "Title": ""
            }
          },
          "ThumbMediaIds": {}
        }
        """);

        var cache = SyncCacheManager.LoadCache(_cachePath, _logger);

        var record = cache.Records["item-1"];
        Assert.Equal(string.Empty, record.SourceKey);
        Assert.Equal(string.Empty, record.SourceId);
        Assert.Equal(string.Empty, record.Title);
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
              "UpdatedAt": "2026-07-21T00:00:00+00:00",
              "SourceKey": "notion",
              "SourceId": "page-1",
              "Title": "Title",
              "LastPublishStatus": null
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
        Assert.Equal("notion", operation.GetProperty("SourceKey").GetString());
        Assert.Equal("page-1", operation.GetProperty("SourceId").GetString());
        Assert.Equal("Title", operation.GetProperty("Title").GetString());
        Assert.Equal(JsonValueKind.Null, operation.GetProperty("LastPublishStatus").ValueKind);
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

    [Theory]
    [MemberData(nameof(InvalidRecordDocuments))]
    [MemberData(nameof(InvalidOperationDocuments))]
    public void LoadCache_InvalidRequiredFieldsOrOperationInvariantsFailClosed(string json)
    {
        var original = Encoding.UTF8.GetBytes(json);
        File.WriteAllBytes(_cachePath, original);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SyncCacheManager.LoadCache(_cachePath, _logger));

        Assert.Contains("repair or remove", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(_cachePath));
    }

    public static IEnumerable<object[]> InvalidRecordDocuments()
    {
        const string validRecord = """
        {
          "LastSuccessAt": "2026-07-21T00:00:00+00:00",
          "WechatDraftId": "draft-1",
          "ContentHash": "hash-1",
          "SourceKey": "notion",
          "SourceId": "page-1",
          "Title": "Title"
        }
        """;

        yield return InvalidRecord(" ", validRecord);
        yield return InvalidRecord("key", validRecord.Replace("2026-07-21T00:00:00+00:00", "0001-01-01T00:00:00+00:00"));
        yield return InvalidRecord("key", validRecord.Replace("draft-1", " "));
        yield return InvalidRecord("key", validRecord.Replace("hash-1", " "));
        yield return InvalidRecord("key", validRecord.Replace("\"SourceKey\": \"notion\",", ""));
        yield return InvalidRecord("key", validRecord.Replace("\"SourceId\": \"page-1\",", ""));
        yield return InvalidRecord("key", validRecord.Replace("\"Title\": \"Title\"", "\"Title\": null"));
        yield return ["""{"Version":3,"Records":{},"ThumbMediaIds":{" ":"thumb-1"},"Operations":{}}"""];
        yield return ["""{"Version":3,"Records":{},"ThumbMediaIds":{"cover":" "},"Operations":{}}"""];
    }

    public static IEnumerable<object[]> InvalidOperationDocuments()
    {
        yield return InvalidOperation("Unknown", "publish", null, null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftSubmitting", "unknown", null, null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftSubmitting", "draft", null, null, "0001-01-01T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftSubmitting", "draft", null, null, "2026-07-21T00:00:00+00:00", " ");
        yield return InvalidOperation("DraftSubmitting", "draft", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftCreated", "draft", null, null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftCreated", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishSubmitting", "draft", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishSubmitting", "publish", null, null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishSubmitted", "publish", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishFailed", "publish", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1", lastPublishStatus: 2);
        yield return InvalidOperation("DraftCreated", "draft", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1", sourceKey: null);
        yield return InvalidOperation("DraftCreated", "draft", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1", sourceId: null);
        yield return InvalidOperation("DraftCreated", "draft", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1", title: null);
        yield return InvalidOperation("DraftSubmitting", "draft", null, null, "2026-07-21T00:00:00+00:00", "hash-1", lastPublishStatus: 1);
        yield return InvalidOperation("PublishSubmitted", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1", lastPublishStatus: 2);
        yield return InvalidOperation("PublishFailed", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishFailed", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1", lastPublishStatus: 7);
    }

    [Theory]
    [MemberData(nameof(ValidOperationDocuments))]
    public void LoadCache_AcceptsEverySupportedOperationStateAndTargetCombination(string json)
    {
        File.WriteAllText(_cachePath, json);

        var cache = SyncCacheManager.LoadCache(_cachePath, _logger);

        AssertOperations(cache, expectedCount: 1);
    }

    public static IEnumerable<object[]> ValidOperationDocuments()
    {
        yield return InvalidOperation("DraftSubmitting", "draft", null, null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftSubmitting", "publish", null, null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftCreated", "draft", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("DraftCreated", "publish", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishSubmitting", "publish", "draft-1", null, "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishSubmitted", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1");
        yield return InvalidOperation("PublishSubmitted", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1", lastPublishStatus: 1);
        yield return InvalidOperation("PublishFailed", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1", lastPublishStatus: 2);
        yield return InvalidOperation("PublishFailed", "publish", "draft-1", "publish-1", "2026-07-21T00:00:00+00:00", "hash-1", lastPublishStatus: 6);
    }

    private static object[] InvalidRecord(string key, string record)
        => [$"{{\"Version\":3,\"Records\":{{{JsonSerializer.Serialize(key)}:{record}}},\"ThumbMediaIds\":{{}},\"Operations\":{{}}}}"];

    private static object[] InvalidOperation(
        string state,
        string target,
        string? draftId,
        string? publishId,
        string updatedAt,
        string contentHash,
        string? sourceKey = "notion",
        string? sourceId = "page-1",
        string? title = "Title",
        int? lastPublishStatus = null)
        => [$$"""
        {
          "Version": 3,
          "Records": {},
          "ThumbMediaIds": {},
          "Operations": {
            "key": {
              "State": "{{state}}",
              "ContentHash": "{{contentHash}}",
              "Target": "{{target}}",
              "DraftId": {{JsonSerializer.Serialize(draftId)}},
              "PublishId": {{JsonSerializer.Serialize(publishId)}},
              "UpdatedAt": "{{updatedAt}}",
              "SourceKey": {{JsonSerializer.Serialize(sourceKey)}},
              "SourceId": {{JsonSerializer.Serialize(sourceId)}},
              "Title": {{JsonSerializer.Serialize(title)}},
              "LastPublishStatus": {{JsonSerializer.Serialize(lastPublishStatus)}}
            }
          }
        }
        """];

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
    public void FlushParentDirectoryMetadata_SucceedsForCacheDirectoryOnCurrentPlatform()
    {
        if (!OperatingSystem.IsWindows())
        {
            SyncCacheManager.FlushParentDirectoryMetadata(_cacheDir);
        }
    }

    [Fact]
    public void CommitAtomicReplacement_ReplacesDestinationWithDurablePlatformOperation()
    {
        var tempPath = Path.Combine(_cacheDir, ".replacement.tmp");
        var destinationPath = Path.Combine(_cacheDir, "replacement.json");
        File.WriteAllText(tempPath, "new");
        File.WriteAllText(destinationPath, "old");

        SyncCacheManager.CommitAtomicReplacement(tempPath, destinationPath);

        Assert.False(File.Exists(tempPath));
        Assert.Equal("new", File.ReadAllText(destinationPath));
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

    [Fact]
    public void ResolvePath_RejectsInRootCacheFileSymlinkAlias()
    {
        var realCache = Path.Combine(_cacheDir, "real.json");
        File.WriteAllText(realCache, "{\"Version\":3,\"Records\":{},\"ThumbMediaIds\":{},\"Operations\":{}}");
        var aliasCache = Path.Combine(_cacheDir, "alias.json");
        File.CreateSymbolicLink(aliasCache, realCache);

        Assert.True(PathUtils.IsSameOrSubPathOf(aliasCache, _rootDir));
        Assert.True(PathUtils.IsSubPathOf(aliasCache, _cacheDir));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SyncCacheManager.ResolvePath(_rootDir, ".cache/wechat-sync/alias.json"));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(realCache));
    }

    [Fact]
    public void ResolvePath_RejectsInRootDirectorySymlinkAlias()
    {
        var realDir = Path.Combine(_cacheDir, "real");
        Directory.CreateDirectory(realDir);
        var aliasDir = Path.Combine(_cacheDir, "alias");
        Directory.CreateSymbolicLink(aliasDir, realDir);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SyncCacheManager.ResolvePath(_rootDir, ".cache/wechat-sync/alias/cache.json"));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePath_RejectsProjectRootSymlinkAlias()
    {
        var realRoot = Path.Combine(_rootDir, "real-project");
        Directory.CreateDirectory(Path.Combine(realRoot, ".cache", "wechat-sync"));
        var rootAlias = Path.Combine(_rootDir, "project-alias");
        Directory.CreateSymbolicLink(rootAlias, realRoot);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SyncCacheManager.ResolvePath(rootAlias, ".cache/wechat-sync/cache.json"));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
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
