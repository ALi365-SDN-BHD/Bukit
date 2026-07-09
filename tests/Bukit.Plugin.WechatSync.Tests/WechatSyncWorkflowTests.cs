using System.Text.Json;
using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncWorkflowTests : IDisposable
{
    private readonly string _rootDir;

    public WechatSyncWorkflowTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-sync-workflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_UsesContentHashToSkipUnchangedCachedContent()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        var context = Context("<p>Hello</p>");
        var options = Options(appId.Name, secret.Name);

        var first = await workflow.RunAsync(context, options);
        var second = await workflow.RunAsync(context, options);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, first.Synced);
        Assert.Equal(1, second.Skipped);
        Assert.Equal(1, gateway.AddDraftCount);
    }

    [Fact]
    public async Task RunAsync_SkipsUnchangedContentAfterHtmlProcessingChangesMarkup()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        var context = Context("<h1>Hello</h1>");
        var options = Options(appId.Name, secret.Name);

        await workflow.RunAsync(context, options);
        var second = await workflow.RunAsync(context, options);

        Assert.True(second.Success);
        Assert.Equal(1, second.Skipped);
        Assert.Equal(1, gateway.AddDraftCount);
    }

    [Fact]
    public async Task RunAsync_ContentChangeBypassesExistingCacheRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        var options = Options(appId.Name, secret.Name);

        await workflow.RunAsync(Context("<p>Old</p>"), options);
        var changed = await workflow.RunAsync(Context("<p>New</p>"), options);

        Assert.True(changed.Success);
        Assert.Equal(1, changed.Synced);
        Assert.Equal(0, changed.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
    }

    [Fact]
    public async Task RunAsync_PublishFailureDoesNotMarkCandidateSyncedOrWriteSuccessfulCacheRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway { PublishStatus = 2 };
        var workflow = new WechatSyncWorkflow(gateway, delayAsync: (_, _) => Task.CompletedTask);
        var options = Options(appId.Name, secret.Name) with
        {
            Target = "publish",
            PublishPollMaxAttempts = 1,
            PublishPollIntervalSeconds = 1
        };

        var result = await workflow.RunAsync(Context("<p>Hello</p>"), options);

        Assert.False(result.Success);
        Assert.Equal(0, result.Synced);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.publishFailed");
        Assert.False(File.Exists(Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json")));
    }

    [Fact]
    public async Task RunAsync_WhenLaterCandidateFails_PreservesEarlierSuccessfulCacheRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway { FailAddDraftOnAttempt = 2 };
        var workflow = new WechatSyncWorkflow(gateway);
        var options = Options(appId.Name, secret.Name);
        var context = Context(
            ("post-1", "page-1", "<p>One</p>"),
            ("post-2", "page-2", "<p>Two</p>"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.RunAsync(context, options));

        var cachePath = Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json");
        var cache = SyncCacheManager.LoadCache(cachePath, new ConsoleLogger(LogLevel.Error));
        Assert.Contains("notion:page-1", cache.Records.Keys);
        Assert.DoesNotContain("notion:page-2", cache.Records.Keys);
    }

    [Fact]
    public void SyncCacheManager_RejectsAbsoluteCachePathOutsideProjectRoot()
    {
        var outside = Path.Combine(Path.GetTempPath(), "bukit-wechat-outside-" + Guid.NewGuid().ToString("N"), "cache.json");

        var ex = Assert.Throws<InvalidOperationException>(() => SyncCacheManager.ResolvePath(_rootDir, outside));

        Assert.Contains("project root", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SyncCacheManager_RejectsRelativeCachePathOutsideProjectRoot()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SyncCacheManager.ResolvePath(_rootDir, "../cache.json"));

        Assert.Contains("project root", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThumbResolver_RejectsRelativeImagePathEscapingOutputDirectory()
    {
        var secretPath = Path.Combine(_rootDir, "secret.png");
        File.WriteAllBytes(secretPath, [1, 2, 3]);
        var context = Context("<p>Hello</p>");

        var resolved = ThumbResolver.TryResolveLocalAssetPath(context, "../secret.png", out var filePath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, filePath);
    }

    [Fact]
    public void ThumbResolver_RejectsMediaIndexPathEscapingDownloadDirectory()
    {
        var downloadDir = Path.Combine(_rootDir, ".cache", "media");
        Directory.CreateDirectory(downloadDir);
        File.WriteAllBytes(Path.Combine(_rootDir, ".cache", "secret.png"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(downloadDir, ".media-index.json"), """
{
  "https://example.com/secret.png": "../secret.png"
}
""");

        var resolved = ThumbResolver.TryResolveFromMediaIndex(downloadDir, "https://example.com/secret.png");

        Assert.Null(resolved);
    }

    private WechatSyncContext Context(string html)
        => Context(("post-1", "page-1", html));

    private WechatSyncContext Context(params (string Id, string SourceId, string Html)[] entries)
    {
        var routed = entries.Select(entry =>
        {
            var item = new WechatSyncItem(
                Id: entry.Id,
                Title: "Hello",
                Slug: entry.Id,
                PublishAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ContentHtml: entry.Html,
                Metadata: new Dictionary<string, object>
                {
                    ["sourceKey"] = "notion",
                    ["sourceId"] = entry.SourceId,
                    ["summary"] = "Summary"
                },
                Fields: new Dictionary<string, WechatSyncField>
                {
                    ["type"] = new("string", "post")
                });
            var route = new WechatSyncRoute($"/posts/{entry.Id}/", Path.Combine(_rootDir, "dist", "posts", entry.Id, "index.html"), "post");
            return (item, route);
        }).ToArray();

        return new WechatSyncContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = new ConsoleLogger(LogLevel.Error),
            Routed = routed
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
            ForceRetryIgnoreCacheEnv: "",
            Author: null,
            DefaultThumbMediaId: "thumb-media-id",
            NeedOpenComment: false,
            OnlyFansCanComment: false,
            SiteName: "Bukit",
            SiteUrl: "https://example.com",
            BaseUrl: "/");

    private sealed class FakeWechatDraftGateway : IWechatDraftGateway
    {
        public int AddDraftCount { get; private set; }
        public int PublishStatus { get; init; }
        public int? FailAddDraftOnAttempt { get; init; }

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            AddDraftCount++;
            if (AddDraftCount == FailAddDraftOnAttempt)
            {
                throw new InvalidOperationException("draft failed");
            }

            return Task.FromResult("draft-" + AddDraftCount);
        }

        public Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
            => Task.FromResult("uploaded-thumb");

        public Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
            => Task.FromResult("https://mmbiz.qpic.cn/image.jpg");

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
            => Task.FromResult("publish-1");

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(string publishId, CancellationToken cancellationToken)
            => Task.FromResult(new WechatPublishStatusResult(publishId, PublishStatus, null));
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string? value)
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
}
