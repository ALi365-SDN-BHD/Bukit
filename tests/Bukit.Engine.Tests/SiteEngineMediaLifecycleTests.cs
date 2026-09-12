using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Content;
using Bukit.Content.Media;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed partial class SiteEngineIntegrationTests
{
    [Theory]
    [InlineData("/assets/uploads", false)]
    [InlineData("/media", false)]
    [InlineData("media", true)]
    public async Task MediaLifecycle_FormatSwitchRefreshesIncrementalHtml(string urlBase, bool multiLanguage)
    {
        var (root, config) = CreateMediaSite(multiLanguage, "/site/", urlBase);
        using var server = new MediaImageServer(width: 1000);
        try
        {
            var output = Path.Combine(root, "dist", multiLanguage ? "en" : "");
            var pageFile = Path.Combine(output, "blog", "one", "index.html");
            foreach (var enabled in new[] { false, true, false, true })
            {
                var configPath = Path.Combine(root, "image-formats.yaml");
                File.WriteAllText(configPath, $$"""
                    site:
                      name: media
                      title: Media
                    content:
                      sources:
                        - type: markdown
                          markdown:
                            dir: content
                    theme:
                      images:
                        enabled: true
                        formats: {{(enabled ? "[webp]" : "[]")}}
                        sizes: [480]
                    """);
                config = config with
                {
                    Theme = config.Theme with
                    {
                        Images = ConfigLoader.Load(configPath).Theme.Images
                    }
                };
                await BuildMediaAsync(root, config, [MediaDocument("one")], $"<img src=\"{server.Url}\" alt=\"fallback\">");
                var page = File.ReadAllText(pageFile);
                Assert.Equal(enabled, page.Contains("<source type=\"image/webp\"", StringComparison.Ordinal));
                Assert.Contains("-480w.png 480w", page);
                Assert.Contains("alt=\"fallback\"", page);
                Assert.DoesNotContain("data-bukit-generated-srcset", page);
                if (enabled)
                {
                    Assert.Contains("-480w.webp 480w", page);
                    Assert.Single(Directory.EnumerateFiles(Path.Combine(output, urlBase.Trim('/')), "*-480w.webp"));
                }
            }
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task MediaLifecycle_WebpOutputsHtmlAndIncrementalManifestStayConsistent()
    {
        var (root, config) = CreateMediaSite(baseUrl: "/site/");
        using var server = new MediaImageServer(width: 1000);
        try
        {
            config = config with
            {
                Theme = config.Theme with
                {
                    Images = new ImageOptimizationConfig
                    {
                        Enabled = true,
                        Formats = new[] { "webp" },
                        Sizes = new[] { 480 },
                        Quality = 78
                    }
                }
            };
            var document = MediaDocument("one");
            var content = $"<img src=\"{server.Url}\" sizes=\"80vw\" alt=\"WebP fallback\" loading=\"lazy\">";

            await BuildMediaAsync(root, config, [document], content);
            var output = Path.Combine(root, "dist");
            var mediaDir = Path.Combine(output, "assets", "uploads");
            var source = Assert.Single(
                Directory.EnumerateFiles(mediaDir, "*.png"),
                path => !Path.GetFileNameWithoutExtension(path).EndsWith("-480w", StringComparison.Ordinal));
            var stem = Path.GetFileNameWithoutExtension(source);
            var responsive = Path.Combine(mediaDir, $"{stem}-480w.webp");
            var full = Path.Combine(mediaDir, $"{stem}-1000w.webp");
            var retainedTimestamp = DateTime.UnixEpoch.AddDays(1);
            File.SetLastWriteTimeUtc(responsive, retainedTimestamp);
            await BuildMediaAsync(root, config, [document], content);
            Assert.Equal(retainedTimestamp, File.GetLastWriteTimeUtc(responsive));
            Assert.Equal(480, Image.Identify(responsive).Width);
            Assert.Equal(1000, Image.Identify(full).Width);

            config = config with
            {
                Theme = config.Theme with { Images = config.Theme.Images! with { Quality = 79 } }
            };
            await BuildMediaAsync(root, config, [document], content);
            Assert.NotEqual(retainedTimestamp, File.GetLastWriteTimeUtc(responsive));
            var freshness = JsonNode.Parse(File.ReadAllText(responsive + ".bukit-freshness.json"))!.AsObject();
            Assert.Equal(79, freshness["quality"]!.GetValue<int>());

            var page = File.ReadAllText(Path.Combine(output, "blog", "one", "index.html"));
            Assert.Contains("<picture><source type=\"image/webp\"", page, StringComparison.Ordinal);
            Assert.Contains($"/site/assets/uploads/{stem}-480w.webp 480w", page, StringComparison.Ordinal);
            Assert.Contains($"/site/assets/uploads/{stem}-1000w.webp 1000w", page, StringComparison.Ordinal);
            Assert.Contains($"src=\"/site/assets/uploads/{Path.GetFileName(source)}\"", page, StringComparison.Ordinal);
            Assert.Contains("sizes=\"80vw\" alt=\"WebP fallback\" loading=\"lazy\"", page, StringComparison.Ordinal);

            var manifest = BuildManifest.Load(Path.Combine(root, ".cache", "build-manifest.json"));
            Assert.Contains(manifest.PluginOutputs.Values, output => output.Path.EndsWith($"{stem}-480w.webp", StringComparison.Ordinal));
            Assert.Contains(manifest.PluginOutputs.Values, output => output.Path.EndsWith($"{stem}-1000w.webp.bukit-freshness.json", StringComparison.Ordinal));
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(output));
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData("/assets/uploads", false)]
    [InlineData("/media", false)]
    [InlineData("media", true)]
    public async Task MediaLifecycle_ImageConfigurationChangesRefreshIncrementalHtml(string urlBase, bool multiLanguage)
    {
        var (root, config) = CreateMediaSite(multiLanguage, "/site/", urlBase);
        using var server = new MediaImageServer(width: 1000);
        try
        {
            var document = MediaDocument("one");
            var html = $"<img src=\"{server.Url}\" alt=\"A > B\">";
            var output = Path.Combine(root, "dist", multiLanguage ? "en" : "");
            var pageFile = Path.Combine(output, "blog", "one", "index.html");
            foreach (var sizes in new[] { new[] { 480, 768 }, new[] { 480 }, new[] { 480, 600 } })
            {
                config = config with { Theme = config.Theme with { Images = new ImageOptimizationConfig { Enabled = true, Sizes = sizes } } };
                await BuildMediaAsync(root, config, [document], html);
                var page = File.ReadAllText(pageFile);
                Assert.Contains("alt=\"A > B\"", page);
                foreach (var size in sizes)
                {
                    Assert.Contains($"-{size}w.png {size}w", page);
                    Assert.Single(Directory.EnumerateFiles(Path.Combine(output, urlBase.Trim('/')), $"*-{size}w.png"));
                }
                if (!sizes.Contains(768))
                {
                    Assert.DoesNotContain("-768w", page);
                    Assert.Empty(Directory.EnumerateFiles(Path.Combine(output, urlBase.Trim('/')), "*-768w.png"));
                }
            }
            config = config with { Theme = config.Theme with { Images = config.Theme.Images! with { Enabled = false } } };
            await BuildMediaAsync(root, config, [document], html);
            Assert.DoesNotContain("srcset=", File.ReadAllText(pageFile));
            config = config with { Theme = config.Theme with { Images = config.Theme.Images! with { Enabled = true } } };
            await BuildMediaAsync(root, config, [document], html);
            Assert.Contains("-600w.png 600w", File.ReadAllText(pageFile));
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData("<a data-href=\"/assets/uploads/ghost.png\" href=\"/about/\">About</a>")]
    [InlineData("<a title=\"href='/assets/uploads/ghost.png'\" href=\"/about/\">About</a>")]
    public async Task MediaLifecycle_IgnoresHrefTextOutsideTheAnchorHref(string html)
    {
        var (root, config) = CreateMediaSite(baseUrl: "/site/");
        try
        {
            await BuildMediaAsync(root, config, [MediaDocument("one")], html);
            var output = Path.Combine(root, "dist");
            Assert.Contains(html, File.ReadAllText(Path.Combine(output, "blog", "one", "index.html")));
            Assert.False(File.Exists(Path.Combine(output, "assets", "uploads", "ghost.png")));
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(output));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task MediaLifecycle_ColdAndWarmDelayedBodyPublishesOnlyReferencedBytes()
    {
        var (root, config) = CreateMediaSite();
        using var server = new MediaImageServer();
        try
        {
            var document = MediaDocument("one");
            var html = $"<img src=\"{server.Url}\" alt=\"图注\">";
            await BuildMediaAsync(root, config, [document], html);
            var cacheFile = Assert.Single(Directory.EnumerateFiles(Path.Combine(root, ".cache", "media"), "*.png", SearchOption.AllDirectories));
            var destination = Path.Combine(root, "dist", "assets", "uploads", Path.GetFileName(cacheFile));
            Assert.True(File.Exists(destination), "A completed cold build must publish its localized body image.");
            Assert.Equal(SHA256.HashData(server.Bytes), SHA256.HashData(File.ReadAllBytes(destination)));
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(cacheFile)!, "historical.png"), server.Bytes);
            await BuildMediaAsync(root, config, [document], html);
            Assert.Equal(1, server.RequestCount);
            Assert.False(File.Exists(Path.Combine(root, "dist", "assets", "uploads", "historical.png")));
            Assert.Equal(SHA256.HashData(server.Bytes), SHA256.HashData(File.ReadAllBytes(destination)));
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(false, "/", "/assets/uploads")]
    [InlineData(false, "/site/", "media")]
    [InlineData(true, "/site/", "/media")]
    public async Task MediaLifecycle_SharedWithdrawalAndVariantUrlsKeepExactOwners(bool multiLanguage, string baseUrl, string urlBase)
    {
        var (root, config) = CreateMediaSite(multiLanguage, baseUrl, urlBase);
        using var server = new MediaImageServer();
        try
        {
            var one = MediaDocument("one");
            var two = MediaDocument("two", multiLanguage ? "zh" : "en");
            var html = $"<img src=\"{server.Url}\" srcset=\"  {server.Url} 1x, {server.Url} 2x\" alt=\"共享图\"><a href=\"{server.Url}\">原图</a>";
            await BuildMediaAsync(root, config, [one, two], html);
            var cached = Assert.Single(Directory.EnumerateFiles(Path.Combine(root, ".cache", "media"), "*.png"));
            var name = Path.GetFileName(cached);
            var prefix = "/" + urlBase.Trim().Trim('/');
            foreach (var (document, language) in new[] { (one, "en"), (two, multiLanguage ? "zh" : "en") })
            {
                var variantRoot = Path.Combine(root, "dist", multiLanguage ? language : "");
                var publicUrl = baseUrl.TrimEnd('/') + (multiLanguage ? "/" + language : "") + prefix + "/" + name;
                var page = File.ReadAllText(Path.Combine(variantRoot, "blog", document.Slug, "index.html"));
                Assert.Contains("src=\"" + publicUrl + "\"", page);
                Assert.Contains("srcset=\"  " + publicUrl + " 1x, " + publicUrl + " 2x\"", page);
                Assert.Contains("href=\"" + publicUrl + "\"", page);
                Assert.Equal(SHA256.HashData(server.Bytes), SHA256.HashData(File.ReadAllBytes(Path.Combine(variantRoot, prefix.TrimStart('/'), name))));
            }
            await BuildMediaAsync(root, config, [two], html);
            var retained = Path.Combine(root, "dist", multiLanguage ? "zh" : "", prefix.TrimStart('/'), name);
            Assert.True(File.Exists(retained));
            if (multiLanguage) Assert.False(File.Exists(Path.Combine(root, "dist", "en", prefix.TrimStart('/'), name)));
            await BuildMediaAsync(root, config, [], html);
            Assert.False(File.Exists(retained));
            Assert.True(File.Exists(cached));
            var manifest = BuildManifest.Load(Path.Combine(root, ".cache", "build-manifest.json"));
            Assert.DoesNotContain(manifest.OwnedOutputs, path => path.EndsWith(name, StringComparison.Ordinal));
            Assert.Equal(1, server.RequestCount);
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MediaLifecycle_DownloadFailureRequiresAnExistingPlannedFallback(bool fallbackExists)
    {
        var (root, config) = CreateMediaSite();
        using var server = new MediaImageServer(fail: true);
        try
        {
            var output = Path.Combine(root, "dist");
            if (fallbackExists)
            {
                var fallback = Path.Combine(root, "static", "assets", "images", "noneimg-news.jpg");
                Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                File.WriteAllBytes(fallback, server.Bytes);
                await BuildMediaAsync(root, config, [MediaDocument("one")], $"<img src=\"{server.Url}\" alt=\"描述\">");
                Assert.False(BuildRecoveryTracker.HasIncompleteBuild(output));
                Assert.Equal(SHA256.HashData(server.Bytes), SHA256.HashData(File.ReadAllBytes(Path.Combine(output, "assets", "images", "noneimg-news.jpg"))));
            }
            else
            {
                var exception = await Assert.ThrowsAsync<IOException>(() => BuildMediaAsync(root, config, [MediaDocument("one")], $"<img src=\"{server.Url}\" alt=\"描述\">"));
                Assert.Contains("Referenced localized media output is missing", exception.Message);
                Assert.True(BuildRecoveryTracker.HasIncompleteBuild(output));
            }
            Assert.True(server.RequestCount > 0);
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MediaLifecycle_ExactOrStructuralConflictFailsBeforeRendering(bool structural)
    {
        var (root, config) = CreateMediaSite();
        using var server = new MediaImageServer();
        try
        {
            var cache = Path.Combine(root, ".cache", "media");
            using var localizer = new ImageAssetLocalizer(config.Content.Media with { DownloadDir = cache });
            var url = await localizer.LocalizeAsync(server.Url, CancellationToken.None);
            var staticPath = Path.Combine(root, "static", structural ? "assets/uploads" : url.TrimStart('/'));
            Directory.CreateDirectory(Path.GetDirectoryName(staticPath)!);
            File.WriteAllText(staticPath, "static owner");
            var exception = await Assert.ThrowsAsync<BukitException>(() => BuildMediaAsync(root, config, [MediaDocument("one")], $"<img src=\"{server.Url}\" alt=\"描述\">"));
            Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
            Assert.False(File.Exists(Path.Combine(root, "dist", "blog", "one", "index.html")));
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task MediaLifecycle_StableSourceFingerprintStillHashesResolvedBody()
    {
        var (root, config) = CreateMediaSite();
        using var first = new MediaImageServer();
        using var second = new MediaImageServer();
        try
        {
            var original = MediaDocument("one");
            var fields = new Dictionary<string, ContentField>(original.CustomFields!) { ["bodyFingerprint"] = new("text", "constant-source-fingerprint") };
            var document = original with { CustomFields = fields };
            await BuildMediaAsync(root, config, [document], $"<img src=\"{first.Url}\" alt=\"first\">");
            var oldFile = Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "dist", "assets", "uploads"), "*.png"));
            await BuildMediaAsync(root, config, [document], $"<img src=\"{second.Url}\" alt=\"second\">");
            Assert.Contains("alt=\"second\"", File.ReadAllText(Path.Combine(root, "dist", "blog", "one", "index.html")));
            Assert.False(File.Exists(oldFile));
            Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "dist", "assets", "uploads"), "*.png"));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task MediaLifecycle_FieldOnlyMediaIsPublishedAndHistoricalCacheIsExcluded()
    {
        var (root, config) = CreateMediaSite(baseUrl: "/site/", urlBase: "/media");
        using var server = new MediaImageServer();
        try
        {
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "<html><body><img src=\"{{ page.fields.cover.value }}\" alt=\"cover\">{{ page.content }}</body></html>");
            var original = MediaDocument("one");
            var document = original with { CustomFields = new Dictionary<string, ContentField>(original.CustomFields!) { ["cover"] = new("file", server.Url) } };
            await BuildMediaAsync(root, config, [document], "<p>No body image</p>");
            var cached = Assert.Single(Directory.EnumerateFiles(Path.Combine(root, ".cache", "media"), "*.png"));
            var publicUrl = "/site/media/" + Path.GetFileName(cached);
            Assert.Contains("src=\"" + publicUrl + "\"", File.ReadAllText(Path.Combine(root, "dist", "blog", "one", "index.html")));
            Assert.Equal(SHA256.HashData(server.Bytes), SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "dist", "media", Path.GetFileName(cached)))));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task MediaLifecycle_UnplannedHtmlReferenceCannotSucceedUsingAnOldPublicFile()
    {
        var (root, config) = CreateMediaSite();
        using var server = new MediaImageServer();
        try
        {
            var document = MediaDocument("one");
            var html = $"<img src=\"{server.Url}\" alt=\"描述\">";
            await BuildMediaAsync(root, config, [document], html);
            var oldPublicFile = Path.Combine(root, "dist", "assets", "uploads", "unplanned.png");
            File.WriteAllBytes(oldPublicFile, server.Bytes);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "<html><body>{{ page.content }}<img src=\"/assets/uploads/unplanned.png\" alt=\"Unplanned\"></body></html>");
            var exception = await Assert.ThrowsAsync<IOException>(() => BuildMediaAsync(root, config, [document], html));
            Assert.Contains("unplanned.png", exception.Message);
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task MediaLifecycle_LeadingWhitespaceSrcsetMissingFileCannotComplete()
    {
        var (root, config) = CreateMediaSite();
        try
        {
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "<html><body>{{ page.content }}<img srcset=\" /assets/uploads/missing.png 1x\" alt=\"Missing\"></body></html>");
            var exception = await Assert.ThrowsAsync<IOException>(() => BuildMediaAsync(root, config, [MediaDocument("one")], "<p>Published body</p>"));
            Assert.Contains("missing.png", exception.Message);
            var output = Path.Combine(root, "dist");
            Assert.Contains("srcset=\" /assets/uploads/missing.png 1x\"", File.ReadAllText(Path.Combine(output, "blog", "one", "index.html")));
            Assert.False(File.Exists(Path.Combine(output, "assets", "uploads", "missing.png")));
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(output));
        }
        finally { CleanupDir(root); }
    }

    private static (string Root, AppConfig Config) CreateMediaSite(bool multiLanguage = false, string baseUrl = "/", string urlBase = "/assets/uploads")
    {
        var (root, config) = CreateBuildReportHealthSite(multiLanguage);
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "<html><head><title>{{ page.title }}</title></head><body>{{ page.content }}</body></html>");
        return (root, config with
        {
            Site = config.Site with { BaseUrl = baseUrl },
            Content = config.Content with { Media = new MediaConfig { BlockPrivateNetworks = false, MaxRetries = 0, UrlBase = urlBase } }
        });
    }

    private static ContentDocument MediaDocument(string id, string language = "en")
        => ContentDocument.Create(id, id, id, DateTimeOffset.Parse("2026-06-01T00:00:00Z"), null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post", ["language"] = language }), bodyKey: id);

    private static Task<BuildResult> BuildMediaAsync(string root, AppConfig config, ContentDocument[] documents, string html)
        => new SiteEngine(new TestLogger(), new MediaProviderFactory(documents, html), new DefaultSearchIndexBuilder())
            .BuildAsync(config, root, new ConfigOverrides { Clean = false, Incremental = true });

    private sealed class MediaProviderFactory(ContentDocument[] documents, string html) : IContentProviderFactory
    {
        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
            => new StaticContentProvider(new RawContentLoadResult(ToRawDocuments(documents), new DelayedMediaBodyStore(html)));
        public Task<RawContentLoadResult> LocalizeContentImagesAsync(RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
            => ContentProviderFactory.LocalizeContentImagesAsync(result, media, rootDir, cacheDir, logger, cancellationToken);
    }

    private sealed class DelayedMediaBodyStore(string html) : IContentBodyStore
    {
        public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);
            return new ContentBody(html);
        }
    }

    private sealed class MediaImageServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task _server;
        private int _requests;
        public byte[] Bytes { get; }
        public string Url { get; }
        public int RequestCount => Volatile.Read(ref _requests);
        public MediaImageServer(bool fail = false, int width = 2)
        {
            using var image = new Image<Rgba32>(width, 2);
            using var bytes = new MemoryStream();
            image.SaveAsPng(bytes);
            Bytes = bytes.ToArray();
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/image.png";
            _server = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        using var client = await _listener.AcceptTcpClientAsync();
                        Interlocked.Increment(ref _requests);
                        await using var stream = client.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                        while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {(fail ? "404 Not Found" : "200 OK")}\r\nContent-Type: image/png\r\nContent-Length: {Bytes.Length}\r\nConnection: close\r\n\r\n"));
                        await stream.WriteAsync(Bytes);
                    }
                }
                catch (SocketException) { }
            });
        }
        public void Dispose()
        {
            _listener.Stop();
            _server.GetAwaiter().GetResult();
        }
    }
}
