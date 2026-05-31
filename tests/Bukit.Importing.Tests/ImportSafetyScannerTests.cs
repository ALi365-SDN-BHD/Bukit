using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportSafetyScannerTests : IDisposable
{
    private readonly string _tempDir;

    public ImportSafetyScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-safety-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Scan_EnvFile_Detected()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "SECRET=xxx");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            new()
            {
                FilePath = Path.Combine(_tempDir, "index.html"),
                RelativePath = "index.html",
                Slug = "",
                Type = PageType.Home,
                FullHtml = "<html></html>"
            }
        };

        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        Assert.Contains(diagnostics, d => d.Code == "SENSITIVE_FILE" && d.Severity == ImportDiagnosticSeverity.Error);
    }

    [Fact]
    public void Scan_InlineScript_Warning()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            new()
            {
                FilePath = Path.Combine(_tempDir, "index.html"),
                RelativePath = "index.html",
                Slug = "",
                Type = PageType.Home,
                FullHtml = "<html><body><script>alert('xss')</script></body></html>"
            }
        };

        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        Assert.Contains(diagnostics, d => d.Code == "INLINE_SCRIPT");
    }

    [Fact]
    public void Scan_Iframe_Warning()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            new()
            {
                FilePath = Path.Combine(_tempDir, "index.html"),
                RelativePath = "index.html",
                Slug = "",
                Type = PageType.Home,
                FullHtml = "<html><body><iframe src=\"https://example.com\"></iframe></body></html>"
            }
        };

        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        Assert.Contains(diagnostics, d => d.Code == "IFRAME_DETECTED");
    }

    [Fact]
    public void Scan_JavascriptProtocol_Warning()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            new()
            {
                FilePath = Path.Combine(_tempDir, "index.html"),
                RelativePath = "index.html",
                Slug = "",
                Type = PageType.Home,
                FullHtml = "<a href=\"javascript:void(0)\">click</a>"
            }
        };

        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        Assert.Contains(diagnostics, d => d.Code == "DANGEROUS_PROTOCOL");
    }

    [Fact]
    public void Scan_InlineEventHandler_Warning()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            new()
            {
                FilePath = Path.Combine(_tempDir, "index.html"),
                RelativePath = "index.html",
                Slug = "",
                Type = PageType.Home,
                FullHtml = "<button onclick=\"doSomething()\">click</button>"
            }
        };

        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        Assert.Contains(diagnostics, d => d.Code == "INLINE_EVENT_HANDLER");
    }

    [Fact]
    public void Scan_MissingTitle_Warning()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            new()
            {
                FilePath = Path.Combine(_tempDir, "index.html"),
                RelativePath = "index.html",
                Slug = "",
                Type = PageType.Home,
                FullHtml = "<html><body>No title</body></html>"
            }
        };

        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        Assert.Contains(diagnostics, d => d.Code == "MISSING_TITLE");
    }
}
