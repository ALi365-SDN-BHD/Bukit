using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class ContentImageProcessorCaseTests : IDisposable
{
    private readonly string _rootDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-wechat-image-case-" + Guid.NewGuid().ToString("N"));

    public ContentImageProcessorCaseTests()
        => Directory.CreateDirectory(_rootDir);

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessImagesAsync_UploadsCaseDistinctUrlsSeparately()
    {
        var gateway = new RecordingGateway();
        var processor = new ContentImageProcessor(
            gateway,
            (_, _) => Task.FromResult(TinyPng),
            new SilentLogger());
        const string html =
            "<p><img src=\"https://cdn.example.com/A.png\"><img src=\"https://cdn.example.com/a.png\"></p>";

        var result = await processor.ProcessImagesAsync(
            Context(html),
            html,
            Options(),
            CancellationToken.None);

        Assert.Equal(2, gateway.UploadContentImageCount);
        Assert.Contains("https://mmbiz.qpic.cn/uploaded-1.png", result, StringComparison.Ordinal);
        Assert.Contains("https://mmbiz.qpic.cn/uploaded-2.png", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessImagesAsync_DeduplicatesTheSameUrlOnce()
    {
        var gateway = new RecordingGateway();
        var processor = new ContentImageProcessor(
            gateway,
            (_, _) => Task.FromResult(TinyPng),
            new SilentLogger());
        const string html =
            "<p><img src=\"https://cdn.example.com/same.png\"><img src=\"https://cdn.example.com/same.png\"></p>";

        var result = await processor.ProcessImagesAsync(
            Context(html),
            html,
            Options(),
            CancellationToken.None);

        Assert.Equal(1, gateway.UploadContentImageCount);
        Assert.Equal(2, result.Split("https://mmbiz.qpic.cn/uploaded-1.png").Length - 1);
    }

    [Fact]
    public async Task ProcessImagesAsync_KeepsQueryCaseDistinct()
    {
        var gateway = new RecordingGateway();
        var processor = new ContentImageProcessor(
            gateway,
            (_, _) => Task.FromResult(TinyPng),
            new SilentLogger());
        const string html =
            "<p><img src=\"https://cdn.example.com/image.png?v=A\"><img src=\"https://cdn.example.com/image.png?v=a\"></p>";

        var result = await processor.ProcessImagesAsync(
            Context(html),
            html,
            Options(),
            CancellationToken.None);

        Assert.Equal(2, gateway.UploadContentImageCount);
        Assert.Contains("uploaded-1.png", result, StringComparison.Ordinal);
        Assert.Contains("uploaded-2.png", result, StringComparison.Ordinal);
    }

    private WechatSyncContext Context(string html)
    {
        var item = new WechatSyncItem(
            "post-1",
            "Title",
            "post-1",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            html,
            new Dictionary<string, object>(),
            new Dictionary<string, WechatSyncField>());
        return new WechatSyncContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = new SilentLogger(),
            Routed =
            [
                (item, new WechatSyncRoute(
                    "/posts/post-1/",
                    Path.Combine(_rootDir, "dist", "posts", "post-1", "index.html"),
                    "post"))
            ]
        };
    }

    private static WechatSyncOptions Options()
        => new(
            SourceNames: [],
            ContentTypes: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            DefaultTypesWhenMissing: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            CacheFile: ".cache/wechat-sync/sync-cache.json",
            MaxAttempts: 1,
            BaseDelayMs: 1,
            BackoffFactor: 1,
            AppIdEnv: "APP_ID",
            AppSecretEnv: "APP_SECRET",
            ForceRetryIgnoreCacheEnv: string.Empty,
            Author: null,
            DefaultThumbMediaId: "thumb-media-id",
            NeedOpenComment: false,
            OnlyFansCanComment: false,
            SiteName: "Bukit",
            SiteUrl: "https://example.com",
            BaseUrl: "/",
            ProcessImages: true);

    private sealed class RecordingGateway : IWechatDraftGateway
    {
        public int UploadContentImageCount { get; private set; }

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
            => Task.FromResult("draft-1");

        public Task<string> UploadThumbAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
            => Task.FromResult("thumb-1");

        public Task<string> UploadContentImageAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            UploadContentImageCount++;
            return Task.FromResult($"https://mmbiz.qpic.cn/uploaded-{UploadContentImageCount}.png");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
            => Task.FromResult("publish-1");

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(
            string publishId,
            CancellationToken cancellationToken)
            => Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
    }

    private sealed class SilentLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
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
