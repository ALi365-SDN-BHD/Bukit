using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Cli.Commands.Dev;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("CWD")]
public sealed class PreviewCommandExtendedTests : IDisposable
{
    private static readonly TimeSpan s_requestTimeout = TimeSpan.FromSeconds(5);
    private readonly string _tempDir;

    private static readonly MethodInfo s_getContentType = typeof(PreviewCommand)
        .GetMethod("GetContentType", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_parsePort = typeof(PreviewCommand)
        .GetMethod("ParsePort", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_resolveRemoveManagedAnalytics = typeof(PreviewCommand)
        .GetMethod("ResolveRemoveManagedAnalyticsInPreview", BindingFlags.NonPublic | BindingFlags.Static)!;

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
    public void ApplyPreviewAnalyticsPolicy_RemoveTrue_StripsManagedBlockOnly()
    {
        var html = """
            <html><head>
              <!-- bukit:analytics:google-analytics:G-ABC123:head:start -->
              <script>managed</script>
              <!-- bukit:analytics:google-analytics:G-ABC123:head:end -->
              <script>gtag('config', 'G-UNMARKED');</script>
            </head><body>content</body></html>
            """;

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true);

        Assert.DoesNotContain("<script>managed</script>", filtered, StringComparison.Ordinal);
        Assert.Contains("gtag('config', 'G-UNMARKED')", filtered, StringComparison.Ordinal);
        Assert.Contains("<body>content</body>", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_RemoveFalse_ReturnsUnchanged()
    {
        var html = "<html><script>gtag('config', 'G-ABC123');</script></html>";
        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: false);
        Assert.Equal(html, filtered);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_NullHtml_ReturnsNull()
    {
        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(null!, removeManagedAnalytics: true);
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
    public void ResolveRemoveManagedAnalyticsInPreview_NoSiteYaml_ReturnsFalse()
    {
        var previewDir = Path.Combine(_tempDir, "no-config");
        Directory.CreateDirectory(previewDir);

        var result = (bool)s_resolveRemoveManagedAnalytics.Invoke(null, new object[] { previewDir })!;
        Assert.False(result);
    }

    [Fact]
    public void ResolveRemoveManagedAnalyticsInPreview_EnabledProductionOnlyProvider_ReturnsTrue()
    {
        var previewDir = Path.Combine(_tempDir, "with-config");
        Directory.CreateDirectory(previewDir);
        File.WriteAllText(Path.Combine(previewDir, "site.yaml"), """
            site:
              name: test
              title: Test
              analytics:
                enabled: true
                productionOnly: true
                providers:
                  - type: google-analytics
                    measurementId: G-ABCDE123
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            """);

        var result = (bool)s_resolveRemoveManagedAnalytics.Invoke(null, new object[] { previewDir })!;
        Assert.True(result);
    }

    [Theory]
    [InlineData("enabled: false\n    productionOnly: true\n    providers:\n      - type: google-analytics\n        measurementId: G-ABCDE123", "")]
    [InlineData("enabled: true\n    productionOnly: false\n    providers:\n      - type: google-analytics\n        measurementId: G-ABCDE123", "")]
    [InlineData("enabled: true\n    productionOnly: true\n    providers: []", "")]
    [InlineData("enabled: true\n    productionOnly: true\n    providers:\n      - type: google-analytics\n        measurementId: G-ABCDE123", "  plugins:\n    analytics: false\n")]
    public void ResolveRemoveManagedAnalyticsInPreview_WhenPolicyInactive_ReturnsFalse(string analyticsYaml, string pluginYaml)
    {
        var previewDir = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(previewDir);
        var indentedAnalytics = string.Join('\n', analyticsYaml.Split('\n').Select(line => "    " + line));
        File.WriteAllText(Path.Combine(previewDir, "site.yaml"),
            $"site:\n  name: test\n  title: Test\n  analytics:\n{indentedAnalytics}\n{pluginYaml}content:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n");

        var result = (bool)s_resolveRemoveManagedAnalytics.Invoke(null, new object[] { previewDir })!;

        Assert.False(result);
    }

    [Fact]
    public async Task RunAsync_ExplicitCustomConfig_UsesItsAnalyticsPolicy_WhenNoSiteYamlExists()
    {
        var rootDir = Path.Combine(_tempDir, "custom-only");
        var outputDir = Path.Combine(rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        var configPath = Path.Combine(rootDir, "custom.yaml");
        WriteConfig(configPath, "dist", policyActive: true);
        WriteManagedHtml(outputDir);

        var body = await RequestPreviewAsync("--config", configPath);

        AssertManagedAnalyticsRemoved(body);
    }

    [Fact]
    public async Task RunAsync_ExplicitCustomConfig_WinsOverNearestSiteYaml()
    {
        var rootDir = Path.Combine(_tempDir, "custom-wins");
        var outputDir = Path.Combine(rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        var configPath = Path.Combine(rootDir, "custom.yaml");
        WriteConfig(configPath, "dist", policyActive: false);
        WriteConfig(Path.Combine(rootDir, "site.yaml"), "dist", policyActive: true);
        WriteManagedHtml(outputDir);

        var body = await RequestPreviewAsync("--config", configPath);

        Assert.Contains("<script>managed-analytics</script>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ConfigAndDir_UsesExplicitConfigForAnalyticsPolicy()
    {
        var rootDir = Path.Combine(_tempDir, "config-and-dir");
        var outputDir = Path.Combine(rootDir, "selected-output");
        Directory.CreateDirectory(outputDir);
        var configPath = Path.Combine(rootDir, "custom.yaml");
        WriteConfig(configPath, "ignored-output", policyActive: true);
        WriteConfig(Path.Combine(rootDir, "site.yaml"), "ignored-output", policyActive: false);
        WriteManagedHtml(outputDir);

        var body = await RequestPreviewAsync("--config", configPath, "--dir", outputDir);

        AssertManagedAnalyticsRemoved(body);
    }

    [Fact]
    public async Task RunAsync_Site_UsesResolvedSiteConfig_NotRootSiteYaml()
    {
        var rootDir = Path.Combine(_tempDir, "multi-site");
        var outputDir = Path.Combine(rootDir, "dist");
        var sitesDir = Path.Combine(rootDir, "sites");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(sitesDir);
        WriteConfig(Path.Combine(sitesDir, "blog.yaml"), "dist", policyActive: true);
        WriteConfig(Path.Combine(rootDir, "site.yaml"), "dist", policyActive: false);
        WriteManagedHtml(outputDir);

        using var _ = new CurrentDirectoryScope(rootDir);
        var body = await RequestPreviewAsync("--site", "blog");

        AssertManagedAnalyticsRemoved(body);
    }

    [Fact]
    public async Task RunAsync_ExplicitConfig_UsesPolicyForExternalConfiguredOutput()
    {
        var rootDir = Path.Combine(_tempDir, "external-config");
        var outputDir = Path.Combine(_tempDir, "external-output");
        Directory.CreateDirectory(rootDir);
        Directory.CreateDirectory(outputDir);
        var configPath = Path.Combine(rootDir, "custom.yaml");
        WriteConfig(configPath, outputDir, policyActive: true);
        WriteManagedHtml(outputDir);

        var body = await RequestPreviewAsync("--config", configPath);

        AssertManagedAnalyticsRemoved(body);
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
    public async Task HandleRequest_RootIndexHtml_StripsManagedAnalyticsFromResponseWithoutWritingDisk()
    {
        var path = Path.Combine(_tempDir, "index.html");
        var original = """
            <html><head>
              <!-- bukit:analytics:google-analytics:G-ABC123:head:start -->
              <script>managed</script>
              <!-- bukit:analytics:google-analytics:G-ABC123:head:end -->
              <script>gtag('config', 'G-UNMARKED');</script>
            </head><body>root</body></html>
            """;
        File.WriteAllText(path, original);

        var response = await SendRequestAsync("/", removeManagedAnalytics: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        Assert.Contains("root", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>managed</script>", response.Body, StringComparison.Ordinal);
        Assert.Contains("gtag('config', 'G-UNMARKED')", response.Body, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public async Task HandleRequest_WhenAnalyticsRemovalDisabled_PreservesUtf8BomBytesAndContentLength()
    {
        var path = Path.Combine(_tempDir, "index.html");
        var payload = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes("<html><body>bom</body></html>"))
            .ToArray();
        File.WriteAllBytes(path, payload);

        var response = await SendRawRequestAsync("/", removeManagedAnalytics: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payload, response.Body);
        Assert.Equal(payload.Length, response.ContentLength);
    }

    [Fact]
    public async Task HandleRequest_WhenRemovalEnabledButNoManagedMarker_PreservesNonUtf8Bytes()
    {
        var path = Path.Combine(_tempDir, "index.html");
        var payload = "<html><body>caf"u8.ToArray()
            .Concat(new byte[] { 0xE9 })
            .Concat("</body></html>"u8.ToArray())
            .ToArray();
        File.WriteAllBytes(path, payload);

        var response = await SendRawRequestAsync("/", removeManagedAnalytics: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payload, response.Body);
        Assert.Equal(payload.Length, response.ContentLength);
    }

    [Theory]
    [InlineData("<script>const marker = '<!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end -->';</script>")]
    [InlineData("<style>/* <!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end --> */</style>")]
    [InlineData("<title><!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end --></title>")]
    [InlineData("<textarea><!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end --></textarea>")]
    [InlineData("<div data-marker=\"<!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end -->\"></div>")]
    [InlineData("<!-- bukit:analytics:google-analytics:G-ABC123:head:start -->")]
    public async Task HandleRequest_WhenMarkerLikeBytesWouldNotBeRemoved_PreservesNonUtf8Bytes(string markerLikeHtml)
    {
        var path = Path.Combine(_tempDir, "index.html");
        var payload = "<html><body>caf"u8.ToArray()
            .Concat(new byte[] { 0xE9 })
            .Concat(System.Text.Encoding.ASCII.GetBytes(markerLikeHtml))
            .Concat("</body></html>"u8.ToArray())
            .ToArray();
        File.WriteAllBytes(path, payload);

        var response = await SendRawRequestAsync("/", removeManagedAnalytics: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payload, response.Body);
        Assert.Equal(payload.Length, response.ContentLength);
    }

    [Fact]
    public async Task HandleRequest_WhenManagedAnalyticsIsRemoved_PreservesUtf8Bom()
    {
        var path = Path.Combine(_tempDir, "index.html");
        var html = "<html><head><!-- bukit:analytics:google-analytics:G-ABC123:head:start --><script>managed</script><!-- bukit:analytics:google-analytics:G-ABC123:head:end --></head><body>ok</body></html>";
        var payload = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes(html))
            .ToArray();
        File.WriteAllBytes(path, payload);

        var response = await SendRawRequestAsync("/", removeManagedAnalytics: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, response.Body[..3]);
        Assert.DoesNotContain("managed", System.Text.Encoding.UTF8.GetString(response.Body));
        Assert.Equal(response.Body.Length, response.ContentLength);
    }

    [Fact]
    public async Task HandleRequest_WhenManagedMarkerRequiresRewrite_RejectsInvalidUtf8()
    {
        var path = Path.Combine(_tempDir, "index.html");
        var payload = "<html><head><!-- bukit:analytics:google-analytics:G-ABC123:head:start --><script>caf"u8.ToArray()
            .Concat(new byte[] { 0xE9 })
            .Concat("</script><!-- bukit:analytics:google-analytics:G-ABC123:head:end --></head></html>"u8.ToArray())
            .ToArray();
        File.WriteAllBytes(path, payload);

        var response = await SendRawRequestAsync("/", removeManagedAnalytics: true);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(response.Body);
    }

    [Theory]
    [InlineData("utf-16le")]
    [InlineData("utf-16be")]
    [InlineData("utf-32le")]
    [InlineData("utf-32be")]
    public async Task HandleRequest_WhenManagedMarkerUsesBomEncodedNonUtf8_RejectsRewrite(string encodingName)
    {
        var path = Path.Combine(_tempDir, "index.html");
        var html = "<html><head><!-- bukit:analytics:google-analytics:G-ABC123:head:start --><script>managed</script><!-- bukit:analytics:google-analytics:G-ABC123:head:end --></head></html>";
        var encoding = CreateBomEncoding(encodingName);
        var payload = encoding.GetPreamble().Concat(encoding.GetBytes(html)).ToArray();
        File.WriteAllBytes(path, payload);

        var response = await SendRawRequestAsync("/", removeManagedAnalytics: true);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(response.Body);
    }

    [Fact]
    public async Task HandleRequest_DirectoryWithoutExtension_FallsBackToNestedIndex()
    {
        var postsDir = Path.Combine(_tempDir, "posts");
        Directory.CreateDirectory(postsDir);
        File.WriteAllText(Path.Combine(postsDir, "index.html"), "<html><body>nested</body></html>");

        var response = await SendRequestAsync("/posts", removeManagedAnalytics: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("nested", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleRequest_SearchActionTarget_MapsToGeneratedSearchRoute()
    {
        var searchDir = Path.Combine(_tempDir, "search");
        Directory.CreateDirectory(searchDir);
        File.WriteAllText(Path.Combine(searchDir, "index.html"), "<html><body>search experience</body></html>");

        var response = await SendRequestAsync("/search/?q=test", removeManagedAnalytics: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("search experience", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleRequest_StaticAsset_ReturnsFileBytes()
    {
        var assetsDir = Path.Combine(_tempDir, "assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "site.css"), "body{color:red;}");

        var response = await SendRequestAsync("/assets/site.css", removeManagedAnalytics: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css; charset=utf-8", response.ContentType);
        Assert.Equal("body{color:red;}", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingFile_ReturnsNotFound()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), "<html></html>");

        var response = await SendRequestAsync("/missing", removeManagedAnalytics: false);

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
        var response = await SendRequestAsync("/%252e%252e/", removeManagedAnalytics: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(_tempDir, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_RejectsBackslashTraversal()
    {
        var response = await SendRequestAsync("/%5c..%5csecret", removeManagedAnalytics: false);

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
        var response = await SendRequestAsync("/%EF%BC%8E%EF%BC%8E/secret", removeManagedAnalytics: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(_tempDir, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_RejectsVeryLongPathWithoutCrash()
    {
        var response = await SendRequestAsync("/" + new string('a', 1024), removeManagedAnalytics: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(_tempDir, response.Body, StringComparison.Ordinal);
    }

    private async Task<(HttpStatusCode StatusCode, string Body, string? ContentType)> SendRequestAsync(string path, bool removeManagedAnalytics)
    {
        var response = await SendRawRequestAsync(path, removeManagedAnalytics);
        return (response.StatusCode, System.Text.Encoding.UTF8.GetString(response.Body), response.ContentType);
    }

    private async Task<(HttpStatusCode StatusCode, byte[] Body, string? ContentType, long? ContentLength)> SendRawRequestAsync(
        string path,
        bool removeManagedAnalytics)
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
                s_handleRequest.Invoke(null, new object[] { _tempDir, context, removeManagedAnalytics });

                using var responseAfterContext = await responseTask;
                var bodyAfterContext = await responseAfterContext.Content.ReadAsByteArrayAsync();
                return (
                    responseAfterContext.StatusCode,
                    bodyAfterContext,
                    responseAfterContext.Content.Headers.ContentType?.ToString(),
                    responseAfterContext.Content.Headers.ContentLength);
            }

            using var responseAfterTimeout = await responseTask;
            var contextAfterTimeout = await contextTask.WaitAsync(s_requestTimeout);
            s_handleRequest.Invoke(null, new object[] { _tempDir, contextAfterTimeout, removeManagedAnalytics });

            var bodyAfterTimeout = await responseAfterTimeout.Content.ReadAsByteArrayAsync();
            return (
                responseAfterTimeout.StatusCode,
                bodyAfterTimeout,
                responseAfterTimeout.Content.Headers.ContentType?.ToString(),
                responseAfterTimeout.Content.Headers.ContentLength);
        }
        finally
        {
            listener.Close();
        }
    }

    private static void WriteConfig(string path, string output, bool policyActive)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            site:
              name: preview-identity
              title: Preview Identity
              analytics:
                enabled: {{policyActive.ToString().ToLowerInvariant()}}
                productionOnly: true
                providers:
                  - type: google-analytics
                    measurementId: G-ABCDE123
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            build:
              output: {{output}}
            """);
    }

    private static void WriteManagedHtml(string outputDir)
    {
        File.WriteAllText(Path.Combine(outputDir, "index.html"), """
            <html><head>
              <!-- bukit:analytics:google-analytics:G-ABCDE123:head:start -->
              <script>managed-analytics</script>
              <!-- bukit:analytics:google-analytics:G-ABCDE123:head:end -->
            </head><body>preview</body></html>
            """);
    }

    private static void AssertManagedAnalyticsRemoved(string body)
    {
        Assert.Contains("<body>preview</body>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("managed-analytics", body, StringComparison.Ordinal);
        Assert.DoesNotContain("bukit:analytics", body, StringComparison.Ordinal);
    }

    private static async Task<string> RequestPreviewAsync(params string[] keyValues)
    {
        var port = PickFreePort();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < keyValues.Length; index += 2)
        {
            options[keyValues[index]] = keyValues[index + 1];
        }

        options["--host"] = IPAddress.Loopback.ToString();
        options["--port"] = port.ToString();
        options["--strict-port"] = "true";
        var bound = new CliBoundCommand(options, Array.Empty<string>());

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var previewTask = PreviewCommand.RunAsync(bound, cancellation.Token);
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var uri = new Uri($"http://{IPAddress.Loopback}:{port}/");
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (previewTask.IsCompleted)
                {
                    await previewTask;
                    throw new InvalidOperationException("Preview stopped before serving a request.");
                }

                try
                {
                    using var response = await client.GetAsync(uri, cancellation.Token);
                    return await response.Content.ReadAsStringAsync(cancellation.Token);
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(25, cancellation.Token);
                }
            }

            throw new TimeoutException("Preview did not start before the request deadline.");
        }
        finally
        {
            cancellation.Cancel();
            Assert.Equal(0, await previewTask.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    private static int PickFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static System.Text.Encoding CreateBomEncoding(string name)
        => name switch
        {
            "utf-16le" => new System.Text.UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true),
            "utf-16be" => new System.Text.UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true),
            "utf-32le" => new System.Text.UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true),
            "utf-32be" => new System.Text.UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unsupported test encoding.")
        };
}
