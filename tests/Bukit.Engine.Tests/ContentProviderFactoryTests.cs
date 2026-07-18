using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Content.Media;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentProviderFactoryTests
{
    [Fact]
    public void Create_WithMarkdownSource_ReturnsMarkdownProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_content_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
                Content = ContentConfigFactory.FromSources(
                [
                    TestContent.MarkdownSource()
                ])
            };
            var logger = new ConsoleLogger(LogLevel.Debug);

            var provider = ContentProviderFactory.Create(config, tempDir, false, logger);

            Assert.NotNull(provider);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Create_WithMultipleMarkdownSources_ReturnsCompositeProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_content_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var postsDir = Path.Combine(tempDir, "content", "posts");
        var pagesDir = Path.Combine(tempDir, "content", "pages");
        Directory.CreateDirectory(postsDir);
        Directory.CreateDirectory(pagesDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
                Content = ContentConfigFactory.FromSources(
                [
                    TestContent.MarkdownSource("content/posts", "posts"),
                    TestContent.MarkdownSource("content/pages", "pages")
                ])
            };
            var logger = new ConsoleLogger(LogLevel.Debug);

            var provider = ContentProviderFactory.Create(config, tempDir, false, logger);

            Assert.NotNull(provider);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Create_WithEmptySources_ThrowsConfigException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_content_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
                Content = ContentConfigFactory.FromSources([])
            };
            var logger = new ConsoleLogger(LogLevel.Debug);

            var ex = Assert.Throws<ConfigException>(() => ContentProviderFactory.Create(config, tempDir, false, logger));
            Assert.Contains("content.sources is required", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task LocalizeContentImagesAsync_WithEmptyItems_ReturnsEmptyResult()
    {
        var items = Array.Empty<ContentDocument>();
        var result = new RawContentLoadResult(
            Array.Empty<RawContentDocument>(),
            NullContentBodyStore.Instance);

        var media = new MediaConfig();
        var logger = new ConsoleLogger(LogLevel.Debug);

        var localized = await ContentProviderFactory.LocalizeContentImagesAsync(
            result, media, "/tmp", "/tmp/cache", logger, CancellationToken.None);

        Assert.NotNull(localized);
        Assert.Empty(localized.Documents);
    }

    [Fact]
    public async Task LocalizeContentImagesAsync_WithNoImages_ReturnsSameItems()
    {
        var items = new List<ContentDocument>
        {
            ContentDocument.Create(
                "test",
                "Test",
                "test",
                DateTimeOffset.UtcNow,
                "<p>Hello world</p>",
                null)
        };

        var result = new RawContentLoadResult(ToRawDocuments(items), NullContentBodyStore.Instance);
        var media = new MediaConfig();
        var logger = new ConsoleLogger(LogLevel.Debug);

        var localized = await ContentProviderFactory.LocalizeContentImagesAsync(
            result, media, "/tmp", "/tmp/cache", logger, CancellationToken.None);

        Assert.NotNull(localized);
        Assert.Single(localized.Documents);
    }

    [Fact]
    public async Task LocalizeContentImagesAsync_AfterFieldImageIsCached_LocalizesDelayedBodyImage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_localizer_lifetime_" + Guid.NewGuid().ToString("N"));
        var cacheDir = Path.Combine(tempDir, "cache");
        Directory.CreateDirectory(cacheDir);

        try
        {
            using var server = new LoopbackImageServer(Encoding.UTF8.GetBytes("body-image"));
            var fieldImageUrl = server.BaseUrl + "field.jpg";
            var bodyImageUrl = server.BaseUrl + "body.jpg";
            var cachedFieldImageName = BuildCachedImageName(fieldImageUrl);
            await File.WriteAllTextAsync(Path.Combine(cacheDir, cachedFieldImageName), "field-image");

            var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["cover"] = new("file", fieldImageUrl)
            };
            var document = new RawContentDocument(
                Id: "delayed-body",
                Title: "Delayed body",
                Slug: "delayed-body",
                PublishAt: DateTimeOffset.UtcNow,
                Body: new RawBody(null, "delayed-body"),
                Properties: RawContentValue.FromFields(fields),
                CustomFields: fields);
            var inner = new DelayedBodyStore($"<p><img src=\"{bodyImageUrl}\"></p>");
            var result = new RawContentLoadResult([document], inner);
            var media = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = cacheDir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/default.jpg",
                BlockPrivateNetworks = false,
                MaxRetries = 0,
                RetryBaseDelayMs = 0
            };

            var localized = await ContentProviderFactory.LocalizeContentImagesAsync(
                result, media, tempDir, cacheDir, new ConsoleLogger(LogLevel.Debug), CancellationToken.None);
            await using var ownedBodyStore = Assert.IsAssignableFrom<IAsyncDisposable>(localized.BodyStore);

            var localizedField = Assert.IsType<string>(localized.Documents[0].CustomFields!["cover"].Value);
            Assert.StartsWith("/assets/uploads/", localizedField, StringComparison.Ordinal);

            var body = await localized.BodyStore.GetAsync(localized.Documents[0]);

            Assert.Contains("/assets/uploads/", body.Html, StringComparison.Ordinal);
            Assert.DoesNotContain(media.DefaultImageUrl, body.Html, StringComparison.Ordinal);
            Assert.Equal(1, server.RequestCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeContentImagesAsync_WhenInitialLocalizationFails_DisposesLocalizer()
    {
        var localizer = new ThrowingDisposableLocalizer(new InvalidOperationException("localize failed"));
        var result = CreateInlineImageLoadResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ContentProviderFactory.LocalizeContentImagesAsync(
                result,
                new MediaConfig(),
                Path.GetTempPath(),
                Path.GetTempPath(),
                new ConsoleLogger(LogLevel.Debug),
                CancellationToken.None,
                (_, _) => localizer));

        Assert.Equal(1, localizer.DisposeCount);
    }

    [Fact]
    public async Task LocalizeContentImagesAsync_WhenInitialLocalizationIsCanceled_DisposesLocalizer()
    {
        var localizer = new ThrowingDisposableLocalizer(new OperationCanceledException());
        var result = CreateInlineImageLoadResult();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ContentProviderFactory.LocalizeContentImagesAsync(
                result,
                new MediaConfig(),
                Path.GetTempPath(),
                Path.GetTempPath(),
                new ConsoleLogger(LogLevel.Debug),
                CancellationToken.None,
                (_, _) => localizer));

        Assert.Equal(1, localizer.DisposeCount);
    }

    private static RawContentLoadResult CreateInlineImageLoadResult()
    {
        var document = new RawContentDocument(
            Id: "inline-image",
            Title: "Inline image",
            Slug: "inline-image",
            PublishAt: DateTimeOffset.UtcNow,
            Body: new RawBody("<img src=\"https://img.example/image.jpg\">", null));
        return new RawContentLoadResult([document], NullContentBodyStore.Instance);
    }

    private static string BuildCachedImageName(string sourceUrl)
    {
        var uri = new Uri(sourceUrl);
        var normalized = $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}:{uri.Port}{uri.AbsolutePath}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16] + ".jpg";
    }

    private static IReadOnlyList<RawContentDocument> ToRawDocuments(IEnumerable<ContentDocument> items)
        => items
            .Select(item => new RawContentDocument(
                Id: item.Id,
                Title: item.Title,
                Slug: item.Slug,
                PublishAt: item.PublishAt,
                Body: new RawBody(item.Body.Html, item.Body.BodyKey, item.Body.Markdown, item.Body.PlainText),
                Properties: RawContentValue.FromFields(item.CustomFields),
                Source: item.Source,
                CustomFields: item.CustomFields))
            .ToArray();

    private sealed class DelayedBodyStore : IContentBodyStore
    {
        private readonly string _html;

        public DelayedBodyStore(string html)
        {
            _html = html;
        }

        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(_html));
    }

    private sealed class ThrowingDisposableLocalizer : IImageAssetLocalizer, IDisposable
    {
        private readonly Exception _exception;
        private int _disposeCount;

        public ThrowingDisposableLocalizer(Exception exception)
        {
            _exception = exception;
        }

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
            => Task.FromException<string>(_exception);

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    private sealed class LoopbackImageServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serveTask;
        private int _requestCount;

        public LoopbackImageServer(byte[] body)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            BaseUrl = $"http://127.0.0.1:{endpoint.Port}/";
            _serveTask = Task.Run(() => ServeAsync(body));
        }

        public string BaseUrl { get; }
        public int RequestCount => Volatile.Read(ref _requestCount);

        public void Dispose()
        {
            _listener.Stop();
            try
            {
                _serveTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Test cleanup only.
            }
        }

        private async Task ServeAsync(byte[] body)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref _requestCount);
                await using var stream = client.GetStream();
                await ReadRequestHeadersAsync(stream);

                var headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: image/jpeg\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(headers);
                await stream.WriteAsync(body);
            }
            catch
            {
                // The listener is stopped during test cleanup.
            }
        }

        private static async Task ReadRequestHeadersAsync(NetworkStream stream)
        {
            var buffer = new byte[1024];
            var request = new StringBuilder();
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    return;
                }

                request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                if (request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
    }

    [Fact]
    public void CreateNotionProvider_WithNotionConfig_ReturnsNotionProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_notion_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
                Content = ContentConfigFactory.FromSources(
                [
                    TestContent.NotionSource("test-db-id", "db")
                ])
            };
            var logger = new ConsoleLogger(LogLevel.Debug);

            var ex = Assert.Throws<ConfigException>(() => ContentProviderFactory.Create(config, tempDir, false, logger));
            Assert.Contains("NOTION_TOKEN", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
