using System.Diagnostics;
using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncWorkflowLockTests : IDisposable
{
    private const string HelperRoleEnvironmentVariable = "BUKIT_WECHAT_LOCK_HELPER_ROLE";
    private const string HelperRootEnvironmentVariable = "BUKIT_WECHAT_LOCK_HELPER_ROOT";
    private const string HelperIpcEnvironmentVariable = "BUKIT_WECHAT_LOCK_HELPER_IPC";
    private const string HelperAppIdEnvironmentVariable = "BUKIT_WECHAT_LOCK_HELPER_APP_ID";
    private const string HelperSecretEnvironmentVariable = "BUKIT_WECHAT_LOCK_HELPER_SECRET";
    private readonly string _rootDir;

    public WechatSyncWorkflowLockTests()
    {
        _rootDir = Path.Combine(AppContext.BaseDirectory, "bukit-wechat-lock-tests-" + Guid.NewGuid().ToString("N"));
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
    public async Task RunAsync_TwoHelperProcessesSerializeThenSecondReloadsAndSkips()
    {
        var ipcDir = Path.Combine(_rootDir, "ipc");
        Directory.CreateDirectory(ipcDir);
        WorkflowHelperProcess? first = null;
        WorkflowHelperProcess? second = null;
        using var totalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            first = StartWorkflowHelperProcess("A", _rootDir, ipcDir);
            await WaitForMarkerAsync(Path.Combine(ipcDir, "a-entered"), totalTimeout.Token);
            second = StartWorkflowHelperProcess("B", _rootDir, ipcDir);

            await WaitForMarkerAsync(Path.Combine(ipcDir, "b-waiting"), totalTimeout.Token);
            Assert.False(File.Exists(Path.Combine(ipcDir, "b-gateway-entered")));
            Assert.False(second.Process.HasExited);
            File.WriteAllText(Path.Combine(ipcDir, "release-a"), string.Empty);

            var firstResult = await first.CompleteAsync(totalTimeout.Token);
            var secondResult = await second.CompleteAsync(totalTimeout.Token);

            Assert.Equal("synced=1 skipped=0 calls=1", firstResult);
            Assert.Equal("synced=0 skipped=1 calls=0", secondResult);
        }
        finally
        {
            File.WriteAllText(Path.Combine(ipcDir, "release-a"), string.Empty);
            if (second is not null)
            {
                await second.DisposeAsync();
            }

            if (first is not null)
            {
                await first.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task HelperProcess_RunWorkflowAccordingToEnvironment()
    {
        var role = Environment.GetEnvironmentVariable(HelperRoleEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        var rootDir = Environment.GetEnvironmentVariable(HelperRootEnvironmentVariable)
            ?? throw new InvalidOperationException("Missing helper root directory.");
        var ipcDir = Environment.GetEnvironmentVariable(HelperIpcEnvironmentVariable)
            ?? throw new InvalidOperationException("Missing helper IPC directory.");
        var gateway = new ProcessGateway(role, ipcDir);
        var workflow = new WechatSyncWorkflow(gateway);
        var run = workflow.RunAsync(
            CreateContext(rootDir),
            Options(HelperAppIdEnvironmentVariable, HelperSecretEnvironmentVariable));

        if (role == "B")
        {
            var cacheDir = Path.Combine(rootDir, ".cache", "wechat-sync");
            await WaitForConditionAsync(
                () => Directory.Exists(cacheDir) &&
                      Directory.GetFiles(cacheDir, "*.run-guard").Length >= 2,
                TimeSpan.FromSeconds(10),
                "two live cache directory guards");
            File.WriteAllText(Path.Combine(ipcDir, "b-waiting"), string.Empty);
        }

        var result = await run.WaitAsync(TimeSpan.FromSeconds(15));
        File.WriteAllText(
            Path.Combine(ipcDir, role.ToLowerInvariant() + "-result"),
            $"synced={result.Synced} skipped={result.Skipped} calls={gateway.TotalCalls}");
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

    [Fact]
    public async Task RunAsync_RejectsInRootCacheAliasInsteadOfUsingDistinctLock()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var realCachePath = CachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(realCachePath)!);
        File.WriteAllText(realCachePath, "{\"Version\":3,\"Records\":{},\"ThumbMediaIds\":{},\"Operations\":{}}");
        var aliasCachePath = Path.Combine(Path.GetDirectoryName(realCachePath)!, "alias.json");
        File.CreateSymbolicLink(aliasCachePath, realCachePath);
        var firstGateway = new ControllableGateway(blockDraft: true);
        var aliasGateway = new ControllableGateway();
        var context = Context();
        var options = Options(appId.Name, secret.Name);

        var firstRun = new WechatSyncWorkflow(firstGateway).RunAsync(context, options);
        await firstGateway.DraftEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WechatSyncWorkflow(aliasGateway).RunAsync(
                    context,
                    options with { CacheFile = ".cache/wechat-sync/alias.json" }));

            Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, aliasGateway.TotalCalls);
        }
        finally
        {
            firstGateway.ReleaseDraft();
            await firstRun.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task RunAsync_RejectsCacheDirectoryReplacementWhileWaitingForLock()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var cachePath = CachePath();
        var cacheDir = Path.GetDirectoryName(cachePath)!;
        var displacedDir = cacheDir + "-displaced";
        Directory.CreateDirectory(cacheDir);
        await using var heldLock = new FileStream(
            cachePath + ".lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        var gateway = new ControllableGateway();
        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: null,
            downloadImageAsync: null,
            runLockTimeout: TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var run = workflow.RunAsync(Context(), Options(appId.Name, secret.Name), cancellation.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        Assert.False(run.IsCompleted);

        var replacementError = Record.Exception(() => Directory.Move(cacheDir, displacedDir));
        if (replacementError is not null)
        {
            Assert.True(
                OperatingSystem.IsWindows() &&
                (replacementError is IOException or UnauthorizedAccessException),
                $"Unexpected cache directory replacement failure: {replacementError}");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.Equal(0, gateway.TotalCalls);
            return;
        }

        Directory.CreateDirectory(cacheDir);
        await heldLock.DisposeAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Contains("cache directory changed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.TotalCalls);
    }

    private string CachePath()
        => Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json");

    private WechatSyncContext Context()
        => CreateContext(_rootDir);

    private static WechatSyncContext CreateContext(string rootDir)
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
                ["summary"] = "Summary",
                ["manifestReviewStatus"] = "approved",
                ["reviewStatus"] = "approved",
                ["syncStatus"] = string.Empty
            },
            Fields: new Dictionary<string, WechatSyncField>
            {
                ["type"] = new("string", "post")
            });
        var route = new WechatSyncRoute(
            "/posts/post-1/",
            Path.Combine(rootDir, "dist", "posts", "post-1", "index.html"),
            "post");

        return new WechatSyncContext
        {
            RootDir = rootDir,
            OutputDir = Path.Combine(rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = new ConsoleLogger(LogLevel.Error),
            Routed = [(item, route)]
        };
    }

    private static WorkflowHelperProcess StartWorkflowHelperProcess(string role, string rootDir, string ipcDir)
    {
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrWhiteSpace(dotnetHost))
        {
            dotnetHost = "dotnet";
        }

        var startInfo = new ProcessStartInfo(dotnetHost)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("vstest");
        startInfo.ArgumentList.Add(typeof(WechatSyncWorkflowLockTests).Assembly.Location);
        startInfo.ArgumentList.Add(
            "--TestCaseFilter:FullyQualifiedName=" +
            typeof(WechatSyncWorkflowLockTests).FullName +
            ".HelperProcess_RunWorkflowAccordingToEnvironment");
        startInfo.Environment[HelperRoleEnvironmentVariable] = role;
        startInfo.Environment[HelperRootEnvironmentVariable] = rootDir;
        startInfo.Environment[HelperIpcEnvironmentVariable] = ipcDir;
        startInfo.Environment[HelperAppIdEnvironmentVariable] = "app";
        startInfo.Environment[HelperSecretEnvironmentVariable] = "secret";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the wechat-sync lock helper process.");
        return new WorkflowHelperProcess(
            process,
            process.StandardOutput.ReadToEndAsync(),
            process.StandardError.ReadToEndAsync(),
            Path.Combine(ipcDir, role.ToLowerInvariant() + "-result"));
    }

    private static Task WaitForMarkerAsync(string path, TimeSpan timeout)
        => WaitForConditionAsync(() => File.Exists(path), timeout, $"marker '{path}'");

    private static async Task WaitForMarkerAsync(string path, CancellationToken cancellationToken)
    {
        while (!File.Exists(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string description)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed >= timeout)
            {
                throw new TimeoutException($"Timed out waiting for {description}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }
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

    private sealed class ProcessGateway(string role, string ipcDir) : IWechatDraftGateway
    {
        private int _totalCalls;

        public int TotalCalls => Volatile.Read(ref _totalCalls);

        public async Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            Interlocked.Increment(ref _totalCalls);
            File.WriteAllText(Path.Combine(ipcDir, role.ToLowerInvariant() + "-gateway-entered"), string.Empty);
            if (role == "A")
            {
                File.WriteAllText(Path.Combine(ipcDir, "a-entered"), string.Empty);
                await WaitForMarkerAsync(Path.Combine(ipcDir, "release-a"), TimeSpan.FromSeconds(15))
                    .WaitAsync(cancellationToken);
            }

            return "draft-1";
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
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult("thumb-1");
        }

        public Task<string> UploadContentImageAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            _ = bytes;
            _ = fileName;
            _ = contentType;
            _ = cancellationToken;
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult("https://mmbiz.qpic.cn/image.jpg");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
        {
            _ = mediaId;
            _ = cancellationToken;
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult("publish-1");
        }

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(
            string publishId,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            Interlocked.Increment(ref _totalCalls);
            return Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
        }
    }

    private sealed class WorkflowHelperProcess(
        Process process,
        Task<string> standardOutput,
        Task<string> standardError,
        string resultPath) : IAsyncDisposable
    {
        private bool _completed;

        internal Process Process { get; } = process;

        internal async Task<string> CompleteAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKill();
                throw new TimeoutException($"Wechat-sync helper process {Process.Id} exceeded the parent test timeout.");
            }

            var output = await standardOutput;
            var error = await standardError;
            _completed = true;
            Assert.True(
                Process.ExitCode == 0,
                $"Wechat-sync helper process exited with {Process.ExitCode}. stdout: {output} stderr: {error}");
            Assert.True(File.Exists(resultPath), $"Wechat-sync helper did not write result '{resultPath}'. stdout: {output} stderr: {error}");
            return File.ReadAllText(resultPath);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed && !Process.HasExited)
            {
                TryKill();
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await Process.WaitForExitAsync(cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    // The process tree kill was already requested; do not hide the primary test failure.
                }
            }

            Process.Dispose();
        }

        private void TryKill()
        {
            try
            {
                Process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }
        }
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
