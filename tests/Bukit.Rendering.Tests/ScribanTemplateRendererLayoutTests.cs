using Xunit;
using Bukit.Shared;

namespace Bukit.Rendering.Tests;

public sealed class ScribanTemplateRendererLayoutTests : IDisposable
{
    private readonly string _layoutsDir;

    public ScribanTemplateRendererLayoutTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-scriban-layout-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_layoutsDir, recursive: true);
    }

    private static SiteModel CreateSite()
    {
        return new SiteModel
        {
            Name = "test",
            Title = "Test Site",
            Url = "https://example.com",
            BaseUrl = "/",
            Language = "en"
        };
    }

    private PageModel CreatePageModel(string title = "Test", string content = "<p>Hello</p>", string url = "/test/")
    {
        return new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = title, Content = content, Url = url }
        };
    }

    [Fact]
    public void LayoutPercentDelimiter_WrapsContentThroughLayout()
    {
        var layoutPath = Path.Combine(_layoutsDir, "_default.html");
        File.WriteAllText(layoutPath, "<html><body>{{ content }}</body></html>");

        var pagePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(pagePath, "{% layout \"_default.html\" %}\n<h1>{{ page.title }}</h1>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", CreatePageModel("Hello"));

        Assert.Contains("<html><body>", result);
        Assert.Contains("<h1>Hello</h1>", result);
        Assert.Contains("</body></html>", result);
    }

    [Fact]
    public void LayoutDoubleBraceDelimiter_WrapsContentThroughLayout()
    {
        var layoutPath = Path.Combine(_layoutsDir, "_default.html");
        File.WriteAllText(layoutPath, "<html><body>{{ content }}</body></html>");

        var pagePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(pagePath, "{{ layout \"_default.html\" }}\n<h1>{{ page.title }}</h1>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", CreatePageModel("World"));

        Assert.Contains("<html><body>", result);
        Assert.Contains("<h1>World</h1>", result);
        Assert.Contains("</body></html>", result);
    }

    [Fact]
    public void MultiLevelLayout_RendersNestedChain()
    {
        var basePath = Path.Combine(_layoutsDir, "base.html");
        File.WriteAllText(basePath, "<html><body><main>{{ content }}</main></body></html>");

        var layoutPath = Path.Combine(_layoutsDir, "_layout.html");
        File.WriteAllText(layoutPath, "{% layout \"base.html\" %}\n<article>{{ content }}</article>");

        var pagePath = Path.Combine(_layoutsDir, "post.html");
        File.WriteAllText(pagePath, "{% layout \"_layout.html\" %}\n<h1>{{ page.title }}</h1><p>{{ page.content }}</p>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("post.html", CreatePageModel("Nested", "<span>body</span>"));

        Assert.Contains("<html><body><main>", result);
        Assert.Contains("<article>", result);
        Assert.Contains("<h1>Nested</h1>", result);
        Assert.Contains("<span>body</span>", result);
        Assert.Contains("</article>", result);
        Assert.Contains("</main></body></html>", result);
    }

    [Fact]
    public void LayoutContentVariable_ReceivesInnerTemplateOutput()
    {
        var layoutPath = Path.Combine(_layoutsDir, "_default.html");
        File.WriteAllText(layoutPath, "BEFORE|{{ content }}|AFTER");

        var pagePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(pagePath, "{% layout \"_default.html\" %}\nINNER-{{ page.title }}-INNER");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", CreatePageModel("C"));

        Assert.Equal("BEFORE|INNER-C-INNER|AFTER", result);
    }

    [Fact]
    public void CacheInvalidation_DetectsModifiedTemplateFile()
    {
        var templatePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(templatePath, "<p>version 1</p>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result1 = renderer.RenderPage("page.html", CreatePageModel());
        Assert.Contains("version 1", result1);

        Thread.Sleep(10);
        File.WriteAllText(templatePath, "<p>version 2</p>");

        var result2 = renderer.RenderPage("page.html", CreatePageModel());
        Assert.Contains("version 2", result2);
        Assert.DoesNotContain("version 1", result2);
    }

    [Fact]
    public void ParseError_BrokenScribanSyntax_ThrowsRenderException()
    {
        var templatePath = Path.Combine(_layoutsDir, "broken.html");
        File.WriteAllText(templatePath, "{{ \"");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var ex = Assert.Throws<RenderException>(() =>
            renderer.RenderPage("broken.html", CreatePageModel()));
        Assert.Contains("broken.html", ex.Message);
    }

    [Fact]
    public void MaxLayoutDepth_ThrowsRenderExceptionWithDepthMessage()
    {
        var layoutPath = Path.Combine(_layoutsDir, "layout.html");
        File.WriteAllText(layoutPath, "{% layout \"layout.html\" %}\n{{ content }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var ex = Assert.Throws<RenderException>(() =>
            renderer.RenderPage("layout.html", CreatePageModel()));
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10", ex.Message);
    }

    [Fact]
    public void LayoutDirective_Throws_WhenPathEscapesLayoutsDirectory()
    {
        var outsidePath = Path.Combine(Path.GetDirectoryName(_layoutsDir)!, "outside-layout.html");
        File.WriteAllText(outsidePath, "outside");
        try
        {
            var pagePath = Path.Combine(_layoutsDir, "page.html");
            File.WriteAllText(pagePath, "{% layout \"../outside-layout.html\" %}\n<h1>{{ page.title }}</h1>");

            var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
            var ex = Assert.Throws<RenderException>(() =>
                renderer.RenderPage("page.html", CreatePageModel("Blocked")));

            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestCleanup.DeleteFile(outsidePath);
        }
    }

    [Fact]
    public void LayoutWithoutQuotedStringParam_RendersAsNormalTemplate()
    {
        var templatePath = Path.Combine(_layoutsDir, "nolayout.html");
        File.WriteAllText(templatePath, "{% layout %}\n<h1>{{ page.title }}</h1>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("nolayout.html", CreatePageModel("Direct"));

        Assert.Contains("<h1>Direct</h1>", result);
    }

    [Fact]
    public void LayoutAfterBlankLines_StillExtractsAndWraps()
    {
        var layoutPath = Path.Combine(_layoutsDir, "_default.html");
        File.WriteAllText(layoutPath, "<html>{{ content }}</html>");

        var pagePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(pagePath, "\n\n\n{% layout \"_default.html\" %}\n<h1>{{ page.title }}</h1>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", CreatePageModel("Spaced"));

        Assert.Contains("<html>", result);
        Assert.Contains("<h1>Spaced</h1>", result);
        Assert.Contains("</html>", result);
    }

    [Fact]
    public void IncludeDirective_RendersIncludedPartialTemplate()
    {
        var partialPath = Path.Combine(_layoutsDir, "_header.html");
        File.WriteAllText(partialPath, "<header>{{ site.title }}</header>");

        var pagePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(pagePath, "{{ include \"_header.html\" }}\n<main>{{ page.content }}</main>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", CreatePageModel(content: "<p>body</p>"));

        Assert.Contains("<header>Test Site</header>", result);
        Assert.Contains("<main><p>body</p></main>", result);
    }

    [Fact]
    public void EmptyTemplateFile_RendersEmptyString()
    {
        var templatePath = Path.Combine(_layoutsDir, "empty.html");
        File.WriteAllText(templatePath, "");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("empty.html", CreatePageModel());

        Assert.Equal("", result);
    }

    [Fact]
    public void LayoutSingleQuoteDelimiter_WrapsContentThroughLayout()
    {
        var layoutPath = Path.Combine(_layoutsDir, "_default.html");
        File.WriteAllText(layoutPath, "<html><body>{{ content }}</body></html>");

        var pagePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(pagePath, "{% layout '_default.html' %}\n<p>Single Quote</p>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", CreatePageModel(content: "<p>Single Quote</p>"));

        Assert.Contains("<html><body>", result);
        Assert.Contains("<p>Single Quote</p>", result);
        Assert.Contains("</body></html>", result);
    }
}
