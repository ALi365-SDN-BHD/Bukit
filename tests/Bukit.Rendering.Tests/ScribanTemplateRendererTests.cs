using Bukit.Content;
using Bukit.Rendering.Scriban;
using Bukit.Shared;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class ScribanTemplateRendererTests : IDisposable
{
    private readonly string _layoutsDir;

    public ScribanTemplateRendererTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-render-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_layoutsDir))
        {
            Directory.Delete(_layoutsDir, recursive: true);
        }
    }

    private static SiteModel CreateSiteModel() => new()
    {
        Name = "test",
        Title = "Test Site",
        BaseUrl = "/",
        Language = "en"
    };

    private static PageModel CreatePageModel(string title = "Page", string content = "<p>Hello</p>") => new()
    {
        Site = CreateSiteModel(),
        Page = new PageInfo
        {
            Title = title,
            Url = "/test/",
            Content = content
        }
    };

    [Fact]
    public void RenderPage_SimpleTemplate_InterpolatesValues()
    {
        WriteTemplate("simple.html", "<h1>{{ page.title }}</h1>");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("simple.html", CreatePageModel("My Title"));

        Assert.Equal("<h1>My Title</h1>", result);
    }

    [Fact]
    public void RenderPage_TemplateWithSiteFields_RendersSiteInfo()
    {
        WriteTemplate("site.html", "{{ site.name }} - {{ site.title }}");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("site.html", CreatePageModel());

        Assert.Equal("test - Test Site", result);
    }

    [Fact]
    public void RenderPage_WithLayoutDirective_RendersIntoLayout()
    {
        WriteTemplate("base.html", "<html>{{ content }}</html>");
        WriteTemplate("child.html", "{% layout \"base.html\" %}\n<p>body</p>");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("child.html", CreatePageModel());

        Assert.Equal("<html><p>body</p></html>", result);
    }

    [Fact]
    public void RenderPage_MissingTemplate_ThrowsRenderException()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        Assert.Throws<RenderException>(() =>
            renderer.RenderPage("nonexistent.html", CreatePageModel()));
    }

    [Fact]
    public void RenderPage_InvalidTemplateSyntax_ThrowsRenderException()
    {
        WriteTemplate("invalid.html", "{{ if }}");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        Assert.Throws<RenderException>(() =>
            renderer.RenderPage("invalid.html", CreatePageModel()));
    }

    [Fact]
    public void RenderPage_PageContent_Accessible()
    {
        WriteTemplate("content.html", "{{ page.content }}");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("content.html", CreatePageModel(content: "<p>test content</p>"));

        Assert.Equal("<p>test content</p>", result);
    }

    [Fact]
    public void RenderPage_SubdirectoryTemplate_ResolvesCorrectly()
    {
        var subDir = Path.Combine(_layoutsDir, "pages");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "article.html"), "Article: {{ page.title }}");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("pages/article.html", CreatePageModel("News"));

        Assert.Equal("Article: News", result);
    }

    [Fact]
    public void RenderList_SimplePagesTemplate_RendersList()
    {
        WriteTemplate("list.html", "{{ for p in pages }}<li>{{ p.title }}</li>{{ end }}");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var model = new ListPageModel
        {
            Site = CreateSiteModel(),
            Pages = new[]
            {
                new PageInfo { Title = "A", Url = "/a/", Content = "" },
                new PageInfo { Title = "B", Url = "/b/", Content = "" }
            }
        };

        var result = renderer.RenderList("list.html", model);
        Assert.Equal("<li>A</li><li>B</li>", result);
    }

    [Fact]
    public void RenderPage_TemplateIsCached_SecondCallUsesCache()
    {
        WriteTemplate("cached.html", "{{ page.title }}");

        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var r1 = renderer.RenderPage("cached.html", CreatePageModel("V1"));
        var r2 = renderer.RenderPage("cached.html", CreatePageModel("V2"));

        Assert.Equal("V1", r1);
        Assert.Equal("V2", r2);
    }

    private void WriteTemplate(string relativePath, string content)
    {
        var fullPath = Path.Combine(_layoutsDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(fullPath, content);
    }
}
