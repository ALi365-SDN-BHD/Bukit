using System.Net;
using System.Reflection;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class PreviewCommandExtendedTests : IDisposable
{
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
    [InlineData("image.png", "image/png")]
    [InlineData("icon.svg", "image/svg+xml")]
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
                                                                    provider: markdown
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
}
