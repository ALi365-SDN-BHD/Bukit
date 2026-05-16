using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneThemeGeneratorTests : IDisposable
{
    private readonly string _rootDir;

    public CloneThemeGeneratorTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-clone-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void WriteTo_MinimalTokens_GeneratesAllFiles()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = CloneLayoutInfo.Default;

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout);

        var themeRoot = Path.Combine(_rootDir, "themes", "test-clone");
        Assert.True(Directory.Exists(Path.Combine(themeRoot, "assets")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "footer.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "list-card.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "pagination-nav.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "page.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "post.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "list.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "pagination.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "taxonomy-index.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "taxonomy-term.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "search.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "bukit.templates.yaml")));
    }

    [Fact]
    public void WriteTo_MinimalTokens_SetsPrimaryCssVariable()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = CloneLayoutInfo.Default;

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("--primary: #ff0000;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_FullTokens_AllCssVariablesSet()
    {
        var tokens = new CloneTokens
        {
            Bg = "#ffffff",
            Surface = "#fafafa",
            SurfaceMuted = "#f0f0f0",
            Text = "#111111",
            Muted = "#888888",
            Border = "#dddddd",
            Primary = "#3b82f6",
            PrimaryStrong = "#2563eb",
            Accent = "#10b981",
            Radius = "12px",
            ContentMax = "800px",
            WideMax = "1200px",
            Shadow = "0 4px 12px rgba(0,0,0,0.1)",
            FontFamily = "Inter, sans-serif",
            CodeFontFamily = "Fira Code, monospace"
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("--bg: #ffffff;", css, StringComparison.Ordinal);
        Assert.Contains("--surface: #fafafa;", css, StringComparison.Ordinal);
        Assert.Contains("--surface-muted: #f0f0f0;", css, StringComparison.Ordinal);
        Assert.Contains("--text: #111111;", css, StringComparison.Ordinal);
        Assert.Contains("--muted: #888888;", css, StringComparison.Ordinal);
        Assert.Contains("--border: #dddddd;", css, StringComparison.Ordinal);
        Assert.Contains("--primary: #3b82f6;", css, StringComparison.Ordinal);
        Assert.Contains("--primary-strong: #2563eb;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #10b981;", css, StringComparison.Ordinal);
        Assert.Contains("--radius: 12px;", css, StringComparison.Ordinal);
        Assert.Contains("--content: 800px;", css, StringComparison.Ordinal);
        Assert.Contains("--wide: 1200px;", css, StringComparison.Ordinal);
        Assert.Contains("--shadow: 0 4px 12px rgba(0,0,0,0.1);", css, StringComparison.Ordinal);
        Assert.Contains("font-family: Inter, sans-serif;", css, StringComparison.Ordinal);
        Assert.Contains("font-family: Fira Code, monospace;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_NullTokens_AllFallbackToDefaults()
    {
        var tokens = new CloneTokens();
        var layout = CloneLayoutInfo.Default;

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("--primary: #0b5fff;", css, StringComparison.Ordinal);
        Assert.Contains("--bg: #fbfaf8;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #0f7b6c;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_GoogleFonts_BaseHasLinkTags()
    {
        var tokens = new CloneTokens
        {
            Primary = "#ff0000",
            GoogleFontsUrl = "https://fonts.googleapis.com/css2?family=Inter:wght@400;700&display=swap"
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var baseHtml = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "layouts", "base.html"));
        Assert.Contains("fonts.googleapis.com", baseHtml, StringComparison.Ordinal);
        Assert.Contains("fonts.gstatic.com", baseHtml, StringComparison.Ordinal);
        Assert.Contains("Inter:wght@400;700", baseHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_NoGoogleFonts_BaseHasNoFontLinks()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var baseHtml = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "layouts", "base.html"));
        Assert.DoesNotContain("fonts.googleapis.com", baseHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithLayoutInfo_IndexHasHeroSection()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            SiteTitle = "My Clone",
            HeroHeading = "Welcome to Cloned Site",
            HeroSubtext = "A pixel-perfect Bukit theme clone",
            HasFeaturesSection = true
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout);

        var indexHtml = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "pages", "index.html"));
        Assert.Contains("My Clone", indexHtml, StringComparison.Ordinal);
        Assert.Contains("Welcome to Cloned Site", indexHtml, StringComparison.Ordinal);
        Assert.Contains("pixel-perfect Bukit theme clone", indexHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"section-heading\">Featured", indexHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithoutLayoutInfo_IndexHasStandardHero()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var indexHtml = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "pages", "index.html"));
        Assert.Contains("{{ site.title }}", indexHtml, StringComparison.Ordinal);
        Assert.Contains("Latest content", indexHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithBrand_HeaderUsesBrand()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo { SiteTitle = "Original Title" };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout, brand: "My Brand");

        var headerHtml = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "partials", "header.html"));
        Assert.Contains("My Brand", headerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_OverwritesExistingTheme()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var tokens2 = new CloneTokens { Primary = "#00ff00" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens2, CloneLayoutInfo.Default);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("--primary: #00ff00;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--primary: #ff0000;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateStyleCss_AllFieldsNull_ProducesDefaultCss()
    {
        var tokens = new CloneTokens();

        var css = CloneThemeGenerator.GenerateStyleCss(tokens);

        Assert.Contains("--primary: #0b5fff;", css, StringComparison.Ordinal);
        Assert.Contains("--bg: #fbfaf8;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #0f7b6c;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateStyleCss_WhitespaceFields_FallbackToDefaults()
    {
        var tokens = new CloneTokens { Primary = "   ", Accent = "\t" };

        var css = CloneThemeGenerator.GenerateStyleCss(tokens);

        Assert.Contains("--primary: #0b5fff;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #0f7b6c;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBaseLayout_GoogleFonts_IncludesPreconnectAndLink()
    {
        var tokens = new CloneTokens
        {
            GoogleFontsUrl = "https://fonts.googleapis.com/css2?family=Roboto&display=swap"
        };

        var html = CloneThemeGenerator.GenerateBaseLayout(tokens);

        Assert.Contains("fonts.googleapis.com", html, StringComparison.Ordinal);
        Assert.Contains("fonts.gstatic.com", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBaseLayout_NoGoogleFonts_NoFontReferences()
    {
        var tokens = new CloneTokens();

        var html = CloneThemeGenerator.GenerateBaseLayout(tokens);

        Assert.DoesNotContain("fonts.googleapis.com", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateIndex_HeroLayout_HasExplicitContent()
    {
        var layout = new CloneLayoutInfo
        {
            HeroHeading = "Test Heading",
            HeroSubtext = "Test Subtext"
        };

        var html = CloneThemeGenerator.GenerateIndex(layout, null);

        Assert.Contains("Test Heading", html, StringComparison.Ordinal);
        Assert.Contains("Test Subtext", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateIndex_DefaultLayout_HasScribanVariables()
    {
        var layout = CloneLayoutInfo.Default;

        var html = CloneThemeGenerator.GenerateIndex(layout, null);

        Assert.Contains("{{ site.title }}", html, StringComparison.Ordinal);
        Assert.Contains("Latest content", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateHeader_BrandProvided_UsesBrand()
    {
        var html = CloneThemeGenerator.GenerateHeader("My Brand");

        Assert.Contains("My Brand", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateHeader_NullBrand_UsesSiteTitle()
    {
        var html = CloneThemeGenerator.GenerateHeader(null);

        Assert.Contains("{{ site.title }}", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateHeader_HtmlChars_EscapedInBrand()
    {
        var html = CloneThemeGenerator.GenerateHeader("<script>alert('xss')</script>");

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }
}

public sealed class CloneModelsTests
{
    [Fact]
    public void CloneTokens_FromJson_FlatFormat_ParsesAllFields()
    {
        var json = """
{
  "bg": "#ffffff",
  "primary": "#3b82f6",
  "accent": "#10b981",
  "googleFontsUrl": "https://fonts.googleapis.com/css2?family=Inter"
}
""";

        var tokens = CloneTokens.FromJson(json);

        Assert.Equal("#ffffff", tokens.Bg);
        Assert.Equal("#3b82f6", tokens.Primary);
        Assert.Equal("#10b981", tokens.Accent);
        Assert.Equal("https://fonts.googleapis.com/css2?family=Inter", tokens.GoogleFontsUrl);
        Assert.Null(tokens.Text);
    }

    [Fact]
    public void CloneTokens_FromJson_WrapperFormat_ParsesFields()
    {
        var json = """
{
  "tokens": {
    "primary": "#ff0000",
    "accent": "#00ff00"
  }
}
""";

        var tokens = CloneTokens.FromJson(json);

        Assert.Equal("#ff0000", tokens.Primary);
        Assert.Equal("#00ff00", tokens.Accent);
    }

    [Fact]
    public void CloneTokens_FromJson_EmptyJson_ReturnsDefault()
    {
        var tokens = CloneTokens.FromJson("{}");

        Assert.Null(tokens.Primary);
        Assert.Null(tokens.Bg);
    }

    [Fact]
    public void CloneTokens_FromJson_NullJson_ReturnsDefault()
    {
        var tokens = CloneTokens.FromJson(null!);

        Assert.Null(tokens.Primary);
    }

    [Fact]
    public void CloneTokens_FromJson_InvalidJson_ReturnsDefault()
    {
        var tokens = CloneTokens.FromJson("not json");

        Assert.Null(tokens.Primary);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_ParsesAllFields()
    {
        var json = """
{
  "siteTitle": "My Site",
  "heroHeading": "Welcome",
  "heroSubtext": "Hello World",
  "hasFeaturesSection": true,
  "hasCTASection": false
}
""";

        var layout = CloneLayoutInfo.FromJson(json);

        Assert.Equal("My Site", layout.SiteTitle);
        Assert.Equal("Welcome", layout.HeroHeading);
        Assert.Equal("Hello World", layout.HeroSubtext);
        Assert.True(layout.HasFeaturesSection);
        Assert.False(layout.HasCTASection);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_Empty_ReturnsDefault()
    {
        var layout = CloneLayoutInfo.FromJson("{}");

        Assert.Null(layout.SiteTitle);
        Assert.False(layout.HasFeaturesSection);
    }

    [Fact]
    public void CloneModels_IsSafeThemeName_SimpleNames_Allowed()
    {
        Assert.True(CloneModels.IsSafeThemeName("my-theme"));
        Assert.True(CloneModels.IsSafeThemeName("clone_2024"));
        Assert.True(CloneModels.IsSafeThemeName("starter"));
    }

    [Fact]
    public void CloneModels_IsSafeThemeName_DangerousNames_Rejected()
    {
        Assert.False(CloneModels.IsSafeThemeName(null));
        Assert.False(CloneModels.IsSafeThemeName(""));
        Assert.False(CloneModels.IsSafeThemeName("   "));
        Assert.False(CloneModels.IsSafeThemeName("."));
        Assert.False(CloneModels.IsSafeThemeName(".."));
        Assert.False(CloneModels.IsSafeThemeName("/etc/passwd"));
        Assert.False(CloneModels.IsSafeThemeName("../etc/passwd"));
        Assert.False(CloneModels.IsSafeThemeName("theme/sub"));
    }
}
