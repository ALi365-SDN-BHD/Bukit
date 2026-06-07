using Xunit;
using Bukit.Rendering;
using Bukit.Rendering.Scriban;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class BuildCompatibilityTests : IDisposable
{
    private readonly string _rootDir;

    public BuildCompatibilityTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-compat-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "old-theme", "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "old-theme", "assets"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "new-theme", "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "new-theme", "layouts", "sections", "hero"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "new-theme", "assets"));
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
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
    public void OldTheme_NoThemeYaml_ScribanRenderWorks()
    {
        var layoutsDir = Path.Combine(_rootDir, "themes", "old-theme", "layouts");

        File.WriteAllText(Path.Combine(layoutsDir, "pages", "page.html"),
            "<html><body>{{ page.title }}</body></html>");

        var renderer = new ScribanTemplateRenderer(layoutsDir);
        var result = renderer.RenderPage("pages/page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Old Theme", Content = "", Url = "/test/" }
        });

        Assert.Contains("Old Theme", result);
        Assert.Contains("<body>", result);
    }

    [Fact]
    public void NewTheme_WithThemeYaml_RenderSectionWorks()
    {
        var layoutsDir = Path.Combine(_rootDir, "themes", "new-theme", "layouts");
        var themeRoot = Path.Combine(_rootDir, "themes", "new-theme");

        File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), """
            name: new-theme
            version: 1.0.0
            sections:
              hero:
                template: sections/hero/hero.html
            """);

        File.WriteAllText(Path.Combine(layoutsDir, "sections", "hero", "hero.html"),
            "<h1>{{ section.props.title }}</h1>");

        File.WriteAllText(Path.Combine(layoutsDir, "pages", "page.html"),
            "{{ render_section '[{\"type\":\"hero\",\"props\":{\"title\":\"New Theme Works\"}}]' }}");

        var manifest = ThemeManifestLoader.Load(themeRoot);
        Assert.NotNull(manifest);

        var registry = new ThemeComponentRegistry(themeRoot, manifest!, null);
        var renderer = new ScribanTemplateRenderer(
            layoutsDir, null, null, null, null,
            registry, null, null, "off", null);

        var result = renderer.RenderPage("pages/page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "New", Content = "", Url = "/test/" }
        });

        Assert.Contains("New Theme Works", result);
        Assert.Contains("<h1>", result);
    }

    [Fact]
    public void BothPaths_Coexist_NoRegistryMeansOldPath()
    {
        var layoutsDir = Path.Combine(_rootDir, "themes", "old-theme", "layouts");

        File.WriteAllText(Path.Combine(layoutsDir, "pages", "page.html"),
            "<p>{{ page.title }}</p>");

        var renderer = new ScribanTemplateRenderer(layoutsDir);
        var result = renderer.RenderPage("pages/page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Coexist", Content = "", Url = "/test/" }
        });

        Assert.Contains("Coexist", result);
        Assert.DoesNotContain("render_section", result);
    }

    [Fact]
    public void ThemeManifestLoader_NoFile_Throws()
    {
        var nonExistentDir = Path.Combine(_rootDir, "themes", "nonexistent");

        Assert.Throws<ThemeManifestException>(() => ThemeManifestLoader.Load(nonExistentDir, true));
    }
}
