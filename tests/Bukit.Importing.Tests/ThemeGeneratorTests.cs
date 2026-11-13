using Bukit.Theme;
using Bukit.Config;
using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ThemeGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public ThemeGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-theme-generator-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Theory]
    [InlineData("Insights & News", "insights-news")]
    [InlineData("--Already__Safe--", "already__safe")]
    [InlineData("中文 Landing", "中文-landing")]
    public void SanitizeTemplateName_NormalizesUnsafeCharacters(string input, string expected)
    {
        Assert.Equal(expected, ThemeGenerator.SanitizeTemplateName(input));
    }

    [Fact]
    public void GetDefaultTemplateBody_ListPage_ContainsPagesLoop()
    {
        var template = ThemeGenerator.GetDefaultTemplateBody(PageType.PostList);

        Assert.Contains("{{ for p in pages }}", template, StringComparison.Ordinal);
        Assert.Contains("{{ page.title }}", template, StringComparison.Ordinal);
        Assert.DoesNotContain("{{ this.title }}", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_GivesEveryPageItsOwnTemplateAndKeepsAssetsOutOfBase()
    {
        const string themeName = "true";
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = themeName,
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            CreatePage("home.html", "", PageType.Home, "Home", """
                <main>
                  <h1>Ignored Home Heading</h1>
                </main>
                """, "<link rel=\"stylesheet\" href=\"/css/site.css\" />"),
            CreatePage("about.html", "about", PageType.Page, "About", "", "<script src=\"/js/app.js\"></script>"),
            CreatePage("insights.html", "insights", PageType.PostList, "Insights", "")
        };

        var layout = new LayoutExtractor.LayoutInfo(
            Header: "<header><a href=\"/\">Home</a></header>",
            Nav: "<nav><ul><li><a href=\"/about/\">About</a></li></ul></nav>",
            Footer: "<footer>Footer</footer>",
            HeaderContainsNav: false);

        // The home page always renders pages/index.html; "landing" therefore belongs to about only.
        var routeMap = new RouteMapConfig
        {
            Pages =
            [
                new RouteMapPage { Source = "home.html", Template = "landing", Type = "Home", Route = "/" },
                new RouteMapPage { Source = "about.html", Template = "landing", Type = "Page", Route = "/about/" }
            ]
        };

        var result = ThemeGenerator.Generate(
            options,
            pages,
            layout,
            warnings: [],
            pathMappings: new Dictionary<string, string>
            {
                ["/css/site.css"] = "/assets/site.css",
                ["/js/app.js"] = "/assets/app.js"
            },
            routeMap);

        var themeRoot = Path.Combine(_tempDir, "themes", themeName);
        var pagesDir = Path.Combine(themeRoot, "layouts", "pages");
        Assert.Equal(themeRoot, result.ThemePath);
        Assert.Equal(3, result.PartialsGenerated);
        // 3 page templates + page/list/post fallbacks.
        Assert.Equal(6, result.TemplatesGenerated);

        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "nav.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "footer.html")));
        Assert.Equal(
            new[] { "index.html", "insights.html", "landing.html", "list.html", "page.html", "post.html" },
            Directory.GetFiles(pagesDir).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray());

        var baseLayout = File.ReadAllText(Path.Combine(themeRoot, "layouts", "layouts", "base.html"));
        Assert.DoesNotContain("site.css", baseLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("app.js", baseLayout, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"{{ site.language | html.escape }}\">", baseLayout, StringComparison.Ordinal);
        Assert.Contains("<title>{{ page.fields.seo_title.value | html.escape }} | {{ site.title | html.escape }}</title>", baseLayout, StringComparison.Ordinal);
        Assert.Contains("<title>{{ page.title | html.escape }} | {{ site.title | html.escape }}</title>", baseLayout, StringComparison.Ordinal);
        Assert.Contains("content=\"{{ page.fields.seo_desc.value | html.escape }}\"", baseLayout, StringComparison.Ordinal);
        Assert.Contains("href=\"{{ page.seo.canonical | html.escape }}\"", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ page_head }}", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ page_scripts }}", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ include 'partials/header.html' }}", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ include 'partials/nav.html' }}", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ include 'partials/footer.html' }}", baseLayout, StringComparison.Ordinal);

        var home = File.ReadAllText(Path.Combine(pagesDir, "index.html"));
        Assert.Contains("{{ capture page_head }}", home, StringComparison.Ordinal);
        Assert.Contains("{{ base_url }}/assets/site.css", home, StringComparison.Ordinal);
        Assert.DoesNotContain("app.js", home, StringComparison.Ordinal);

        var about = File.ReadAllText(Path.Combine(pagesDir, "landing.html"));
        Assert.Contains("{{ base_url }}/assets/app.js", about, StringComparison.Ordinal);
        Assert.DoesNotContain("site.css", about, StringComparison.Ordinal);

        foreach (var fallback in new[] { "page.html", "list.html", "post.html" })
        {
            var text = File.ReadAllText(Path.Combine(pagesDir, fallback));
            Assert.DoesNotContain("capture", text, StringComparison.Ordinal);
            Assert.DoesNotContain("assets/", text, StringComparison.Ordinal);
        }

        var themeYaml = File.ReadAllText(Path.Combine(themeRoot, "theme.yaml"));
        Assert.Contains("name: \"true\"", themeYaml, StringComparison.Ordinal);
        Assert.Contains("version: 1.0.0", themeYaml, StringComparison.Ordinal);
        Assert.Contains("engine: bukit", themeYaml, StringComparison.Ordinal);
        Assert.Contains("template: pages/index.html", themeYaml, StringComparison.Ordinal);
        Assert.Contains("template: pages/page.html", themeYaml, StringComparison.Ordinal);
        Assert.Contains("template: pages/insights.html", themeYaml, StringComparison.Ordinal);
        Assert.Empty(ThemeManifestYamlValidator.Validate(themeRoot));
    }

    private static DiscoveredPage CreatePage(
        string relativePath,
        string slug,
        PageType type,
        string title,
        string uniqueBody,
        string? headContent = null)
    {
        return new DiscoveredPage
        {
            FilePath = Path.Combine("/demo", relativePath),
            RelativePath = relativePath,
            Slug = slug,
            Type = type,
            Title = title,
            UniqueBody = uniqueBody,
            HeadContent = headContent
        };
    }
}
