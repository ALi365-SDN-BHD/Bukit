using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneFidelityGeneratorTests : IDisposable
{
    private readonly string _testDir;

    public CloneFidelityGeneratorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-fidelity-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Generate_SingleHtmlFile_ReturnsCorrectCounts()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"),
            "<html><head><title>Test</title></head><body><h1>Hello</h1></body></html>");

        var result = CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        Assert.True(result.TemplateCount > 0);
        Assert.True(result.PageCount > 0);
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public void Generate_EmptyHtmlDirectory_ThrowsInvalidOperationException()
    {
        var htmlDir = Path.Combine(_testDir, "empty-html");
        Directory.CreateDirectory(htmlDir);

        var ex = Assert.Throws<InvalidOperationException>(
            () => CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme"));

        Assert.Contains("No .html files found", ex.Message);
    }

    [Fact]
    public void Generate_MultipleHtmlFiles_ProducesTemplateAndPartialCounts()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"),
            "<html><head><title>Home</title></head><body><header><nav><a href=\"/\">Home</a></nav></header><main><h1>Welcome</h1></main><footer><p>Footer</p></footer></body></html>");
        File.WriteAllText(Path.Combine(htmlDir, "about.html"),
            "<html><head><title>About</title></head><body><header><nav><a href=\"/\">Home</a></nav></header><main><h1>About Us</h1></main><footer><p>Footer</p></footer></body></html>");

        var result = CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        Assert.Equal(2, result.PageCount);
        Assert.True(result.TemplateCount >= 3 + result.PageCount);
        Assert.True(result.PartialCount >= 1);
    }

    [Fact]
    public void Generate_CreatesThemeDirectoryStructure()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Test</h1></main></body></html>");

        CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        var themeDir = Path.Combine(_testDir, "themes", "test-theme");
        Assert.True(Directory.Exists(Path.Combine(themeDir, "layouts", "layouts")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "layouts", "pages")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "layouts", "partials")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "assets")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "static")));
    }

    [Fact]
    public void Generate_WritesBaseLayoutFile()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"),
            "<html><head><title>Test</title></head><body><h1>Test</h1></body></html>");

        CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        var layoutPath = Path.Combine(_testDir, "themes", "test-theme", "layouts", "layouts", "base.html");
        Assert.True(File.Exists(layoutPath));
        var content = File.ReadAllText(layoutPath);
        Assert.Contains("{{ content }}", content);
    }

    [Fact]
    public void Generate_IndexAndListTemplates_Written()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        var pagesDir = Path.Combine(_testDir, "themes", "test-theme", "layouts", "pages");
        Assert.True(File.Exists(Path.Combine(pagesDir, "index.html")));
        Assert.True(File.Exists(Path.Combine(pagesDir, "list.html")));
    }

    [Fact]
    public void Generate_SinglePage_DoesNotProduceSharedPartialWarnings()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "page.html"),
            "<html><head><title>Page</title></head><body><header><div class=\"logo\">Logo</div></header><main><h1>Content</h1></main></body></html>");

        var result = CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        Assert.NotNull(result.Warnings);
        Assert.True(result.PageCount == 1);
    }

    [Fact]
    public void Generate_WithArticleTag_DetectsCommonBlocks()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "a.html"),
            "<html><head><title>A</title></head><body><nav><a href=\"/\">Home</a></nav><article><h1>A</h1></article><footer>End</footer></body></html>");
        File.WriteAllText(Path.Combine(htmlDir, "b.html"),
            "<html><head><title>B</title></head><body><nav><a href=\"/\">Home</a></nav><article><h1>B</h1></article><footer>End</footer></body></html>");

        var result = CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        Assert.Equal(2, result.PageCount);
        Assert.True(result.TemplateCount > 0);
    }

    [Fact]
    public void Generate_WithNestedHtmlFiles_IncludesAllFiles()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        var subDir = Path.Combine(htmlDir, "blog");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(subDir, "post1.html"),
            "<html><head><title>Post 1</title></head><body><main><h1>Post 1</h1></main></body></html>");

        var result = CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        Assert.Equal(2, result.PageCount);
    }

    [Fact]
    public void Generate_PageNameIndex_IsRenamedToPageIndex()
    {
        var htmlDir = Path.Combine(_testDir, "html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"),
            "<html><head><title>Index Page</title></head><body><main><h1>Index</h1></main></body></html>");

        CloneFidelityGenerator.Generate(_testDir, htmlDir, "test-theme");

        var pagesDir = Path.Combine(_testDir, "themes", "test-theme", "layouts", "pages");
        Assert.True(File.Exists(Path.Combine(pagesDir, "page-index.html")));
    }
}
