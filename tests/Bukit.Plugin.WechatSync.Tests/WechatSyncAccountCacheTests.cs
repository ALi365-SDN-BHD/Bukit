using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncAccountCacheTests : IDisposable
{
    private const string CacheFile = ".cache/wechat-sync/sync-cache.json";
    private readonly string _rootDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-wechat-account-cache-" + Guid.NewGuid().ToString("N"));

    public WechatSyncAccountCacheTests()
        => Directory.CreateDirectory(_rootDir);

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_SwitchesAccountsWithoutReusingRecordsOrThumbMedia()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_ACCOUNT_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_ACCOUNT_SECRET", "secret-a");
        var gateway = new RecordingGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        var context = ContextWithCover();
        var options = Options(appId.Name, secret.Name);

        var firstA = await workflow.RunAsync(context, options);

        Assert.True(firstA.Success);
        Assert.Equal(1, firstA.Synced);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.UploadThumbCount);

        Environment.SetEnvironmentVariable(appId.Name, "app-b");
        Environment.SetEnvironmentVariable(secret.Name, "secret-b");

        var firstB = await workflow.RunAsync(context, options);

        Assert.True(firstB.Success);
        Assert.Equal(1, firstB.Synced);
        Assert.Equal(0, firstB.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
        Assert.Equal(2, gateway.UploadThumbCount);
        Assert.NotEqual(firstA.CachePath, firstB.CachePath);

        var cacheA = SyncCacheManager.LoadCache(firstA.CachePath, context.Logger, "app-a");
        var cacheB = SyncCacheManager.LoadCache(firstB.CachePath, context.Logger, "app-b");
        Assert.Equal("app-a", cacheA.AppId);
        Assert.Equal("app-b", cacheB.AppId);
        Assert.DoesNotContain("secret-a", File.ReadAllText(firstA.CachePath), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-b", File.ReadAllText(firstB.CachePath), StringComparison.Ordinal);

        Environment.SetEnvironmentVariable(appId.Name, "app-a");
        Environment.SetEnvironmentVariable(secret.Name, "secret-a");

        var secondA = await workflow.RunAsync(context, options);

        Assert.True(secondA.Success);
        Assert.Equal(0, secondA.Synced);
        Assert.Equal(1, secondA.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
        Assert.Equal(2, gateway.UploadThumbCount);
        Assert.Equal(firstA.CachePath, secondA.CachePath);
    }

    [Fact]
    public async Task RunAsync_RejectsLegacyCacheWithoutAppIdInsteadOfReusingIt()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_ACCOUNT_APP_ID", "app-a");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_ACCOUNT_SECRET", "secret-a");
        var legacyPath = SyncCacheManager.ResolvePath(_rootDir, CacheFile);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        SyncCacheManager.SaveCache(legacyPath, new SyncCache(
            3,
            new Dictionary<string, SyncRecord>(StringComparer.Ordinal)));
        var gateway = new RecordingGateway();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WechatSyncWorkflow(gateway).RunAsync(ContextWithCover(), Options(appId.Name, secret.Name)));

        Assert.Contains("not bound", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Back it up", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, gateway.TotalCalls);
        Assert.True(File.Exists(legacyPath));
    }

    private WechatSyncContext ContextWithCover()
    {
        var coverPath = Path.Combine(_rootDir, "dist", "assets", "cover.png");
        Directory.CreateDirectory(Path.GetDirectoryName(coverPath)!);
        File.WriteAllBytes(coverPath, TinyPng);
        var item = new WechatSyncItem(
            "post-1",
            "Title",
            "post-1",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "<p>Hello</p>",
            new Dictionary<string, object>
            {
                ["sourceKey"] = "notion",
                ["sourceId"] = "page-1",
                ["summary"] = "Summary",
                ["manifestReviewStatus"] = "approved",
                ["reviewStatus"] = "approved"
            },
            new Dictionary<string, WechatSyncField>
            {
                ["type"] = new("string", "post"),
                ["cover"] = new("string", "/assets/cover.png")
            });
        return new WechatSyncContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = new ConsoleLogger(LogLevel.Error),
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
            DefaultThumbMediaId: null,
            NeedOpenComment: false,
            OnlyFansCanComment: false,
            SiteName: "Bukit",
            SiteUrl: "https://example.com",
            BaseUrl: "/");

    private sealed class RecordingGateway : IWechatDraftGateway
    {
        public int AddDraftCount { get; private set; }
        public int UploadThumbCount { get; private set; }
        public int TotalCalls => AddDraftCount + UploadThumbCount;

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            AddDraftCount++;
            return Task.FromResult("draft-" + AddDraftCount);
        }

        public Task<string> UploadThumbAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            _ = bytes;
            _ = fileName;
            _ = contentType;
            _ = cancellationToken;
            UploadThumbCount++;
            return Task.FromResult("thumb-" + UploadThumbCount);
        }

        public Task<string> UploadContentImageAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
            => Task.FromResult("https://mmbiz.qpic.cn/uploaded.png");

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
            => Task.FromResult("publish-1");

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(
            string publishId,
            CancellationToken cancellationToken)
            => Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
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

    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];
}
