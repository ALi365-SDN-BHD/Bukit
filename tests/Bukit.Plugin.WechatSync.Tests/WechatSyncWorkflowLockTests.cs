using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncWorkflowLockTests : IDisposable
{
    private readonly string _rootDir;

    public WechatSyncWorkflowLockTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-lock-tests-" + Guid.NewGuid().ToString("N"));
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
    public async Task RunAsync_WhenLockWaitIsCanceled_MakesNoGatewayCalls()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var cachePath = CachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        using var heldLock = new FileStream(
            cachePath + ".lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        var gateway = new ControllableGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            workflow.RunAsync(Context(), Options(appId.Name, secret.Name), cancellation.Token));

        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Equal(0, gateway.TotalCalls);
    }

    [Fact]
    public async Task RunAsync_WhenLockWaitTimesOut_MakesNoGatewayCalls()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var cachePath = CachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        using var heldLock = new FileStream(
            cachePath + ".lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        var gateway = new ControllableGateway();
        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: null,
            downloadImageAsync: null,
            runLockTimeout: TimeSpan.FromMilliseconds(100));
        using var safetyCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            workflow.RunAsync(Context(), Options(appId.Name, secret.Name), safetyCancellation.Token));

        Assert.Contains("cache lock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Equal(0, gateway.TotalCalls);
    }

    [Fact]
    public async Task RunAsync_ConcurrentWorkflowsSerializeWholeRunThenSecondReloadsAndSkips()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var firstGateway = new ControllableGateway(blockDraft: true);
        var secondGateway = new ControllableGateway();
        var firstWorkflow = new WechatSyncWorkflow(firstGateway);
        var secondWorkflow = new WechatSyncWorkflow(secondGateway);
        var context = Context();
        var options = Options(appId.Name, secret.Name);

        var firstRun = firstWorkflow.RunAsync(context, options);
        await firstGateway.DraftEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var secondRun = secondWorkflow.RunAsync(context, options);
        var earlySecondEntry = await Task.WhenAny(
            secondGateway.DraftEntered.Task,
            Task.Delay(TimeSpan.FromMilliseconds(150)));
        var secondEnteredWhileFirstWasBlocked = ReferenceEquals(earlySecondEntry, secondGateway.DraftEntered.Task);

        firstGateway.ReleaseDraft();
        var firstResult = await firstRun.WaitAsync(TimeSpan.FromSeconds(2));
        var secondResult = await secondRun.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(secondEnteredWhileFirstWasBlocked);
        Assert.Equal(1, firstResult.Synced);
        Assert.Equal(1, secondResult.Skipped);
        Assert.Equal(0, secondGateway.TotalCalls);
    }

    [Fact]
    public async Task RunAsync_RejectsLockSidecarSymlinkEscapingWechatSyncRoot()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var cachePath = CachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-lock-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outsideLock = Path.Combine(outsideDir, "outside.lock");
            File.WriteAllText(outsideLock, string.Empty);
            File.CreateSymbolicLink(cachePath + ".lock", outsideLock);
            var gateway = new ControllableGateway();
            var workflow = new WechatSyncWorkflow(gateway);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                workflow.RunAsync(Context(), Options(appId.Name, secret.Name)));

            Assert.Equal(0, gateway.TotalCalls);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsDanglingLockSidecarSymlinkWithoutCreatingOutsideTarget()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var cachePath = CachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-dangling-lock-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outsideLock = Path.Combine(outsideDir, "not-created.lock");
            File.CreateSymbolicLink(cachePath + ".lock", outsideLock);
            var gateway = new ControllableGateway();
            var workflow = new WechatSyncWorkflow(gateway);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                workflow.RunAsync(Context(), Options(appId.Name, secret.Name)));

            Assert.False(File.Exists(outsideLock));
            Assert.Equal(0, gateway.TotalCalls);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    private string CachePath()
        => Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json");

    private WechatSyncContext Context()
    {
        var item = new WechatSyncItem(
            Id: "post-1",
            Title: "Hello",
            Slug: "post-1",
            PublishAt: DateTimeOffset.Parse("2026-07-21T00:00:00Z"),
            ContentHtml: "<p>Hello</p>",
            Metadata: new Dictionary<string, object>
            {
                ["sourceKey"] = "notion",
                ["sourceId"] = "page-1",
                ["summary"] = "Summary"
            },
            Fields: new Dictionary<string, WechatSyncField>
            {
                ["type"] = new("string", "post")
            });
        var route = new WechatSyncRoute(
            "/posts/post-1/",
            Path.Combine(_rootDir, "dist", "posts", "post-1", "index.html"),
            "post");

        return new WechatSyncContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = new ConsoleLogger(LogLevel.Error),
            Routed = [(item, route)]
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
            BaseUrl: "/");

    private sealed class ControllableGateway(bool blockDraft = false) : IWechatDraftGateway
    {
        private readonly TaskCompletionSource _releaseDraft = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _addDraftCount;
        private int _totalCalls;

        public TaskCompletionSource DraftEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int AddDraftCount => Volatile.Read(ref _addDraftCount);
        public int TotalCalls => Volatile.Read(ref _totalCalls);

        public async Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _addDraftCount);
            Interlocked.Increment(ref _totalCalls);
            DraftEntered.TrySetResult();
            if (blockDraft)
            {
                await _releaseDraft.Task.WaitAsync(cancellationToken);
            }

            return "draft-1";
        }

        public Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult("thumb-1");
        }

        public Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult("https://mmbiz.qpic.cn/image.jpg");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult("publish-1");
        }

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(string publishId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
        }

        public void ReleaseDraft()
            => _releaseDraft.TrySetResult();
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
