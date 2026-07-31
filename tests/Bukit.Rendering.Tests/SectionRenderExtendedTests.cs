using Xunit;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;
using Bukit.Rendering.Scriban;
using Scriban;
using Scriban.Runtime;

namespace Bukit.Rendering.Tests;

public sealed class SectionRenderExtendedTests : IDisposable
{
    private readonly string _themeDir;
    private readonly string _layoutsDir;

    public SectionRenderExtendedTests()
    {
        _themeDir = Path.Combine(Path.GetTempPath(), "bukit-ext-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_themeDir, "layouts");
        Directory.CreateDirectory(_layoutsDir);
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "sections", "hero"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "sections", "posts"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "components"));
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_themeDir, recursive: true);
    }

    private static SiteModel CreateSite() => new()
    {
        Name = "test", Title = "Test", Url = "https://example.com", BaseUrl = "/", Language = "en"
    };

    private ScribanTemplateRenderer CreateRenderer(
        ThemeManifestV2 manifest,
        IReadOnlyList<(ContentDocument, RouteInfo?)>? allPages = null,
        string validation = "off",
        IReadOnlyDictionary<string, ISectionPlugin>? sectionPlugins = null)
    {
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        return new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, null, null, validation, allPages, sectionPlugins, null);
    }

    private PageModel CreateModel() => new()
    {
        Site = CreateSite(),
        Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
    };

    // ── Section with variant ──────────────────────────────────────────

    [Fact]
    public void RenderSection_WithVariant_UsesVariantTemplate()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<div class=\"default\">{{ section.props.title }}</div>");
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero-compact.html"),
            "<div class=\"compact\">{{ section.props.title }}</div>");

        var json = "[{\"type\":\"hero\",\"variant\":\"compact\",\"props\":{\"title\":\"Compact\"}}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new()
                {
                    Template = "sections/hero/hero.html",
                    Variants = new()
                    {
                        ["compact"] = new() { Template = "sections/hero/hero-compact.html" }
                    }
                }
            }
        };

        var result = CreateRenderer(manifest).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("compact", result);
        Assert.Contains("Compact", result);
    }

    // ── Section with components ───────────────────────────────────────

    [Fact]
    public void RenderSection_WithComponent_DataNotScriptObject_ReturnsComment()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<div>{{ render_component 'badge' 'not-a-script-object' }}</div>");
        File.WriteAllText(Path.Combine(_layoutsDir, "components", "badge.html"),
            "<span class=\"badge\">New</span>");

        var json = "[{\"type\":\"hero\"}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["hero"] = new() { Template = "sections/hero/hero.html" } },
            Components = new() { ["badge"] = new() { Template = "components/badge.html" } }
        };

        var result = CreateRenderer(manifest).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("component", result);
    }

    // ── Section with schema validation (warn mode) ────────────────────

    [Fact]
    public void RenderSection_WithSchemaValidator_WarnMode_NoThrow()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<div>{{ section.props.title }}</div>");

        var json = "[{\"type\":\"hero\",\"props\":{\"title\":\"OK\"}}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["hero"] = new() { Template = "sections/hero/hero.html" } }
        };

        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var validator = new SectionSchemaValidator(ValidationMode.Warn, _themeDir);
        var renderer = new ScribanTemplateRenderer(
            _layoutsDir, null, null, null, null,
            registry, validator, null, "warn");

        var result = renderer.RenderPage("pages/page.html", CreateModel());
        Assert.Contains("OK", result);
    }

    // ── Section unknown type ──────────────────────────────────────────

    [Fact]
    public void RenderSection_UnknownType_LenientMode()
    {
        var json = "[{\"type\":\"nonexistent\"}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0.0" };
        var result = CreateRenderer(manifest).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("section not found", result);
    }

    // ── Section with sort ─────────────────────────────────────────────

    [Fact]
    public void RenderSection_WithSortAndLimit_AppliesCorrectly()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "posts", "posts.html"),
            """{{ for item in items }}<span>{{ item.title }}</span>{{ end }}""");

        var json = "[{\"type\":\"posts\",\"source\":\"posts\",\"sort\":\"title desc\",\"limit\":2}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var allPages = new List<(ContentDocument, RouteInfo?)>
        {
            (ContentDocument.Create("a", "Alpha", "alpha", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), null,
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "posts" })), null),
            (ContentDocument.Create("b", "Bravo", "bravo", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), null,
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "posts" })), null),
            (ContentDocument.Create("c", "Charlie", "charlie", new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero), null,
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "posts" })), null)
        };

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["posts"] = new() { Template = "sections/posts/posts.html" } }
        };

        var result = CreateRenderer(manifest, allPages).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("Charlie", result);
        Assert.Contains("Bravo", result);
        Assert.DoesNotContain("Alpha", result);
    }

    // ── Section with plugin hooks ─────────────────────────────────────

    private sealed class TestBeforePlugin : ISectionPlugin
    {
        public SectionHook SupportedHook => SectionHook.BeforeRender;
        public Task ExecuteAsync(SectionContext ctx, CancellationToken ct = default)
        {
            if (ctx.Props is not null)
                ctx.Props["title"] = "ModifiedByPlugin";
            return Task.CompletedTask;
        }
    }

    private sealed class TestAfterPlugin : ISectionPlugin
    {
        public SectionHook SupportedHook => SectionHook.AfterRender;
        public Task ExecuteAsync(SectionContext ctx, CancellationToken ct = default)
        {
            ctx.RenderedHtml = "<!-- wrapped -->" + ctx.RenderedHtml;
            return Task.CompletedTask;
        }
    }

    private sealed class TestFailingPlugin : ISectionPlugin
    {
        public SectionHook SupportedHook => SectionHook.BeforeRender;
        public Task ExecuteAsync(SectionContext ctx, CancellationToken ct = default) => throw new InvalidOperationException("plugin boom");
    }

    [Fact]
    public void RenderSection_BeforePlugin_ModifiesProps()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<h1>{{ section.props.title }}</h1>");

        var json = "[{\"type\":\"hero\",\"props\":{\"title\":\"Original\"}}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["hero"] = new() { Template = "sections/hero/hero.html", Plugin = "before" } }
        };

        var plugins = new Dictionary<string, ISectionPlugin> { ["before"] = new TestBeforePlugin() };
        var result = CreateRenderer(manifest, sectionPlugins: plugins).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("ModifiedByPlugin", result);
    }

    [Fact]
    public void RenderSection_AfterPlugin_WrapsOutput()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<h1>Hello</h1>");

        var json = "[{\"type\":\"hero\"}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["hero"] = new() { Template = "sections/hero/hero.html", Plugin = "after" } }
        };

        var plugins = new Dictionary<string, ISectionPlugin> { ["after"] = new TestAfterPlugin() };
        var result = CreateRenderer(manifest, sectionPlugins: plugins).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("<!-- wrapped -->", result);
    }

    [Fact]
    public void RenderSection_PluginFails_Lenient_ReturnsComment()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<h1>Hello</h1>");

        var json = "[{\"type\":\"hero\"}]";
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '" + json + "' }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["hero"] = new() { Template = "sections/hero/hero.html", Plugin = "fail" } }
        };

        var plugins = new Dictionary<string, ISectionPlugin> { ["fail"] = new TestFailingPlugin() };
        var result = CreateRenderer(manifest, sectionPlugins: plugins).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("plugin error", result);
    }

    // ── SectionRenderHelper direct ────────────────────────────────────

    [Fact]
    public void SectionRenderHelper_RenderScriptObject_MissingType()
    {
        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0.0" };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var helper = new SectionRenderHelper(
            registry, null, "off", new FileTemplateLoader(_layoutsDir, null),
            new ScriptObject(), null);

        var so = new ScriptObject();
        var result = helper.RenderScriptObjectSection(so, new ScriptObject());
        Assert.Contains("missing type", result);
    }

    [Fact]
    public void SectionRenderHelper_RenderScriptObject_SectionNotFound()
    {
        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0.0" };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var helper = new SectionRenderHelper(
            registry, null, "off", new FileTemplateLoader(_layoutsDir, null),
            new ScriptObject(), null);

        var so = new ScriptObject { ["type"] = "nonexistent" };
        var result = helper.RenderScriptObjectSection(so, new ScriptObject());
        Assert.Contains("section not found", result);
    }

    [Fact]
    public void SectionRenderHelper_RenderScriptObject_TemplateNotFound()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"), "<h1>Hero</h1>");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["hero"] = new() { Template = "sections/hero/missing.html" } }
        };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var helper = new SectionRenderHelper(
            registry, null, "off", new FileTemplateLoader(_layoutsDir, null),
            new ScriptObject(), null);

        var so = new ScriptObject { ["type"] = "hero" };
        var result = helper.RenderScriptObjectSection(so, new ScriptObject());
        Assert.Contains("template not found", result);
    }

    [Fact]
    public void SectionRenderHelper_RenderScriptObject_WithProps()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "sections", "hero", "hero.html"),
            "<h1>{{ section.props.title }}</h1>");

        var manifest = new ThemeManifestV2
        {
            Name = "test", Version = "1.0.0",
            Sections = new() { ["hero"] = new() { Template = "sections/hero/hero.html" } }
        };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);
        var helper = new SectionRenderHelper(
            registry, null, "off", new FileTemplateLoader(_layoutsDir, null),
            new ScriptObject(), null);

        var propsSo = new ScriptObject { ["title"] = "Direct Render" };
        var so = new ScriptObject
        {
            ["type"] = "hero",
            ["props"] = propsSo,
            ["limit"] = "5",
            ["sort"] = "date desc"
        };
        var result = helper.RenderScriptObjectSection(so, new ScriptObject());
        Assert.Contains("Direct Render", result);
    }

    // ── Empty JSON render_section ─────────────────────────────────────

    [Fact]
    public void RenderSection_EmptyArray_NoSectionsParsed()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"),
            "{{ render_section '[]' }}");

        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0.0" };
        var result = CreateRenderer(manifest).RenderPage("pages/page.html", CreateModel());
        Assert.Contains("no sections parsed", result);
    }

    // ── ThemeComponentRenderFunction with valid template ──────────────

    [Fact]
    public void ThemeComponentRenderFunction_ValidTemplate_Renders()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-comp-{Guid.NewGuid():N}");
        var compDir = Path.Combine(dir, "components");
        Directory.CreateDirectory(compDir);
        File.WriteAllText(Path.Combine(compDir, "badge.html"), "<span>{{ label }}</span>");

        try
        {
            var fn = new ThemeComponentRenderFunction(
                new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["badge"] = new() { Template = "components/badge.html" }
                },
                new FileTemplateLoader(dir, null),
                new ScriptObject(),
                dir,
                "lenient");
            var data = new ScriptObject { ["label"] = "Hot" };
            var result = fn.Render("badge", data);
            Assert.Contains("Hot", result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRenderFunction_InvalidTemplate_StrictThrows()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["bad"] = new() { Template = "" }
            },
            null!,
            new ScriptObject(),
            "/tmp",
            "strict");
        Assert.Throws<RenderException>(() => fn.Render("bad", null));
    }

    [Fact]
    public void ThemeComponentRenderFunction_TemplateParseError_StrictThrows()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-parse-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bad.html"), "{{ if }}");

        try
        {
            var fn = new ThemeComponentRenderFunction(
                new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bad"] = new() { Template = "bad.html" }
                },
                new FileTemplateLoader(dir, null),
                new ScriptObject(),
                dir,
                "strict");
            Assert.Throws<RenderException>(() => fn.Render("bad", null));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

}
