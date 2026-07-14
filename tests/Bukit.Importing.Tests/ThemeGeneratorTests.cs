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
    public void Generate_WritesThemeFilesAndFallsBackOnDuplicateRouteMapTemplates()
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
            HeadExtras: "<link rel=\"stylesheet\" href=\"/css/site.css\" />",
            HeaderContainsNav: false);

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
        Assert.Equal(themeRoot, result.ThemePath);
        Assert.Equal(3, result.PartialsGenerated);
        Assert.Equal(5, result.TemplatesGenerated);

        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "nav.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "footer.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "landing.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "about.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "insights.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "list.html")));

        var baseLayout = File.ReadAllText(Path.Combine(themeRoot, "layouts", "layouts", "base.html"));
        Assert.Contains("{{ base_url }}/assets/site.css", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ base_url }}/assets/app.js", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ include 'partials/header.html' }}", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ include 'partials/nav.html' }}", baseLayout, StringComparison.Ordinal);
        Assert.Contains("{{ include 'partials/footer.html' }}", baseLayout, StringComparison.Ordinal);

        var indexTemplate = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "index.html"));
        Assert.Contains("<li><a href=\"/\">Home</a></li>", indexTemplate, StringComparison.Ordinal);
        Assert.Contains("<li><a href=\"/about/\">About</a></li>", indexTemplate, StringComparison.Ordinal);

        var themeYaml = File.ReadAllText(Path.Combine(themeRoot, "theme.yaml"));
        Assert.Contains("name: \"true\"", themeYaml, StringComparison.Ordinal);
        Assert.Contains("version: 1.0.0", themeYaml, StringComparison.Ordinal);
        Assert.Contains("engine: bukit", themeYaml, StringComparison.Ordinal);
        Assert.Contains("template: pages/index.html", themeYaml, StringComparison.Ordinal);
        Assert.Contains("template: pages/page.html", themeYaml, StringComparison.Ordinal);
        Assert.Contains("template: pages/insights.html", themeYaml, StringComparison.Ordinal);
        Assert.Empty(ConfigValidator.ValidateThemeYaml(themeRoot));
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
