using System.Security.Cryptography;
using System.Text;
using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WxP103ContentPipelineTests : IDisposable
{
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

    private readonly string _rootDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-wx-p1-03-" + Guid.NewGuid().ToString("N"));

    public WxP103ContentPipelineTests()
        => Directory.CreateDirectory(_rootDir);

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("<p><img src=\"data:image/gif;base64,placeholder\" data-src=\"https://images.example.com/lazy.png\" loading=\"lazy\"></p>", "https://images.example.com/lazy.png")]
    [InlineData("<p><img srcset=\"https://images.example.com/small.png 400w, https://images.example.com/large.png 1200w\"></p>", "https://images.example.com/large.png")]
    public async Task RunAsync_ProcessesLazyImageCandidateBeforeCleanup(string html, string expectedDownloadUrl)
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new RecordingGateway();
        var downloadedUrls = new List<string>();
        var workflow = new WechatSyncWorkflow(
            gateway,
            downloadImageAsync: (url, _) =>
            {
                downloadedUrls.Add(url);
                return Task.FromResult(TinyPng);
            });

        var result = await workflow.RunAsync(Context(html), Options(appId.Name, secret.Name) with { ProcessImages = true });

        var request = Assert.Single(gateway.Requests);
        Assert.True(result.Success);
        Assert.NotEmpty(downloadedUrls);
        Assert.All(downloadedUrls, url => Assert.Equal(expectedDownloadUrl, url));
        Assert.Equal(1, gateway.UploadContentImageCount);
        Assert.Contains("src=\"https://mmbiz.qpic.cn/uploaded.jpg\"", request.ContentHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-src", request.ContentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("srcset", request.ContentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", request.ContentHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ImageFailureCleansLazyAttributesAndRetainsOriginalSrcFallback()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new RecordingGateway();
        var workflow = new WechatSyncWorkflow(
            gateway,
            downloadImageAsync: (_, _) => throw new InvalidOperationException("download failed"));
        const string placeholder = "data:image/gif;base64,placeholder";

        var result = await workflow.RunAsync(
            Context($"<p><img src=\"{placeholder}\" data-src=\"https://images.example.com/lazy.png\" loading=\"lazy\"></p>"),
            Options(appId.Name, secret.Name) with { ProcessImages = true });

        var request = Assert.Single(gateway.Requests);
        Assert.True(result.Success);
        Assert.Equal(0, gateway.UploadContentImageCount);
        Assert.Contains($"src=\"{placeholder}\"", request.ContentHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-src", request.ContentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("loading=", request.ContentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://images.example.com/lazy.png", request.ContentHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithoutImageProcessingStillCleansLazyAttributes()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new RecordingGateway();
        var workflow = new WechatSyncWorkflow(gateway);

        await workflow.RunAsync(
            Context("<p><img src=\"https://images.example.com/fallback.png\" data-src=\"https://images.example.com/lazy.png\"></p>"),
            Options(appId.Name, secret.Name));

        var request = Assert.Single(gateway.Requests);
        Assert.Equal(0, gateway.UploadContentImageCount);
        Assert.Contains("src=\"https://images.example.com/fallback.png\"", request.ContentHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-src", request.ContentHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_PassthroughPreservesLazyAttributesWithoutUploading()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new RecordingGateway();
        var workflow = new WechatSyncWorkflow(gateway);
        const string html = "<p><img src=\"data:image/gif;base64,placeholder\" data-src=\"https://images.example.com/lazy.png\"></p>";

        await workflow.RunAsync(Context(html), Options(appId.Name, secret.Name) with { ProcessImages = true, Passthrough = true });

        var request = Assert.Single(gateway.Requests);
        Assert.Equal(0, gateway.UploadContentImageCount);
        Assert.Equal(html, request.ContentHtml);
    }

    [Fact]
    public async Task RunAsync_InvalidatesOnlyLegacyProcessedImageSuccessRecord()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");
        var gateway = new RecordingGateway();
        var workflow = new WechatSyncWorkflow(gateway, downloadImageAsync: (_, _) => Task.FromResult(TinyPng));
        const string html = "<p><img src=\"https://images.example.com/fallback.png\" data-src=\"https://images.example.com/lazy.png\"></p>";
        var context = Context(html);
        var options = Options(appId.Name, secret.Name) with { ProcessImages = true };
        var item = context.Routed[0].Item;
        var route = context.Routed[0].Route;
        var legacyHash = ComputeLegacyContentHash(item, route, html, options);
        var cachePath = Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        SyncCacheManager.SaveCache(cachePath, new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal)
        {
            ["notion:page-1"] = new SyncRecord(DateTimeOffset.UtcNow, "legacy-draft", legacyHash, "notion", "page-1", "Hello")
        }));

        var result = await workflow.RunAsync(context, options);
        var cache = SyncCacheManager.LoadCache(cachePath, new ConsoleLogger(LogLevel.Error));

        Assert.True(result.Success);
        Assert.Equal(1, result.Synced);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(3, cache.Version);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ComputeContentHash_PreservesLegacyHashForUnaffectedModes(bool processImages, bool passthrough)
    {
        const string html = "<p><img src=\"https://images.example.com/fallback.png\" data-src=\"https://images.example.com/lazy.png\"></p>";
        var context = Context(html);
        var options = Options("app", "secret") with { ProcessImages = processImages, Passthrough = passthrough };
        var item = context.Routed[0].Item;
        var route = context.Routed[0].Route;

        var hash = SyncCacheManager.ComputeContentHash(item, route, html, options, context);

        Assert.Equal(ComputeLegacyContentHash(item, route, html, options), hash);
    }

    private WechatSyncContext Context(string html)
    {
        var item = new WechatSyncItem(
            Id: "post-1",
            Title: "Hello",
            Slug: "post-1",
            PublishAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ContentHtml: html,
            Metadata: new Dictionary<string, object>
            {
                ["sourceKey"] = "notion",
                ["sourceId"] = "page-1",
                ["summary"] = "Summary",
                ["manifestReviewStatus"] = "approved",
                ["reviewStatus"] = "approved"
            },
            Fields: new Dictionary<string, WechatSyncField>
            {
                ["type"] = new("string", "post")
            });
        var route = new WechatSyncRoute("/posts/post-1/", Path.Combine(_rootDir, "dist", "posts", "post-1", "index.html"), "post");
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
            ForceRetryIgnoreCacheEnv: "",
            Author: null,
            DefaultThumbMediaId: "thumb-media-id",
            NeedOpenComment: false,
            OnlyFansCanComment: false,
            SiteName: "Bukit",
            SiteUrl: "https://example.com",
            BaseUrl: "/");

    private static string ComputeLegacyContentHash(
        WechatSyncItem item,
        WechatSyncRoute route,
        string html,
        WechatSyncOptions options)
    {
        var author = string.IsNullOrWhiteSpace(options.Author) ? options.SiteName : options.Author;
        var contentSourceUrl = WechatSyncHelpers.CombineAbsoluteUrl(options.SiteUrl, options.BaseUrl, route.Url);
        var summary = WechatSyncHelpers.ReadMetaString(item.Metadata, "summary");
        var thumbSource = ThumbResolver.ResolveThumbSource(item, options) ?? string.Empty;
        var payload = string.Join('\n',
            "wechat-sync-cache-v3",
            item.Id,
            item.Title ?? string.Empty,
            html,
            route.Url,
            summary,
            author,
            contentSourceUrl,
            thumbSource,
            options.DefaultThumbMediaId ?? string.Empty,
            options.DefaultImageUrl ?? string.Empty,
            options.NeedOpenComment.ToString(),
            options.OnlyFansCanComment.ToString(),
            options.SiteUrl ?? string.Empty,
            options.BaseUrl,
            options.ProcessImages.ToString(),
            options.Passthrough.ToString(),
            options.Target,
            string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private sealed class RecordingGateway : IWechatDraftGateway
    {
        public int AddDraftCount { get; private set; }
        public int UploadContentImageCount { get; private set; }
        public List<WechatDraftRequest> Requests { get; } = [];

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            AddDraftCount++;
            Requests.Add(request);
            return Task.FromResult("draft-" + AddDraftCount);
        }

        public Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
            => Task.FromResult("thumb-media-id");

        public Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            UploadContentImageCount++;
            return Task.FromResult("https://mmbiz.qpic.cn/uploaded.jpg");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
            => Task.FromResult("publish-1");

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(string publishId, CancellationToken cancellationToken)
            => Task.FromResult(new WechatPublishStatusResult(publishId, 0, null));
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
}
