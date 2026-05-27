using Xunit;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;using Bukit.Shared;
using Bukit.Theme;
using Bukit.Rendering.Scriban;

namespace Bukit.Rendering.Tests;

public sealed class RenderSectionTests : IDisposable
{
    private readonly string _themeDir;
    private readonly string _layoutsDir;

    public RenderSectionTests()
    {
        _themeDir = Path.Combine(Path.GetTempPath(), "bukit-section-tests-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_themeDir, "layouts");
        Directory.CreateDirectory(_layoutsDir);
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "sections", "hero"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "sections", "cta"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "sections", "cardGrid"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_themeDir, recursive: true); } catch { }
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
    public void RenderSection_JsonString_RendersMultipleSections()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            """
            <section class="hero">
              <h2>{{ section.props.title }}</h2>
              {{ if section.props.subtitle }}
              <p>{{ section.props.subtitle }}</p>
              {{ end }}
            </section>
            """);

        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "cta", "cta.html"),
            """
            <div class="cta">
              <a href="{{ section.props.url }}">{{ section.props.text }}</a>
            </div>
            """);

        var json = "[{\"type\":\"hero\",\"props\":{\"title\":\"Welcome\",\"subtitle\":\"This is a test\"}},{\"type\":\"cta\",\"props\":{\"text\":\"Go Now\",\"url\":\"/signup\"}}]";

        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero/hero.html" },
                ["cta"] = new() { Template = "sections/cta/cta.html" }
            }
        };

        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir,
            parentLayoutsDir: null,
            shortcodes: null,
            components: null,
            userLayoutsDir: null,
            themeRegistry: registry,
            schemaValidator: null,
            dataResolver: null,
            componentValidation: "off");

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo
            {
                Title = "Test",
                Content = "",
                Url = "/test/"
            }
        };

        var result = renderer.RenderPage("pages/page.html", model);

        Assert.Contains("Welcome", result);
        Assert.Contains("This is a test", result);
        Assert.Contains("Go Now", result);
        Assert.Contains("/signup", result);
        Assert.Contains("class=\"hero\"", result);
        Assert.Contains("class=\"cta\"", result);
    }

    [Fact]
    public void RenderSection_EmptyInput_ReturnsComment()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '' }}");

        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0.0" };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "off");

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var result = renderer.RenderPage("pages/page.html", model);

        Assert.Contains("empty input", result);
    }

    [Fact]
    public void RenderSection_InvalidJson_ReturnsComment()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section 'not json' }}");

        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0.0" };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "off");

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var result = renderer.RenderPage("pages/page.html", model);

        Assert.Contains("not valid JSON", result);
    }

    [Fact]
    public void RenderSection_InvalidJson_StrictThrows()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section 'not json' }}");

        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0.0" };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "strict");

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var ex = Assert.Throws<RenderException>(() => renderer.RenderPage("pages/page.html", model));
        Assert.Contains("theme.render_section.invalid_json", ex.Message);
    }

    [Fact]
    public void RenderSection_TemplateParseError_StrictThrows()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "{{ if }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '{\"type\":\"hero\"}' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero/hero.html" }
            }
        };

        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "strict");

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var ex = Assert.Throws<RenderException>(() => renderer.RenderPage("pages/page.html", model));
        Assert.Contains("theme.section.template_parse_failed", ex.Message);
        Assert.Contains("hero", ex.Message);
    }

    [Fact]
    public void RenderSection_TemplateTraversal_DoesNotReadOutsideLayouts()
    {
        File.WriteAllText(Path.Combine(_themeDir, "secret.html"), "SECRET_OUTSIDE_LAYOUTS");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '{\"type\":\"hero\"}' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "../secret.html" }
            }
        };

        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "off");

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var result = renderer.RenderPage("pages/page.html", model);

        Assert.DoesNotContain("SECRET_OUTSIDE_LAYOUTS", result);
        Assert.Contains("section template not found", result);
    }

    [Fact]
    public void RenderSection_WithDataBinding_ResolvesAndInjectsItems()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            """
            <section class="hero">
              <h2>{{ section.props.title }}</h2>
              {{ for item in items }}
              <div class="item">{{ item.title }}|{{ item.url }}</div>
              {{ end }}
            </section>
            """);

        var json = "[{\"type\":\"hero\",\"source\":\"posts\",\"limit\":2}]";

        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero/hero.html" }
            }
        };

        var allPages = new List<(ContentItem, RouteInfo?)>
        {
            (new ContentItem("post1", "First Post", "first-post", DateTimeOffset.UtcNow, null,
                new Dictionary<string, object> { ["type"] = "posts" }), null),
            (new ContentItem("post2", "Second Post", "second-post", DateTimeOffset.UtcNow, null,
                new Dictionary<string, object> { ["type"] = "posts" }), null),
            (new ContentItem("other", "Other Page", "other", DateTimeOffset.UtcNow, null,
                new Dictionary<string, object> { ["type"] = "page" }), null)
        };

        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "off", allPages);

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var result = renderer.RenderPage("pages/page.html", model);

        Assert.Contains("First Post", result);
        Assert.Contains("Second Post", result);
        Assert.DoesNotContain("Other Page", result);
    }

    [Fact]
    public void RenderSection_WithSourceFilter_AppliesFilter()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "cardGrid", "card-grid.html"),
            """
            <div class="grid">
              {{ for item in items }}
              <span>{{ item.fields.featured }}</span>
              {{ end }}
            </div>
            """);

        var json = "[{\"type\":\"cardGrid\",\"source\":\"*\",\"filter\":{\"featured\":true},\"limit\":1}]";

        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Sections = new()
            {
                ["cardGrid"] = new() { Template = "sections/cardGrid/card-grid.html" }
            }
        };

        var allPages = new List<(ContentItem, RouteInfo?)>
        {
            (new ContentItem("a", "A", "a", DateTimeOffset.UtcNow, null,
                new Dictionary<string, object>(),
                new Dictionary<string, ContentField> { ["featured"] = new("boolean", true) }), null),
            (new ContentItem("b", "B", "b", DateTimeOffset.UtcNow, null,
                new Dictionary<string, object>(),
                new Dictionary<string, ContentField> { ["featured"] = new("boolean", false) }), null)
        };

        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "off", allPages);

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var result = renderer.RenderPage("pages/page.html", model);

        Assert.Contains("<span>true</span>", result);
        Assert.DoesNotContain("<span>false</span>", result);
    }

    [Fact]
    public void RenderSection_NoSource_NoItemsInjected()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<h1>{{ section.props.title }}</h1>");

        var json = "[{\"type\":\"hero\",\"props\":{\"title\":\"Hello\"}}]";

        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero/hero.html" }
            }
        };

        var allPages = new List<(ContentItem, RouteInfo?)>
        {
            (new ContentItem("p1", "Post", "post", DateTimeOffset.UtcNow, null,
                new Dictionary<string, object> { ["type"] = "posts" }), null)
        };

        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, "off", allPages);

        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        };

        var result = renderer.RenderPage("pages/page.html", model);

        Assert.Contains("Hello", result);
        Assert.DoesNotContain("Post", result);
    }
}
