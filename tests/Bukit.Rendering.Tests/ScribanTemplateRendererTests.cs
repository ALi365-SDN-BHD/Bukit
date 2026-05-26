using Xunit;
using Bukit.Config;
using Bukit.Shared;
using Bukit.Theme;

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

    [Fact]
    public async Task RenderPage_WithComponents_UsesRendererLocalStateUnderParallelRendering()
    {
        var leftLayouts = Path.Combine(_layoutsDir, "left");
        var rightLayouts = Path.Combine(_layoutsDir, "right");
        Directory.CreateDirectory(leftLayouts);
        Directory.CreateDirectory(rightLayouts);

        File.WriteAllText(Path.Combine(leftLayouts, "page.html"), """
            {{ for i in 1..20 }}{{ comp.render 'badge' '' '' '' }};{{ end }}
            """);
        File.WriteAllText(Path.Combine(leftLayouts, "badge.html"), "left:{{ page.title }}");

        File.WriteAllText(Path.Combine(rightLayouts, "page.html"), """
            {{ for i in 1..20 }}{{ comp.render 'badge' '' '' '' }};{{ end }}
            """);
        File.WriteAllText(Path.Combine(rightLayouts, "badge.html"), "right:{{ page.title }}");

        var components = new Dictionary<string, ComponentDefinition>
        {
            ["badge"] = new() { Template = "badge.html" }
        };

        var leftRenderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(leftLayouts, null, null, components, null);
        var rightRenderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(rightLayouts, null, null, components, null);
        var site = CreateSite();

        var tasks = Enumerable.Range(0, 250).Select(i => Task.Run(() =>
        {
            var left = leftRenderer.RenderPage("page.html", new PageModel
            {
                Site = site,
                Page = new PageInfo { Title = $"L{i}", Content = "", Url = $"/l{i}/" }
            });
            var right = rightRenderer.RenderPage("page.html", new PageModel
            {
                Site = site,
                Page = new PageInfo { Title = $"R{i}", Content = "", Url = $"/r{i}/" }
            });
            return (i, left, right);
        }));

        var results = await Task.WhenAll(tasks);

        foreach (var (i, left, right) in results)
        {
            Assert.DoesNotContain("right:", left);
            Assert.DoesNotContain("R" + i, left);
            Assert.DoesNotContain("left:", right);
            Assert.DoesNotContain("L" + i, right);
            Assert.Equal(20, CountOccurrences(left, $"left:L{i}"));
            Assert.Equal(20, CountOccurrences(right, $"right:R{i}"));
        }
    }

    [Fact]
    public void RenderPage_ImageHelper_RejectsUnsafeImageSource()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"),
            "{{ image.img 'javascript:alert(1)' 'bad' '480' 'hero' }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Image", Content = "", Url = "/image/" }
        });

        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderPage_ImageHelper_EscapesAttributeValues()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"),
            "{{ image.img '/img/photo.jpg' 'A \"quoted\" alt' '480' 'hero \"bad\"' }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Image", Content = "", Url = "/image/" }
        });

        Assert.Contains("src=\"/img/photo.jpg\"", result);
        Assert.Contains("alt=\"A &quot;quoted&quot; alt\"", result);
        Assert.Contains("class=\"hero &quot;bad&quot;\"", result);
        Assert.DoesNotContain("alt=\"A \"quoted\" alt\"", result);
    }

    [Fact]
    public void RenderPage_ThemeComponentMissing_StrictThrows()
    {
        var themeDir = Path.Combine(_layoutsDir, "theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'missing' {} }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Components = new()
            {
                ["existing"] = new() { Template = "existing.html" }
            }
        };
        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "strict");

        var ex = Assert.Throws<RenderException>(() => renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Component", Content = "", Url = "/component/" }
        }));

        Assert.Contains("theme.component.not_found", ex.Message);
        Assert.Contains("missing", ex.Message);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
