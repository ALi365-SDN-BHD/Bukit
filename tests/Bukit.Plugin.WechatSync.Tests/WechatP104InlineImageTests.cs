using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatP104InlineImageTests : IDisposable
{
    private readonly string _rootDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-p1-04-" + Guid.NewGuid().ToString("N"));

    public WechatP104InlineImageTests()
    {
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
    public async Task RunAsync_ReportsInlineContractViolationWithoutDraftSubmissionOrRetry()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new RecordingGateway { InlineImageException = new WechatDraftContractViolationException("plugin.wechat-sync.contract.inlineImage.bytes", "inline uploadimg must be smaller than 1048576 bytes.") };
        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: (_, _) => Task.FromResult(TinyPng));

        var context = Context("<p><img src=\"https://cdn.example.com/inline.png\"></p>");
        var result = await workflow.RunAsync(context, Options(appId.Name, secret.Name) with { ProcessImages = true, MaxAttempts = 2 });

        Assert.False(result.Success);
        Assert.Equal(1, gateway.UploadContentImageCount);
        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.contract.inlineImage.bytes");
        var cache = SyncCacheManager.LoadCache(result.CachePath, context.Logger);
        Assert.DoesNotContain(cache.Operations.Values, operation => operation.State == "DraftSubmitting");
    }

    [Fact]
    public async Task ProcessImagesAsync_NormalizesOneMiBPlusOneLocalPngWithoutNetworkDownload()
    {
        var gateway = new RecordingGateway();
        var downloadCalls = 0;
        var processor = new ContentImageProcessor(
            gateway,
            (_, _) =>
            {
                downloadCalls++;
                return Task.FromException<byte[]>(new InvalidOperationException("network download must not be used for a local file"));
            },
            new SilentLogger());
        var context = Context("<img src=\"/assets/exact.png\">");
        var assetDirectory = Path.Combine(context.OutputDir, "assets");
        Directory.CreateDirectory(assetDirectory);
        File.WriteAllBytes(
            Path.Combine(assetDirectory, "exact.png"),
            PaddedTinyPng(WechatDraftContract.InlineImageMaxBytesExclusive + 1));

        var html = await processor.ProcessImagesAsync(context, "<img src=\"/assets/exact.png\">", Options("app", "secret"), CancellationToken.None);

        Assert.Equal(0, downloadCalls);
        Assert.Single(gateway.UploadedContentImages);
        Assert.True(gateway.UploadedContentImages[0].Length < WechatDraftContract.InlineImageMaxBytesExclusive);
        Assert.Contains("https://mmbiz.qpic.cn/uploaded.png", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessImagesAsync_NormalizesOneMiBPlusOneMediaCachePngWithoutNetworkDownload()
    {
        const string imageUrl = "https://cdn.example.com/cache.png";
        var gateway = new RecordingGateway();
        var downloadCalls = 0;
        var processor = new ContentImageProcessor(
            gateway,
            (_, _) =>
            {
                downloadCalls++;
                return Task.FromException<byte[]>(new InvalidOperationException("network download must not be used for a media-cache hit"));
            },
            new SilentLogger());
        var mediaDirectory = Path.Combine(_rootDir, ".cache", "media");
        Directory.CreateDirectory(mediaDirectory);
        File.WriteAllBytes(
            Path.Combine(mediaDirectory, "cache.png"),
            PaddedTinyPng(WechatDraftContract.InlineImageMaxBytesExclusive + 1));
        File.WriteAllText(Path.Combine(mediaDirectory, ".media-index.json"), $$"""
{
  "{{imageUrl}}": "cache.png"
}
""");
        var context = WithMediaDownloadDir(Context($"<img src=\"{imageUrl}\">"), mediaDirectory);

        var html = await processor.ProcessImagesAsync(context, $"<img src=\"{imageUrl}\">", Options("app", "secret"), CancellationToken.None);

        Assert.Equal(0, downloadCalls);
        Assert.Single(gateway.UploadedContentImages);
        Assert.True(gateway.UploadedContentImages[0].Length < WechatDraftContract.InlineImageMaxBytesExclusive);
        Assert.Contains("https://mmbiz.qpic.cn/uploaded.png", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeForUpload_WithStrictOneMiBLimitNeverReturnsBoundarySizedPayload()
    {
        var normalized = ImageConverter.NormalizeForUpload(
            PaddedTinyPng(),
            WechatDraftContract.InlineImageMaxBytesExclusive,
            requireStrictlyBelowMaxBytes: true);

        Assert.NotNull(normalized);
        Assert.True(normalized.Value.Bytes.Length < WechatDraftContract.InlineImageMaxBytesExclusive);
    }

    [Fact]
    public async Task UploadContentImageAsync_RejectsValidExactOneMiBPngBeforeTokenOrHttpActivity()
    {
        using var gateway = new WechatDraftGateway(new SilentLogger(), "app", "secret");

        var exception = await Assert.ThrowsAsync<WechatDraftContractViolationException>(() =>
            gateway.UploadContentImageAsync(PaddedTinyPng(), "exact.png", "image/png", CancellationToken.None));

        Assert.Equal("plugin.wechat-sync.contract.inlineImage.bytes", exception.Code);
    }

    private WechatSyncContext Context(string html)
    {
        var item = new WechatSyncItem(
            "post-1",
            "Title",
            "post-1",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            html,
            new Dictionary<string, object>
            {
                ["sourceKey"] = "notion",
                ["sourceId"] = "page-1",
                ["summary"] = "Summary",
                ["manifestReviewStatus"] = "approved",
                ["reviewStatus"] = "approved"
            },
            new Dictionary<string, WechatSyncField> { ["type"] = new("string", "post") });
        return new WechatSyncContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = new SilentLogger(),
            Routed = [(item, new WechatSyncRoute("/posts/post-1/", Path.Combine(_rootDir, "dist", "posts", "post-1", "index.html"), "post"))]
        };
    }

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
        => new([], new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase), new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase), ".cache/wechat-sync/sync-cache.json", 1, 1, 1, appIdEnv, appSecretEnv, "", null, "thumb-media-id", false, false, "Bukit", "https://example.com", "/", ProcessImages: true);

    private static byte[] PaddedTinyPng(int length = WechatDraftContract.InlineImageMaxBytesExclusive)
    {
        var bytes = new byte[length];
        TinyPng.CopyTo(bytes, 0);
        return bytes;
    }

    private sealed class RecordingGateway : IWechatDraftGateway
    {
        public int AddDraftCount { get; private set; }
        public int UploadContentImageCount { get; private set; }
        public List<byte[]> UploadedContentImages { get; } = [];
        public Exception? InlineImageException { get; init; }

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            AddDraftCount++;
            return Task.FromResult("draft-1");
        }

        public Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
            => Task.FromResult("thumb-1");

        public Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            UploadContentImageCount++;
            if (InlineImageException is not null)
            {
                return Task.FromException<string>(InlineImageException);
            }

            UploadedContentImages.Add(bytes);
            return Task.FromResult("https://mmbiz.qpic.cn/uploaded.png");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
            => Task.FromResult("publish-1");

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(string publishId, CancellationToken cancellationToken)
            => Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
    }

    private sealed class SilentLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public string Name { get; }

        public EnvironmentVariableScope(string name, string value)
        {
            Name = name;
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
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
