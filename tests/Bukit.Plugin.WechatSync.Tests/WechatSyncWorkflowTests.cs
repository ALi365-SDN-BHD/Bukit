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
    public async Task RunAsync_ReviewStatusDeniedBeforeGatewayEvenWhenForced()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var context = WithReviewStatus(Context("<p>Hello</p>"), "needs-review");
        var options = Options(appId.Name, secret.Name) with
        {
            Target = "publish",
            Force = true
        };

        var result = await new WechatSyncWorkflow(gateway).RunAsync(context, options);

        Assert.True(result.Success);
        Assert.Equal(0, result.Candidates);
        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.reviewStatusDenied" &&
            diagnostic.Severity == "warning");
    }

    [Fact]
    public async Task RunAsync_ContentExpiringAfterDraftCreationIsNotPublished()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var now = DateTimeOffset.Parse("2026-07-22T00:00:00Z");
        var expiresAt = now.AddMinutes(1);
        var gateway = new FakeWechatDraftGateway
        {
            OnAddDraft = () => now = expiresAt
        };
        var context = WithExpiresAt(Context("<p>Hello</p>"), expiresAt);
        var options = Options(appId.Name, secret.Name) with
        {
            Target = "publish",
            PublishPollMaxAttempts = 1,
            PublishPollIntervalSeconds = 1
        };
        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            saveCache: null,
            utcNow: () => now);

        var result = await workflow.RunAsync(context, options);

        Assert.True(result.Success);
        Assert.Equal(1, result.Candidates);
        Assert.Equal(0, result.Synced);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(0, gateway.PublishCount);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.contentExpired" &&
            diagnostic.Severity == "warning");
    }

    [Fact]
    public async Task RunAsync_ContentExpiringWhilePublishSubmissionIsPersistedIsNotPublished()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var now = DateTimeOffset.Parse("2026-07-22T00:00:00Z");
        var expiresAt = now.AddMinutes(1);
        var saveCount = 0;
        var savedStates = new List<string?>();
        var gateway = new FakeWechatDraftGateway();
        var context = WithExpiresAt(Context("<p>Hello</p>"), expiresAt);
        var options = Options(appId.Name, secret.Name) with
        {
            Target = "publish",
            PublishPollMaxAttempts = 1,
            PublishPollIntervalSeconds = 1
        };
        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            saveCache: (_, cache) =>
            {
                saveCount++;
                savedStates.Add(cache.Operations.Values.SingleOrDefault()?.State);
                if (saveCount == 3)
                {
                    now = expiresAt;
                }
            },
            utcNow: () => now);

        var result = await workflow.RunAsync(context, options);

        Assert.True(result.Success);
        Assert.True(saveCount >= 3);
        Assert.Equal("DraftCreated", savedStates[^1]);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(0, gateway.PublishCount);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.contentExpired" &&
            diagnostic.Severity == "warning");
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
    public async Task RunAsync_LocalInlineImageFileChangeBypassesExistingCacheRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(
            gateway,
            downloadImageAsync: (_, _) => throw new InvalidOperationException("network should not be used for local image"));
        var context = Context("""<p><img src="/assets/inline.png"></p>""");
        var assetsDir = Path.Combine(context.OutputDir, "assets");
        Directory.CreateDirectory(assetsDir);
        var imagePath = Path.Combine(assetsDir, "inline.png");
        File.WriteAllBytes(imagePath, TinyPng);
        var options = Options(appId.Name, secret.Name) with { ProcessImages = true };

        await workflow.RunAsync(context, options);
        var originalWriteTime = File.GetLastWriteTimeUtc(imagePath);
        File.WriteAllBytes(imagePath, TinyPngVariant());
        File.SetLastWriteTimeUtc(imagePath, originalWriteTime);
        var changed = await workflow.RunAsync(context, options);

        Assert.True(changed.Success);
        Assert.Equal(1, changed.Synced);
        Assert.Equal(0, changed.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
        Assert.Equal(2, gateway.UploadContentImageCount);
    }

    [Fact]
    public async Task RunAsync_LocalDefaultImageFileChangeBypassesExistingCacheRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(
            gateway,
            downloadImageAsync: (_, _) => Task.FromResult(Array.Empty<byte>()));
        var context = ContextWithCover("<p>Hello</p>", "https://cdn.example.com/missing.png");
        var defaultPath = Path.Combine(context.OutputDir, "assets", "default.png");
        File.WriteAllBytes(defaultPath, TinyPng);
        var options = Options(appId.Name, secret.Name) with
        {
            DefaultThumbMediaId = null,
            DefaultImageUrl = "/assets/default.png"
        };

        await workflow.RunAsync(context, options);
        var originalWriteTime = File.GetLastWriteTimeUtc(defaultPath);
        File.WriteAllBytes(defaultPath, TinyPngVariant());
        File.SetLastWriteTimeUtc(defaultPath, originalWriteTime);
        var changed = await workflow.RunAsync(context, options);

        Assert.True(changed.Success);
        Assert.Equal(1, changed.Synced);
        Assert.Equal(0, changed.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
        Assert.Equal(2, gateway.UploadThumbCount);
    }

    [Fact]
    public async Task RunAsync_ProtocolRelativeLocalCoverFileChangeBypassesExistingCacheRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(
            gateway,
            downloadImageAsync: (_, _) => Task.FromResult(Array.Empty<byte>()));
        var context = ContextWithCover("<p>Hello</p>", "//example.com/assets/cover.png");
        var coverPath = Path.Combine(context.OutputDir, "assets", "cover.png");
        var options = Options(appId.Name, secret.Name) with { DefaultThumbMediaId = null };

        await workflow.RunAsync(context, options);
        var originalWriteTime = File.GetLastWriteTimeUtc(coverPath);
        File.WriteAllBytes(coverPath, TinyPngVariant());
        File.SetLastWriteTimeUtc(coverPath, originalWriteTime);
        var changed = await workflow.RunAsync(context, options);

        Assert.True(changed.Success);
        Assert.Equal(1, changed.Synced);
        Assert.Equal(0, changed.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
        Assert.Equal(2, gateway.UploadThumbCount);
    }

    [Theory]
    [MemberData(nameof(DraftRequestOptionChanges))]
    public async Task RunAsync_DraftRequestOptionChangeBypassesExistingCacheRecord(
        string caseName,
        Func<WechatSyncOptions, WechatSyncOptions> mutateOptions,
        Action<WechatDraftRequest> assertRequest)
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        var context = Context("<p>Hello</p>");
        var options = Options(appId.Name, secret.Name);

        Assert.False(string.IsNullOrWhiteSpace(caseName));

        var first = await workflow.RunAsync(context, options);
        var second = await workflow.RunAsync(context, mutateOptions(options));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, second.Synced);
        Assert.Equal(0, second.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
        assertRequest(gateway.Requests[^1]);
    }

    public static IEnumerable<object[]> DraftRequestOptionChanges()
    {
        yield return
        [
            "author",
            new Func<WechatSyncOptions, WechatSyncOptions>(options => options with { Author = "Writer" }),
            new Action<WechatDraftRequest>(request => Assert.Equal("Writer", request.Author))
        ];

        yield return
        [
            "comment-options",
            new Func<WechatSyncOptions, WechatSyncOptions>(options => options with
            {
                NeedOpenComment = true,
                OnlyFansCanComment = true
            }),
            new Action<WechatDraftRequest>(request =>
            {
                Assert.True(request.NeedOpenComment);
                Assert.True(request.OnlyFansCanComment);
            })
        ];

        yield return
        [
            "content-source-url",
            new Func<WechatSyncOptions, WechatSyncOptions>(options => options with
            {
                SiteUrl = "https://changed.example",
                BaseUrl = "/docs"
            }),
            new Action<WechatDraftRequest>(request => Assert.Equal("https://changed.example/docs/posts/post-1/", request.ContentSourceUrl))
        ];

        yield return
        [
            "default-thumb-media-id",
            new Func<WechatSyncOptions, WechatSyncOptions>(options => options with { DefaultThumbMediaId = "thumb-media-id-2" }),
            new Action<WechatDraftRequest>(request => Assert.Equal("thumb-media-id-2", request.ThumbMediaId))
        ];
    }

    [Fact]
    public async Task RunAsync_ContentSourceUrlDoesNotDuplicateConfiguredBaseUrl()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        var context = Context("<p>Hello</p>");
        var (item, route) = context.Routed[0];
        context = new WechatSyncContext
        {
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = "/docs",
            SiteName = context.SiteName,
            SiteUrl = context.SiteUrl,
            MediaDownloadDir = context.MediaDownloadDir,
            Logger = context.Logger,
            Routed = [(item, route with { Url = "/docs/posts/post-1/" })]
        };
        var options = Options(appId.Name, secret.Name) with
        {
            SiteUrl = "https://example.com",
            BaseUrl = "/docs"
        };

        var result = await workflow.RunAsync(context, options);

        Assert.True(result.Success);
        Assert.Equal("https://example.com/docs/posts/post-1/", gateway.Requests.Single().ContentSourceUrl);
    }

    [Fact]
    public async Task RunAsync_PropagatesCancellationFromDraftUploadWithoutRetrying()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway { CancelAddDraft = true };
        var workflow = new WechatSyncWorkflow(gateway, delayAsync: (_, _) => Task.CompletedTask);
        var options = Options(appId.Name, secret.Name) with { MaxAttempts = 2 };

        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RunAsync(Context("<p>Hello</p>"), options));

        Assert.Equal(1, gateway.AddDraftCount);
    }

    [Fact]
    public async Task RunAsync_PropagatesCancellationFromPublish()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway { CancelPublish = true };
        var workflow = new WechatSyncWorkflow(gateway, delayAsync: (_, _) => Task.CompletedTask);
        var options = Options(appId.Name, secret.Name) with
        {
            Target = "publish",
            PublishPollMaxAttempts = 1,
            PublishPollIntervalSeconds = 1
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RunAsync(Context("<p>Hello</p>"), options));
    }

    [Fact]
    public async Task RunAsync_PropagatesCancellationFromImageDownload()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(
            gateway,
            downloadImageAsync: (_, _) => throw new OperationCanceledException("download canceled"));
        var options = Options(appId.Name, secret.Name) with { ProcessImages = true };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            workflow.RunAsync(Context("""<p><img src="https://cdn.example.com/image.png"></p>"""), options));

        Assert.Equal(0, gateway.AddDraftCount);
    }

    [Fact]
    public async Task ContentImageProcessor_RejectsOversizedLocalImageBeforeConversion()
    {
        var logger = new RecordingLogger();
        var gateway = new FakeWechatDraftGateway();
        var processor = new ContentImageProcessor(
            gateway,
            (_, _) => throw new InvalidOperationException("network should not be used for local image"),
            logger);
        var baseContext = Context("""<p><img src="/assets/huge.png"></p>""");
        var context = WithLogger(baseContext, logger);
        var assetsDir = Path.Combine(context.OutputDir, "assets");
        Directory.CreateDirectory(assetsDir);
        WritePngLikeFile(Path.Combine(assetsDir, "huge.png"), ImageConverter.ContentImageMaxBytes + 1);

        var html = await processor.ProcessImagesAsync(
            context,
            """<p><img src="/assets/huge.png"></p>""",
            Options("app", "secret") with { ProcessImages = true },
            CancellationToken.None);

        Assert.Contains("/assets/huge.png", html, StringComparison.Ordinal);
        Assert.Equal(0, gateway.UploadContentImageCount);
        Assert.Contains(logger.Warnings, warning => warning.Contains("too large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ContentImageProcessor_RejectsOversizedMediaCacheImageBeforeConversion()
    {
        var logger = new RecordingLogger();
        var gateway = new FakeWechatDraftGateway();
        var processor = new ContentImageProcessor(
            gateway,
            (_, _) => throw new InvalidOperationException("network should not be used after oversized cache hit"),
            logger);
        var cacheDir = Path.Combine(_rootDir, ".cache", "media");
        Directory.CreateDirectory(cacheDir);
        WritePngLikeFile(Path.Combine(cacheDir, "huge.png"), ImageConverter.ContentImageMaxBytes + 1);
        File.WriteAllText(Path.Combine(cacheDir, ".media-index.json"), """
{
  "https://cdn.example.com/huge.png": "huge.png"
}
""");
        var context = WithMediaDownloadDir(
            WithLogger(Context("""<p><img src="https://cdn.example.com/huge.png"></p>"""), logger),
            cacheDir);

        var html = await processor.ProcessImagesAsync(
            context,
            """<p><img src="https://cdn.example.com/huge.png"></p>""",
            Options("app", "secret") with { ProcessImages = true },
            CancellationToken.None);

        Assert.Contains("https://cdn.example.com/huge.png", html, StringComparison.Ordinal);
        Assert.Equal(0, gateway.UploadContentImageCount);
        Assert.Contains(logger.Warnings, warning => warning.Contains("too large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ThumbResolver_RejectsOversizedLocalCoverBeforeConversion()
    {
        var logger = new RecordingLogger();
        var gateway = new FakeWechatDraftGateway();
        var resolver = new ThumbResolver(
            gateway,
            (_, _) => throw new InvalidOperationException("network should not be used for local cover"),
            logger);
        var context = WithLogger(ContextWithCover("<p>Hello</p>", "/assets/cover.png"), logger);
        WritePngLikeFile(Path.Combine(context.OutputDir, "assets", "cover.png"), ImageConverter.MaterialImageMaxBytes + 1);
        var options = Options("app", "secret") with
        {
            DefaultThumbMediaId = null,
            DefaultImageUrl = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAndUploadThumbAsync(
                context,
                context.Routed[0].Item,
                options,
                new SyncCache(2, new Dictionary<string, SyncRecord>(StringComparer.Ordinal)),
                CancellationToken.None));

        Assert.Equal(0, gateway.UploadThumbCount);
        Assert.Contains(logger.Warnings, warning => warning.Contains("too large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ThumbResolver_RejectsOversizedMediaCacheCoverBeforeConversion()
    {
        var logger = new RecordingLogger();
        var gateway = new FakeWechatDraftGateway();
        var resolver = new ThumbResolver(
            gateway,
            (_, _) => Task.FromResult(Array.Empty<byte>()),
            logger);
        var cacheDir = Path.Combine(_rootDir, ".cache", "media");
        Directory.CreateDirectory(cacheDir);
        WritePngLikeFile(Path.Combine(cacheDir, "huge.png"), ImageConverter.MaterialImageMaxBytes + 1);
        File.WriteAllText(Path.Combine(cacheDir, ".media-index.json"), """
{
  "https://cdn.example.com/cover.png": "huge.png"
}
""");
        var context = WithMediaDownloadDir(
            WithLogger(ContextWithCover("<p>Hello</p>", "https://cdn.example.com/cover.png"), logger),
            cacheDir);
        var options = Options("app", "secret") with
        {
            DefaultThumbMediaId = null,
            DefaultImageUrl = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAndUploadThumbAsync(
                context,
                context.Routed[0].Item,
                options,
                new SyncCache(2, new Dictionary<string, SyncRecord>(StringComparer.Ordinal)),
                CancellationToken.None));

        Assert.Equal(0, gateway.UploadThumbCount);
        Assert.Contains(logger.Warnings, warning => warning.Contains("too large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ThumbResolver_FallsBackToHashMediaCacheWhenIndexFileIsInvalid()
    {
        var logger = new RecordingLogger();
        var gateway = new FakeWechatDraftGateway();
        var resolver = new ThumbResolver(
            gateway,
            (_, _) => Task.FromResult(Array.Empty<byte>()),
            logger);
        var cacheDir = Path.Combine(_rootDir, ".cache", "media");
        Directory.CreateDirectory(cacheDir);
        var coverUrl = "https://cdn.example.com/cover.png";
        var normalizedKey = WechatSyncHelpers.NormalizeMediaSourceUrlKey(coverUrl);
        File.WriteAllBytes(Path.Combine(cacheDir, "empty.png"), []);
        File.WriteAllText(Path.Combine(cacheDir, ".media-index.json"), $$"""
{
  "{{normalizedKey}}": "empty.png"
}
""");
        var hashFileName = WechatSyncHelpers.BuildMediaCacheStableFileName(
            normalizedKey,
            WechatSyncHelpers.ResolveMediaCacheExtension(coverUrl));
        File.WriteAllBytes(Path.Combine(cacheDir, hashFileName), TinyPng);
        var context = WithMediaDownloadDir(
            WithLogger(ContextWithCover("<p>Hello</p>", coverUrl), logger),
            cacheDir);
        var options = Options("app", "secret") with
        {
            DefaultThumbMediaId = null,
            DefaultImageUrl = null
        };

        var (thumbMediaId, cacheUpdated) = await resolver.ResolveAndUploadThumbAsync(
            context,
            context.Routed[0].Item,
            options,
            new SyncCache(2, new Dictionary<string, SyncRecord>(StringComparer.Ordinal)),
            CancellationToken.None);

        Assert.Equal("uploaded-thumb", thumbMediaId);
        Assert.True(cacheUpdated);
        Assert.Equal(1, gateway.UploadThumbCount);
        Assert.Contains(logger.Warnings, warning => warning.Contains("file empty", StringComparison.OrdinalIgnoreCase));
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
        var cache = SyncCacheManager.LoadCache(
            Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json"),
            new ConsoleLogger(LogLevel.Error));
        Assert.Empty(cache.Records);
        var operation = Assert.Single(cache.Operations).Value;
        Assert.Equal("PublishFailed", operation.State);
        Assert.Equal(2, operation.LastPublishStatus);
    }

    [Fact]
    public async Task RunAsync_PublishFailurePersistsThumbCacheWithoutSuccessfulRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway { PublishStatus = 2 };
        var workflow = new WechatSyncWorkflow(gateway, delayAsync: (_, _) => Task.CompletedTask);
        var context = ContextWithCover("<p>Hello</p>", "/assets/cover.png");
        var options = Options(appId.Name, secret.Name) with
        {
            DefaultThumbMediaId = null,
            Target = "publish",
            PublishPollMaxAttempts = 1,
            PublishPollIntervalSeconds = 1
        };

        var result = await workflow.RunAsync(context, options);

        var cache = SyncCacheManager.LoadCache(Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json"), new ConsoleLogger(LogLevel.Error));
        Assert.False(result.Success);
        Assert.Empty(cache.Records);
        Assert.NotEmpty(cache.ThumbMediaIds);
        Assert.Equal("PublishFailed", Assert.Single(cache.Operations).Value.State);
        Assert.Equal(1, gateway.UploadThumbCount);
    }

    [Fact]
    public async Task RunAsync_DraftFailurePersistsThumbCacheWithoutSuccessfulRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway { FailAddDraftOnAttempt = 1 };
        var workflow = new WechatSyncWorkflow(gateway, delayAsync: (_, _) => Task.CompletedTask);
        var context = ContextWithCover("<p>Hello</p>", "/assets/cover.png");
        var options = Options(appId.Name, secret.Name) with
        {
            DefaultThumbMediaId = null,
            MaxAttempts = 1
        };

        var result = await workflow.RunAsync(context, options);

        var cache = SyncCacheManager.LoadCache(Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json"), new ConsoleLogger(LogLevel.Error));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Empty(cache.Records);
        Assert.NotEmpty(cache.ThumbMediaIds);
        Assert.Equal("DraftSubmitting", Assert.Single(cache.Operations).Value.State);
        Assert.Equal(1, gateway.UploadThumbCount);
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

        var result = await workflow.RunAsync(context, options);

        var cachePath = Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json");
        var cache = SyncCacheManager.LoadCache(cachePath, new ConsoleLogger(LogLevel.Error));
        Assert.False(result.Success);
        Assert.Equal(1, result.Synced);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Contains("notion:page-1", cache.Records.Keys);
        Assert.DoesNotContain("notion:page-2", cache.Records.Keys);
        Assert.Equal("DraftSubmitting", cache.Operations["notion:page-2"].State);
    }

    [Fact]
    public async Task RunAsync_RetriesCoverUploadFailureWithinSyncAttemptBudget()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway { FailUploadThumbOnAttempt = 1 };
        var workflow = new WechatSyncWorkflow(gateway, delayAsync: (_, _) => Task.CompletedTask);
        var context = ContextWithCover("<p>Hello</p>", "/assets/cover.png");
        var options = Options(appId.Name, secret.Name) with
        {
            DefaultThumbMediaId = null,
            MaxAttempts = 2
        };

        var result = await workflow.RunAsync(context, options);

        Assert.True(result.Success);
        Assert.Equal(1, result.Synced);
        Assert.Equal(2, gateway.UploadThumbCount);
        Assert.Equal(1, gateway.AddDraftCount);
    }

    [Fact]
    public async Task RunAsync_ForceReuploadsChangedLocalCoverInsteadOfReusingThumbCache()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new FakeWechatDraftGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        var context = ContextWithCover("<p>Hello</p>", "/assets/cover.png");
        var coverPath = Path.Combine(context.OutputDir, "assets", "cover.png");
        var options = Options(appId.Name, secret.Name) with { DefaultThumbMediaId = null };

        await workflow.RunAsync(context, options);
        var originalWriteTime = File.GetLastWriteTimeUtc(coverPath);
        File.WriteAllBytes(coverPath, TinyPngVariant());
        File.SetLastWriteTimeUtc(coverPath, originalWriteTime);
        var forced = await workflow.RunAsync(context, options with { Force = true });

        Assert.True(forced.Success);
        Assert.Equal(1, forced.Synced);
        Assert.Equal(0, forced.Skipped);
        Assert.Equal(2, gateway.AddDraftCount);
        Assert.Equal(2, gateway.UploadThumbCount);
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
    public void SyncCacheManager_RejectsCachePathOutsideWechatSyncCacheDirectory()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SyncCacheManager.ResolvePath(_rootDir, "site.yaml"));

        Assert.Contains(".cache/wechat-sync", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SyncCacheManager_AllowsCachePathUnderWechatSyncCacheDirectory()
    {
        var path = SyncCacheManager.ResolvePath(_rootDir, ".cache/wechat-sync/custom.json");

        Assert.Equal(Path.Combine(_rootDir, ".cache", "wechat-sync", "custom.json"), path);
    }

    [Fact]
    public void SyncCacheManager_RejectsCachePathWhenCacheDirectoryIsSymlinkOutsideProject()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-cache-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, ".cache"));
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_rootDir, ".cache", "wechat-sync"), outsideDir);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SyncCacheManager.ResolvePath(_rootDir, ".cache/wechat-sync/sync-cache.json"));

            Assert.Contains("symbolic link", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
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
    public void ThumbResolver_RejectsLocalAssetSymlinkEscapingOutputDirectory()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-asset-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            var context = Context("<p>Hello</p>");
            var assetsDir = Path.Combine(context.OutputDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var outsideImage = Path.Combine(outsideDir, "secret.png");
            File.WriteAllBytes(outsideImage, TinyPng);
            File.CreateSymbolicLink(Path.Combine(assetsDir, "secret.png"), outsideImage);

            var resolved = ThumbResolver.TryResolveLocalAssetPath(context, "/assets/secret.png", out var filePath);

            Assert.False(resolved);
            Assert.Equal(string.Empty, filePath);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
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

    [Fact]
    public void ThumbResolver_RejectsMediaIndexSymlinkEscapingDownloadDirectory()
    {
        var downloadDir = Path.Combine(_rootDir, ".cache", "media");
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-media-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(downloadDir);
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outsideImage = Path.Combine(outsideDir, "secret.png");
            File.WriteAllBytes(outsideImage, TinyPng);
            File.CreateSymbolicLink(Path.Combine(downloadDir, "linked.png"), outsideImage);
            File.WriteAllText(Path.Combine(downloadDir, ".media-index.json"), """
{
  "https://example.com/secret.png": "linked.png"
}
""");

            var resolved = ThumbResolver.TryResolveFromMediaIndex(downloadDir, "https://example.com/secret.png");

            Assert.Null(resolved);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public void ThumbResolver_RejectsMediaCacheDirectorySymlinkEscapingProjectRoot()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-media-dir-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, ".cache"));
        try
        {
            File.WriteAllBytes(Path.Combine(outsideDir, "secret.png"), TinyPng);
            File.WriteAllText(Path.Combine(outsideDir, ".media-index.json"), """
{
  "https://example.com/secret.png": "secret.png"
}
""");
            Directory.CreateSymbolicLink(Path.Combine(_rootDir, ".cache", "media"), outsideDir);
            var context = Context("<p>Hello</p>");

            var downloadDir = ThumbResolver.ResolveEffectiveMediaDownloadDir(context);
            var resolved = ThumbResolver.TryResolveFromMediaIndex(downloadDir, "https://example.com/secret.png");

            Assert.Null(resolved);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    private WechatSyncContext Context(string html)
        => Context(("post-1", "page-1", html));

    private WechatSyncContext ContextWithCover(string html, string coverPath)
    {
        var context = Context(html);
        var assetsDir = Path.Combine(context.OutputDir, "assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllBytes(Path.Combine(assetsDir, "cover.png"), TinyPng);

        var (item, route) = context.Routed[0];
        var fields = item.Fields.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        fields["cover"] = new WechatSyncField("string", coverPath);

        return new WechatSyncContext
        {
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            SiteName = context.SiteName,
            SiteUrl = context.SiteUrl,
            Logger = context.Logger,
            Routed = [(item with { Fields = fields }, route)]
        };
    }

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
                    ["summary"] = "Summary",
                    ["manifestReviewStatus"] = "approved",
                    ["reviewStatus"] = "approved",
                    ["syncStatus"] = string.Empty
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

    private static WechatSyncContext WithReviewStatus(WechatSyncContext context, string reviewStatus)
    {
        var (item, route) = Assert.Single(context.Routed);
        var metadata = item.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        metadata["manifestReviewStatus"] = reviewStatus;
        metadata["reviewStatus"] = reviewStatus;
        return new WechatSyncContext
        {
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            SiteName = context.SiteName,
            SiteUrl = context.SiteUrl,
            MediaDownloadDir = context.MediaDownloadDir,
            Logger = context.Logger,
            Routed = [(item with { Metadata = metadata }, route)]
        };
    }

    private static WechatSyncContext WithExpiresAt(WechatSyncContext context, DateTimeOffset expiresAt)
    {
        var (item, route) = Assert.Single(context.Routed);
        var metadata = item.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        metadata["expiresAt"] = expiresAt;
        return new WechatSyncContext
        {
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            SiteName = context.SiteName,
            SiteUrl = context.SiteUrl,
            MediaDownloadDir = context.MediaDownloadDir,
            Logger = context.Logger,
            Routed = [(item with { Metadata = metadata }, route)]
        };
    }

    private static WechatSyncContext WithLogger(WechatSyncContext context, ILogger logger)
        => new()
        {
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            SiteName = context.SiteName,
            SiteUrl = context.SiteUrl,
            MediaDownloadDir = context.MediaDownloadDir,
            Logger = logger,
            Routed = context.Routed
        };

    private static WechatSyncContext WithMediaDownloadDir(WechatSyncContext context, string mediaDownloadDir)
        => new()
        {
            RootDir = context.RootDir,
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            SiteName = context.SiteName,
            SiteUrl = context.SiteUrl,
            MediaDownloadDir = mediaDownloadDir,
            Logger = context.Logger,
            Routed = context.Routed
        };

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
        public int UploadThumbCount { get; private set; }
        public int UploadContentImageCount { get; private set; }
        public int PublishCount { get; private set; }
        public List<WechatDraftRequest> Requests { get; } = [];
        public int PublishStatus { get; init; }
        public int? FailAddDraftOnAttempt { get; init; }
        public int? FailUploadThumbOnAttempt { get; init; }
        public bool CancelAddDraft { get; init; }
        public bool CancelPublish { get; init; }
        public Action? OnAddDraft { get; init; }

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            AddDraftCount++;
            Requests.Add(request);
            OnAddDraft?.Invoke();
            if (CancelAddDraft)
            {
                throw new OperationCanceledException("draft canceled");
            }

            if (AddDraftCount == FailAddDraftOnAttempt)
            {
                throw new InvalidOperationException("draft failed");
            }

            return Task.FromResult("draft-" + AddDraftCount);
        }

        public Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            UploadThumbCount++;
            if (UploadThumbCount == FailUploadThumbOnAttempt)
            {
                throw new InvalidOperationException("thumb upload failed");
            }

            return Task.FromResult("uploaded-thumb");
        }

        public Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            UploadContentImageCount++;
            return Task.FromResult("https://mmbiz.qpic.cn/image.jpg");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
        {
            PublishCount++;
            if (CancelPublish)
            {
                throw new OperationCanceledException("publish canceled");
            }

            return Task.FromResult("publish-1");
        }

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

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
            => Warnings.Add(message);

        public void Error(string message)
        {
        }
    }

    private static void WritePngLikeFile(string path, int length)
    {
        var bytes = new byte[length];
        TinyPng.CopyTo(bytes, 0);
        File.WriteAllBytes(path, bytes);
    }

    private static byte[] TinyPngVariant()
    {
        var bytes = (byte[])TinyPng.Clone();
        bytes[^1] = (byte)(bytes[^1] ^ 0x01);
        return bytes;
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
