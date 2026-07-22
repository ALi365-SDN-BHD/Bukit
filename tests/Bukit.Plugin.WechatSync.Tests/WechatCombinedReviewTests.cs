using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatCombinedReviewTests : IDisposable
{
    private const string SyncKey = "notion:page-1";
    private readonly string _rootDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-wechat-combined-review-" + Guid.NewGuid().ToString("N"));

    public WechatCombinedReviewTests()
        => Directory.CreateDirectory(_rootDir);

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("DraftSubmitting", "draft", false)]
    [InlineData("PublishSubmitting", "publish", false)]
    [InlineData("DraftCreated", "draft", true)]
    public async Task RunAsync_RecoveryRequiredPrecedesInvalidCurrentDraftContract(
        string state,
        string target,
        bool useMismatchedHash)
    {
        using var appId = new EnvironmentVariableScope(
            "BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"),
            "app");
        using var secret = new EnvironmentVariableScope(
            "BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"),
            "secret");
        var options = Options(appId.Name, secret.Name) with { Target = target };
        var context = Context();
        var (item, route) = context.Routed[0];
        var currentHash = SyncCacheManager.ComputeContentHash(
            item,
            route,
            item.ContentHtml ?? string.Empty,
            options,
            context);
        SeedOperation(
            state,
            target,
            useMismatchedHash ? "mismatched-hash" : currentHash,
            draftId: state is "PublishSubmitting" or "DraftCreated" ? "draft-existing" : null);
        var gateway = new RecordingGateway();
        var downloadCalls = 0;
        var workflow = new WechatSyncWorkflow(
            gateway,
            downloadImageAsync: (_, _) =>
            {
                downloadCalls++;
                return Task.FromResult(TinyPng);
            });

        var result = await workflow.RunAsync(context, options);

        Assert.False(result.Success);
        var error = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("plugin.wechat-sync.recoveryRequired", error.Code);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Code.StartsWith("plugin.wechat-sync.contract.", StringComparison.Ordinal));
        Assert.Equal(0, gateway.TotalCalls);
        Assert.Equal(0, downloadCalls);
        Assert.Equal(state, LoadCache().Operations[SyncKey].State);
    }

    private WechatSyncContext Context()
    {
        var item = new WechatSyncItem(
            "post-1",
            new string('中', WechatDraftContract.TitleMaxTextElements + 1),
            "post-1",
            DateTimeOffset.Parse("2026-07-22T00:00:00Z"),
            "<p><img src=\"https://cdn.example.com/inline.png\"></p>",
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
                ["type"] = new("string", "post")
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
            CacheFile: ".cache/wechat-sync/sync-cache.json",
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
            BaseUrl: "/",
            ProcessImages: true);

    private string CachePath
        => Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json");

    private SyncCache LoadCache()
        => SyncCacheManager.LoadCache(CachePath, new ConsoleLogger(LogLevel.Error));

    private void SeedOperation(
        string state,
        string target,
        string contentHash,
        string? draftId)
    {
        var operation = new SyncOperation(
            state,
            contentHash,
            target,
            draftId,
            null,
            DateTimeOffset.Parse("2026-07-22T00:00:00Z"))
        {
            SourceKey = "notion",
            SourceId = "page-1",
            Title = "Stored title"
        };
        SyncCacheManager.SaveCache(CachePath, new SyncCache(
            3,
            new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            Operations = new Dictionary<string, SyncOperation>(StringComparer.Ordinal)
            {
                [SyncKey] = operation
            }
        });
    }

    private sealed class RecordingGateway : IWechatDraftGateway
    {
        public int TotalCalls { get; private set; }

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult("draft-new");
        }

        public Task<string> UploadThumbAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult("thumb-new");
        }

        public Task<string> UploadContentImageAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult("https://mmbiz.qpic.cn/uploaded.png");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult("publish-new");
        }

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(
            string publishId,
            CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string value)
        {
            Name = name;
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public string Name { get; }

        public void Dispose()
            => Environment.SetEnvironmentVariable(_name, _previous);
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
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];
}
