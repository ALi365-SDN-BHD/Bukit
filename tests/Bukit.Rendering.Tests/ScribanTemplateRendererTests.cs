using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class ScribanTemplateRendererTests : IDisposable
{
    private readonly string _layoutsDir;

    public ScribanTemplateRendererTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-scriban-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_layoutsDir, recursive: true); } catch { }
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

    [Fact]
    public void Constructor_CreatesRenderer()
    {
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RenderPage_Throws_WhenTemplateNotFound()
    {
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo
            {
                Title = "Test",
                Content = "<p>hi</p>",
                Url = "/test/"
            }
        };

        Assert.Throws<Bukit.Shared.RenderException>(() =>
            renderer.RenderPage("missing.html", model));
    }

    [Fact]
    public void RenderPage_RendersSimpleTemplate()
    {
        var templatePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(templatePath, "<html><head><title>{{ page.title }}</title></head><body>{{ page.content }}</body></html>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo
            {
                Title = "Hello",
                Content = "<p>World</p>",
                Url = "/hello/"
            }
        };

        var result = renderer.RenderPage("page.html", model);
        Assert.Contains("<title>Hello</title>", result);
        Assert.Contains("<p>World</p>", result);
    }

    [Fact]
    public void RenderList_RendersListTemplate()
    {
        var templatePath = Path.Combine(_layoutsDir, "list.html");
        File.WriteAllText(templatePath, "{{ for page in pages }}{{ page.title }}|{{ end }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var model = new ListPageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "List", Url = "/list/", Content = "" },
            Pages = new[]
            {
                new PageInfo { Title = "A", Url = "/a/", Content = "" },
                new PageInfo { Title = "B", Url = "/b/", Content = "" }
            }
        };

        var result = renderer.RenderList("list.html", model);
        Assert.Contains("A|", result);
        Assert.Contains("B|", result);
    }
}
