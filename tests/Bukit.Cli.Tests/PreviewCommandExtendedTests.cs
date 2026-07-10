using System.Net;
using System.Net.Http;
using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Cli.Commands.Dev;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class PreviewCommandExtendedTests : IDisposable
{
    private static readonly TimeSpan s_requestTimeout = TimeSpan.FromSeconds(5);
    private readonly string _tempDir;

    private static readonly MethodInfo s_getContentType = typeof(PreviewCommand)
        .GetMethod("GetContentType", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_parsePort = typeof(PreviewCommand)
        .GetMethod("ParsePort", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_resolveDisableAnalytics = typeof(PreviewCommand)
        .GetMethod("ResolveDisableAnalyticsInPreview", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_createAndStartListener = typeof(PreviewCommand)
        .GetMethod("CreateAndStartListener", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_isPortConflict = typeof(PreviewCommand)
        .GetMethod("IsPortConflict", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_handleRequest = typeof(PreviewCommand)
        .GetMethod("HandleRequest", BindingFlags.NonPublic | BindingFlags.Static)!;

    public PreviewCommandExtendedTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-preview-ext-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("test.html", "text/html; charset=utf-8")]
    [InlineData("style.css", "text/css; charset=utf-8")]
    [InlineData("script.js", "application/javascript; charset=utf-8")]
    [InlineData("data.json", "application/json; charset=utf-8")]
    [InlineData("feed.xml", "application/xml; charset=utf-8")]
    [InlineData("image.png", "image/png")]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("animation.gif", "image/gif")]
    [InlineData("icon.svg", "image/svg+xml")]
    [InlineData("favicon.ico", "image/x-icon")]
    [InlineData("FAVICON.ICO", "image/x-icon")]
    [InlineData("site.webmanifest", "application/manifest+json; charset=utf-8")]
    [InlineData("image.webp", "image/webp")]
    [InlineData("image.avif", "image/avif")]
    [InlineData("font.woff", "font/woff")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("script.js.map", "application/json; charset=utf-8")]
    [InlineData("document.pdf", "application/pdf")]
    [InlineData("robots.txt", "text/plain; charset=utf-8")]
    [InlineData("file.unknown", "application/octet-stream")]
    public void GetContentType_ReturnsCorrectMimeType(string filename, string expected)
    {
        var result = (string)s_getContentType.Invoke(null, new object[] { filename })!;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_DisableTrue_StripsGtagScripts()
    {
        var html = """
            <html><head>
              <script async src="https://www.googletagmanager.com/gtag/js?id=G-ABC123"></script>
              <script>
                window.dataLayer = window.dataLayer || [];
                function gtag(){dataLayer.push(arguments);}
                gtag('js', new Date());
                gtag('config', 'G-ABC123');
              </script>
            </head><body>content</body></html>
            """;

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, disableAnalytics: true);

        Assert.DoesNotContain("googletagmanager.com/gtag/js", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("gtag('config'", filtered, StringComparison.Ordinal);
        Assert.Contains("<body>content</body>", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_DisableFalse_ReturnsUnchanged()
    {
        var html = "<html><script>gtag('config', 'G-ABC123');</script></html>";
        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, disableAnalytics: false);
        Assert.Equal(html, filtered);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_NullHtml_ReturnsNull()
    {
        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(null!, disableAnalytics: true);
        Assert.Null(filtered);
    }

    [Theory]
    [InlineData("auto", 0)]
    [InlineData("8080", 8080)]
    [InlineData("invalid", -1)]
    [InlineData("-1", -1)]
    public void ParsePort_ReturnsExpected(string portText, int expected)
    {
        var result = (int)s_parsePort.Invoke(null, new object[] { portText })!;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveDisableAnalyticsInPreview_NoSiteYaml_ReturnsFalse()
    {
        var previewDir = Path.Combine(_tempDir, "no-config");
        Directory.CreateDirectory(previewDir);

        var result = (bool)s_resolveDisableAnalytics.Invoke(null, new object[] { previewDir })!;
        Assert.False(result);
    }

    [Fact]
    public void ResolveDisableAnalyticsInPreview_DisableFalse_ReturnsFalse()
    {
        var previewDir = Path.Combine(_tempDir, "with-config");
        Directory.CreateDirectory(previewDir);
        File.WriteAllText(Path.Combine(previewDir, "site.yaml"), """
                                                                  site:
                                                                    name: test
                                                                    title: Test
                                                                  content:
                                                                    sources:
                                                                      - type: markdown
                                                                        name: page
                                                                        collection: page
                                                                        markdown:
                                                                          dir: content
                                                                  """);

        var result = (bool)s_resolveDisableAnalytics.Invoke(null, new object[] { previewDir })!;
        Assert.False(result);
    }

    [Fact]
    public void CreateAndStartListener_WithPortZero_StartsAndStopsCleanly()
    {
        var result = s_createAndStartListener.Invoke(null, new object[] { "localhost", 0, false })!;
        var type = result.GetType();
        var listener = (HttpListener)type.GetField("Item1")!.GetValue(result)!;
        var prefix = (string)type.GetField("Item2")!.GetValue(result)!;

        Assert.StartsWith("http://localhost:", prefix, StringComparison.Ordinal);
        Assert.True(listener.IsListening);

        listener.Stop();
        listener.Close();
    }

    [Fact]
    public void IsPortConflict_WithConflictMessage_ReturnsTrue()
    {
        var ex = new HttpListenerException(0, "conflicts with an existing registration");
        var result = (bool)s_isPortConflict.Invoke(null, new object[] { ex })!;
        Assert.True(result);
    }

    [Fact]
    public void IsPortConflict_WithAccessDenied_ReturnsTrue()
    {
        var ex = new HttpListenerException(0, "Access is denied");
        var result = (bool)s_isPortConflict.Invoke(null, new object[] { ex })!;
        Assert.True(result);
    }

    [Fact]
    public void IsPortConflict_WithUnrelatedMessage_ReturnsFalse()
    {
        var ex = new HttpListenerException(0, "some other error");
        var result = (bool)s_isPortConflict.Invoke(null, new object[] { ex })!;
        Assert.False(result);
    }

    [Fact]
    public async Task HandleRequest_RootIndexHtml_StripsAnalyticsWhenDisabled()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), """
            <html><head>
              <script async src="https://www.googletagmanager.com/gtag/js?id=G-ABC123"></script>
              <script>gtag('config', 'G-ABC123');</script>
            </head><body>root</body></html>
            """);

        var response = await SendRequestAsync("/", disableAnalytics: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        Assert.Contains("root", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("googletagmanager.com/gtag/js", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("gtag('config'", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleRequest_DirectoryWithoutExtension_FallsBackToNestedIndex()
    {
        var postsDir = Path.Combine(_tempDir, "posts");
        Directory.CreateDirectory(postsDir);
        File.WriteAllText(Path.Combine(postsDir, "index.html"), "<html><body>nested</body></html>");

        var response = await SendRequestAsync("/posts", disableAnalytics: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("nested", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleRequest_StaticAsset_ReturnsFileBytes()
    {
        var assetsDir = Path.Combine(_tempDir, "assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "site.css"), "body{color:red;}");

        var response = await SendRequestAsync("/assets/site.css", disableAnalytics: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css; charset=utf-8", response.ContentType);
        Assert.Equal("body{color:red;}", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingFile_ReturnsNotFound()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), "<html></html>");

        var response = await SendRequestAsync("/missing", disableAnalytics: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Preview_RejectsEncodedDotDotPath()
    {
        Assert.Null(DevPathGuard.TryResolveWithinRoot(_tempDir, "/%2e%2e/"));
    }

    [Fact]
    public async Task Preview_RejectsDoubleEncodedDotDotPath()
    {
        var response = await SendRequestAsync("/%252e%252e/", disableAnalytics: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(_tempDir, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_RejectsBackslashTraversal()
    {
        var response = await SendRequestAsync("/%5c..%5csecret", disableAnalytics: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(_tempDir, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_RejectsMixedSeparatorTraversal()
    {
        Assert.Null(DevPathGuard.TryResolveWithinRoot(_tempDir, "/assets%5c..%2fsecret"));
    }

    [Fact]
    public async Task Preview_RejectsUnicodeNormalizationTraversal()
    {
        var response = await SendRequestAsync("/%EF%BC%8E%EF%BC%8E/secret", disableAnalytics: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(_tempDir, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_RejectsVeryLongPathWithoutCrash()
    {
        var response = await SendRequestAsync("/" + new string('a', 1024), disableAnalytics: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(_tempDir, response.Body, StringComparison.Ordinal);
    }

    private async Task<(HttpStatusCode StatusCode, string Body, string? ContentType)> SendRequestAsync(string path, bool disableAnalytics)
    {
        var result = s_createAndStartListener.Invoke(null, new object[] { "localhost", 0, false })!;
        var tupleType = result.GetType();
        var listener = (HttpListener)tupleType.GetField("Item1")!.GetValue(result)!;
        var prefix = (string)tupleType.GetField("Item2")!.GetValue(result)!;

        try
        {
            using var client = new HttpClient { Timeout = s_requestTimeout };
            var responseTask = client.GetAsync(new Uri(new Uri(prefix), path));
            var contextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(s_requestTimeout);
            var first = await Task.WhenAny(contextTask, responseTask, timeoutTask);

            if (first == timeoutTask)
            {
                throw new TimeoutException("preview request did not complete in time");
            }

            if (first == contextTask)
            {
                var context = await contextTask;
                s_handleRequest.Invoke(null, new object[] { _tempDir, context, disableAnalytics });

                using var responseAfterContext = await responseTask;
                var bodyAfterContext = await responseAfterContext.Content.ReadAsStringAsync();
                return (responseAfterContext.StatusCode, bodyAfterContext, responseAfterContext.Content.Headers.ContentType?.ToString());
            }

            using var responseAfterTimeout = await responseTask;
            var contextAfterTimeout = await contextTask.WaitAsync(s_requestTimeout);
            s_handleRequest.Invoke(null, new object[] { _tempDir, contextAfterTimeout, disableAnalytics });

            var bodyAfterTimeout = await responseAfterTimeout.Content.ReadAsStringAsync();
            return (responseAfterTimeout.StatusCode, bodyAfterTimeout, responseAfterTimeout.Content.Headers.ContentType?.ToString());
        }
        finally
        {
            listener.Close();
        }
    }
}
