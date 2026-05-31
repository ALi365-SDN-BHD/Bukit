using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class HtmlDemoScannerTests : IDisposable
{
    private readonly string _tempDir;

    public HtmlDemoScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-scan-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Scan_SingleHtmlFile_ReturnsOnePage()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var pages = HtmlDemoScanner.Scan(_tempDir);

        Assert.Single(pages);
        Assert.Equal("Test", pages[0].Title);
        Assert.Equal(PageType.Home, pages[0].Type);
        Assert.Equal("", pages[0].Slug);
    }

    [Fact]
    public void Scan_MultipleHtmlFiles_ReturnsAllPages()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        var pages = HtmlDemoScanner.Scan(_tempDir);

        Assert.Equal(2, pages.Count);
        Assert.Contains(pages, p => p.Type == PageType.Home);
        Assert.Contains(pages, p => p.Type == PageType.Page);
    }

    [Fact]
    public void Scan_NestedDirectories_IncludesAllFiles()
    {
        var subDir = Path.Combine(_tempDir, "blog");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main>Home</main></body></html>");
        File.WriteAllText(Path.Combine(subDir, "post1.html"),
            "<html><head><title>Post</title></head><body><article>Post</article></body></html>");

        var pages = HtmlDemoScanner.Scan(_tempDir);

        Assert.Equal(2, pages.Count);
    }

    [Fact]
    public void Scan_NoHtmlFiles_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => HtmlDemoScanner.Scan(_tempDir));
        Assert.Contains(".html", ex.Message);
    }

    [Fact]
    public void Scan_ParsesBodySplit_Correctly()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"),
            "<html><head><title>Page</title></head><body><header>Nav</header><main><h1>Content</h1></main><footer>End</footer></body></html>");

        var pages = HtmlDemoScanner.Scan(_tempDir);

        Assert.Single(pages);
        Assert.Contains("Nav", pages[0].BodyOpening);
        Assert.Contains("Content", pages[0].UniqueBody);
        Assert.Contains("End", pages[0].BodyClosing);
    }

    [Fact]
    public void Scan_ExtractsAssetPaths()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"),
            "<html><head><title>Page</title></head><body><img src=\"/images/hero.jpg\" /><link href=\"/css/style.css\" rel=\"stylesheet\" /></body></html>");

        var pages = HtmlDemoScanner.Scan(_tempDir);

        Assert.Single(pages);
        Assert.Contains(pages[0].AssetPaths, p => p.EndsWith("hero.jpg"));
        Assert.Contains(pages[0].AssetPaths, p => p.EndsWith("style.css"));
    }

    [Fact]
    public void Scan_SkipsExternalUrls()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"),
            "<html><head><title>Page</title></head><body><img src=\"https://cdn.example.com/img.jpg\" /><img src=\"/local.jpg\" /></body></html>");

        var pages = HtmlDemoScanner.Scan(_tempDir);

        Assert.Single(pages);
        Assert.DoesNotContain(pages[0].AssetPaths, p => p.StartsWith("https://"));
        Assert.Contains(pages[0].AssetPaths, p => p == "/local.jpg");
    }
}
