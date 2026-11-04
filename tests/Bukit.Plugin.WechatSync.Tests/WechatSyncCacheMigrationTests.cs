using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncCacheMigrationTests : IDisposable
{
    private const string SyncKey = "notion:page-1";
    private const string CacheFile = ".cache/wechat-sync/sync-cache.json";
    private readonly string _rootDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-wechat-cache-migration-" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _logger = new ConsoleLogger(LogLevel.Error);

    public WechatSyncCacheMigrationTests()
        => Directory.CreateDirectory(_rootDir);

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_MigratesSuccessRecordAndSkipsWithoutDuplicateDraft()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_SECRET", "secret-a");
        var context = Context();
        var options = Options(appId.Name, secret.Name);
        SeedLegacy(new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            Records =
            {
                [SyncKey] = new SyncRecord(
                    DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                    "old-draft",
                    CurrentHash(context, options),
                    "notion",
                    "page-1",
                    "Hello")
            },
            ThumbMediaIds = { ["old-thumb-key"] = "old-thumb-id" }
        });
        var gateway = new RecordingGateway();

        var result = await new WechatSyncWorkflow(gateway).RunAsync(context, options);

        Assert.True(result.Success);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Synced);
        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Equal(0, gateway.UploadThumbCount);
        var account = LoadAccount("app-a");
        Assert.Equal(4, account.Version);
        Assert.Equal("app-a", account.AppId);
        Assert.Equal("old-draft", account.Records[SyncKey].WechatDraftId);
        Assert.Equal("old-thumb-id", account.ThumbMediaIds["old-thumb-key"]);
        Assert.True(File.Exists(BasePath));
    }

    [Fact]
    public async Task RunAsync_MigratesDraftSubmittingAndKeepsRecoveryGuard()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_SECRET", "secret-a");
        var context = Context();
        var options = Options(appId.Name, secret.Name);
        SeedLegacy(new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            Operations =
            {
                [SyncKey] = new SyncOperation(
                    "DraftSubmitting",
                    CurrentHash(context, options),
                    "draft",
                    null,
                    null,
                    DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
                {
                    SourceKey = "notion",
                    SourceId = "page-1",
                    Title = "Hello"
                }
            }
        });
        var gateway = new RecordingGateway();

        var result = await new WechatSyncWorkflow(gateway).RunAsync(context, options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Equal(0, gateway.TotalCalls);
        var account = LoadAccount("app-a");
        Assert.Equal("DraftSubmitting", account.Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_MigratesPublishSubmittedAndDoesNotPublishAgain()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_SECRET", "secret-a");
        var context = Context();
        var options = Options(appId.Name, secret.Name) with { Target = "publish" };
        SeedLegacy(new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            Operations =
            {
                [SyncKey] = new SyncOperation(
                    "PublishSubmitted",
                    CurrentHash(context, options),
                    "publish",
                    "old-draft",
                    "old-publish",
                    DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
                {
                    SourceKey = "notion",
                    SourceId = "page-1",
                    Title = "Hello"
                }
            }
        });
        var gateway = new RecordingGateway();

        var result = await new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask).RunAsync(context, options);

        Assert.True(result.Success);
        Assert.Equal(1, result.Synced);
        Assert.Equal(0, gateway.PublishCount);
        Assert.Equal(1, gateway.CheckStatusCount);
        Assert.Equal(0, gateway.AddDraftCount);
    }

    [Fact]
    public async Task RunAsync_MigratesLegacyCacheEvenWhenNoCandidatesRemain()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_SECRET", "secret-a");
        var context = Context(reviewStatus: "needs-review");
        var options = Options(appId.Name, secret.Name);
        SeedLegacy(new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            Records =
            {
                [SyncKey] = new SyncRecord(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "old-draft", "old-hash", "notion", "page-1", "Hello")
            }
        });
        var gateway = new RecordingGateway();

        var result = await new WechatSyncWorkflow(gateway).RunAsync(context, options);

        Assert.True(result.Success);
        Assert.Equal(0, result.Candidates);
        Assert.Equal(0, gateway.TotalCalls);
        Assert.True(File.Exists(AccountPath("app-a")));
        Assert.Equal("old-draft", LoadAccount("app-a").Records[SyncKey].WechatDraftId);
        Assert.True(File.Exists(BasePath));
    }

    [Fact]
    public async Task RunAsync_RejectsMigrationWithoutMatchingAppIdConfirmation()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_SECRET", "secret-a");
        var context = Context();
        var options = Options(appId.Name, secret.Name) with { ConfirmedLegacyAppId = "app-b" };
        SeedLegacy(new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal)));
        var gateway = new RecordingGateway();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WechatSyncWorkflow(gateway).RunAsync(context, options));

        Assert.Contains("confirm", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(AccountPath("app-a")));
        Assert.True(File.Exists(BasePath));
        Assert.Equal(0, gateway.TotalCalls);
    }

    [Fact]
    public async Task RunAsync_RejectsLegacyCacheWithoutMigrationFlagAndDoesNotArchive()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_SECRET", "secret-a");
        var context = Context();
        var options = Options(appId.Name, secret.Name) with
        {
            MigrateLegacyCache = false,
            ConfirmedLegacyAppId = null
        };
        SeedLegacy(new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal)));
        var gateway = new RecordingGateway();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WechatSyncWorkflow(gateway).RunAsync(context, options));

        Assert.Contains("migrate", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archiving", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(AccountPath("app-a")));
        Assert.True(File.Exists(BasePath));
        Assert.Equal(0, gateway.TotalCalls);
    }

    [Fact]
    public async Task RunAsync_RejectsMigrationWhenLegacyAndAccountCachesBothExist()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_MIGRATION_SECRET", "secret-a");
        var context = Context();
        var options = Options(appId.Name, secret.Name);
        var hash = CurrentHash(context, options);
        SeedLegacy(new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            Records =
            {
                [SyncKey] = new SyncRecord(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "legacy-draft", hash, "notion", "page-1", "Hello")
            }
        });
        SyncCacheManager.SaveCache(AccountPath("app-a"), new SyncCache(
            4,
            new Dictionary<string, SyncRecord>(StringComparer.Ordinal)
            {
                [SyncKey] = new SyncRecord(DateTimeOffset.Parse("2026-02-01T00:00:00Z"), "account-draft", hash, "notion", "page-1", "Hello")
            })
        {
            AppId = "app-a"
        });
        var gateway = new RecordingGateway();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WechatSyncWorkflow(gateway).RunAsync(context, options));

        Assert.Contains("refusing to merge", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.TotalCalls);
        Assert.Equal("account-draft", LoadAccount("app-a").Records[SyncKey].WechatDraftId);
        Assert.Equal("legacy-draft", SyncCacheManager.LoadCache(BasePath, _logger).Records[SyncKey].WechatDraftId);
    }

    private string BasePath
        => SyncCacheManager.ResolvePath(_rootDir, CacheFile);

    private string AccountPath(string appId)
        => SyncCacheManager.ResolveAccountPath(BasePath, appId);

    private SyncCache LoadAccount(string appId)
        => SyncCacheManager.LoadCache(AccountPath(appId), _logger, appId);

    private void SeedLegacy(SyncCache cache)
        => SyncCacheManager.SaveCache(BasePath, cache);

    private string CurrentHash(WechatSyncContext context, WechatSyncOptions options)
    {
        var (item, route) = context.Routed[0];
        return SyncCacheManager.ComputeContentHash(
            item,
            route,
            item.ContentHtml ?? string.Empty,
            options,
            context);
    }

    private WechatSyncContext Context(string reviewStatus = "approved")
    {
        var item = new WechatSyncItem(
            Id: "post-1",
            Title: "Hello",
            Slug: "post-1",
            PublishAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ContentHtml: "<p>Hello</p>",
            Metadata: new Dictionary<string, object>
            {
                ["sourceKey"] = "notion",
                ["sourceId"] = "page-1",
                ["summary"] = "Summary",
                ["manifestReviewStatus"] = reviewStatus,
                ["reviewStatus"] = reviewStatus
            },
            Fields: new Dictionary<string, WechatSyncField>
            {
                ["type"] = new("string", "post")
            });
        return new WechatSyncContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = _logger,
            Routed =
            [
                (item, new WechatSyncRoute(
                    "/posts/post-1/",
                    Path.Combine(_rootDir, "dist", "posts", "post-1", "index.html"),
                    "post"))
            ]
        };
    }

    private static WechatSyncOptions Options(string appIdEnv, string appSecretEnv)
        => new(
            SourceNames: [],
            ContentTypes: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            DefaultTypesWhenMissing: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            CacheFile: CacheFile,
            MaxAttempts: 1,
            BaseDelayMs: 1,
            BackoffFactor: 1,
            AppIdEnv: appIdEnv,
            AppSecretEnv: appSecretEnv,
            ForceRetryIgnoreCacheEnv: string.Empty,
            Author: null,
            DefaultThumbMediaId: "thumb-media-id",
            NeedOpenComment: false,
            OnlyFansCanComment: false,
            SiteName: "Bukit",
            SiteUrl: "https://example.com",
            BaseUrl: "/")
        {
            MigrateLegacyCache = true,
            ConfirmedLegacyAppId = "app-a"
        };

    private sealed class RecordingGateway : IWechatDraftGateway
    {
        public int AddDraftCount { get; private set; }
        public int UploadThumbCount { get; private set; }
        public int PublishCount { get; private set; }
        public int CheckStatusCount { get; private set; }
        public int TotalCalls => AddDraftCount + UploadThumbCount + PublishCount + CheckStatusCount;

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            AddDraftCount++;
            return Task.FromResult("new-draft");
        }

        public Task<string> UploadThumbAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            UploadThumbCount++;
            return Task.FromResult("thumb-new");
        }

        public Task<string> UploadContentImageAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
            => Task.FromResult("https://mmbiz.qpic.cn/uploaded.png");

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
        {
            PublishCount++;
            return Task.FromResult("publish-new");
        }

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(
            string publishId,
            CancellationToken cancellationToken)
        {
            CheckStatusCount++;
            return Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string value)
        {
            Name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public string Name { get; }

        public void Dispose() => Environment.SetEnvironmentVariable(Name, _previous);
    }
}
