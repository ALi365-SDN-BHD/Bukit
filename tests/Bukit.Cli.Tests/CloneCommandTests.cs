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
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public void WriteTo_MinimalTokens_GeneratesAllFiles()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var tr = Path.Combine(_rootDir, "themes", "test-clone");
        Assert.True(File.Exists(Path.Combine(tr, "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "partials", "footer.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "partials", "list-card.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "partials", "pagination-nav.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "page.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "post.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "list.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "pagination.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "taxonomy-index.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "taxonomy-term.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "pages", "search.html")));
        Assert.True(File.Exists(Path.Combine(tr, "layouts", "bukit.templates.yaml")));
    }

    [Fact]
    public void WriteTo_FullTokens_AllCssVariablesSet()
    {
        var tokens = new CloneTokens
        {
            Bg = "#ffffff", Surface = "#fafafa", SurfaceMuted = "#f0f0f0",
            Text = "#111111", Muted = "#888888", Border = "#dddddd",
            Primary = "#3b82f6", PrimaryStrong = "#2563eb", Accent = "#10b981",
            Radius = "12px", ContentMax = "800px", WideMax = "1200px",
            Shadow = "0 2px 8px rgba(0,0,0,0.04)", CardShadow = "0 4px 12px rgba(0,0,0,0.1)",
            ModalShadow = "0 20px 60px rgba(0,0,0,0.2)", DropdownShadow = "0 4px 16px rgba(0,0,0,0.08)",
            NavPadding = "12px 32px", ContainerPadding = "48px 32px 80px", SectionGap = "48px",
            FontFamily = "Inter, sans-serif", CodeFontFamily = "Fira Code, monospace",
            ResponsiveBreakpoints = new() { Mobile = "768px", Tablet = "1024px", Desktop = "1280px" },
            SpacingScale = new() { Xs = "4px", Sm = "8px", Md = "16px", Lg = "32px", Xl = "64px" }
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("--primary: #3b82f6;", css, StringComparison.Ordinal);
        Assert.Contains("--card-shadow: 0 4px 12px rgba(0,0,0,0.1);", css, StringComparison.Ordinal);
        Assert.Contains("--modal-shadow: 0 20px 60px rgba(0,0,0,0.2);", css, StringComparison.Ordinal);
        Assert.Contains("--dropdown-shadow: 0 4px 16px rgba(0,0,0,0.08);", css, StringComparison.Ordinal);
        Assert.Contains("--nav-padding: 12px 32px;", css, StringComparison.Ordinal);
        Assert.Contains("--container-padding: 48px 32px 80px;", css, StringComparison.Ordinal);
        Assert.Contains("--section-gap: 48px;", css, StringComparison.Ordinal);
        Assert.Contains("--space-xs: 4px;", css, StringComparison.Ordinal);
        Assert.Contains("--space-xl: 64px;", css, StringComparison.Ordinal);
        Assert.Contains("--bp-mobile: 768px;", css, StringComparison.Ordinal);
        Assert.Contains("font-family: Inter, sans-serif;", css, StringComparison.Ordinal);
        Assert.Contains("font-family: Fira Code, monospace;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_GoogleFonts_BaseHasLinkTags()
    {
        var tokens = new CloneTokens { Primary = "#ff0000", GoogleFontsUrl = "https://fonts.googleapis.com/css2?family=Inter:wght@400;700&display=swap" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "layouts", "base.html"));
        Assert.Contains("fonts.googleapis.com", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_NoGoogleFonts_BaseHasNoFontLinks()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "layouts", "base.html"));
        Assert.DoesNotContain("fonts.googleapis.com", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithNavLinks_HeaderContainsLinks()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            NavLinks = [new() { Label = "Products", Url = "/products/" }, new() { Label = "About", Url = "/about/" }]
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "partials", "header.html"));
        Assert.Contains("Products", html, StringComparison.Ordinal);
        Assert.Contains("/about/", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithFooterLinks_FooterContainsLinks()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            FooterLinks = [new() { Label = "GitHub", Url = "https://github.com" }]
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "partials", "footer.html"));
        Assert.Contains("GitHub", html, StringComparison.Ordinal);
        Assert.Contains("https://github.com", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithHeroCta_IndexHasCtaButton()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            HeroHeading = "Test",
            HasHeroCta = true,
            HeroCtaText = "Get Started",
            HeroCtaUrl = "/signup"
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, layout);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "layouts", "pages", "index.html"));
        Assert.Contains("hero-cta", html, StringComparison.Ordinal);
        Assert.Contains("Get Started", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_NullTokens_AllFallbackToDefaults()
    {
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", new CloneTokens(), CloneLayoutInfo.Default);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("--primary: #0b5fff;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_DefaultBreakpoints_UsedWhenNotProvided()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("--bp-mobile: 680px;", css, StringComparison.Ordinal);
        Assert.Contains("--bp-tablet: 1024px;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_HeadingFontFamily_Applied()
    {
        var tokens = new CloneTokens { Primary = "#ff0000", HeadingFontFamily = "Georgia, serif" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-clone", tokens, CloneLayoutInfo.Default);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-clone", "assets", "style.css"));
        Assert.Contains("font-family: Georgia, serif;", css, StringComparison.Ordinal);
    }
}

public sealed class CloneModelsTests
{
    [Fact]
    public void CloneTokens_FromJson_ParsesNewFields()
    {
        var json = """{"cardShadow":"0 0 10px black","modalShadow":"0 0 50px black","navPadding":"10px 20px","responsiveBreakpoints":{"mobile":"640px","tablet":"960px"},"spacingScale":{"xs":"2px","sm":"4px"}}""";

        var t = CloneTokens.FromJson(json);

        Assert.Equal("0 0 10px black", t.CardShadow);
        Assert.Equal("0 0 50px black", t.ModalShadow);
        Assert.Equal("10px 20px", t.NavPadding);
        Assert.Equal("640px", t.ResponsiveBreakpoints?.Mobile);
        Assert.Equal("2px", t.SpacingScale?.Xs);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_ParsesNavAndFooterLinks()
    {
        var json = """{"navLinks":[{"label":"Home","url":"/"},{"label":"Blog","url":"/blog/"}],"footerLinks":[{"label":"Twitter","url":"https://twitter.com"}],"hasHeroCta":true,"heroCtaText":"Start","heroCtaUrl":"/go"}""";

        var l = CloneLayoutInfo.FromJson(json);

        Assert.Equal(2, l.NavLinks.Count); // has 2 items, not 1 — can't use Assert.Single
        Assert.Equal("Home", l.NavLinks[0].Label);
        Assert.Single(l.FooterLinks);
        Assert.True(l.HasHeroCta);
        Assert.Equal("Start", l.HeroCtaText);
    }

    [Fact]
    public void CloneTokens_FromJson_FlathFormat_ParsesAll()
    {
        var json = """{"bg":"#fff","primary":"#3b82f6"}""";
        var tokens = CloneTokens.FromJson(json);
        Assert.Equal("#fff", tokens.Bg);
    }

    [Fact]
    public void CloneTokens_FromJson_WrapperFormat_Parses()
    {
        var json = """{"tokens":{"primary":"#ff0000"}}""";
        Assert.Equal("#ff0000", CloneTokens.FromJson(json).Primary);
    }

    [Fact]
    public void CloneTokens_FromJson_Null_ReturnsDefault() => Assert.Null(CloneTokens.FromJson(null!).Primary);

    [Fact]
    public void CloneTokens_FromJson_Invalid_ReturnsDefault() => Assert.Null(CloneTokens.FromJson("bad").Primary);

    [Fact]
    public void CloneLayoutInfo_FromJson_Empty_Default() => Assert.Null(CloneLayoutInfo.FromJson("{}").SiteTitle);

    [Fact]
    public void CloneModels_IsSafeThemeName_Simple_Allowed() => Assert.True(CloneModels.IsSafeThemeName("my-theme"));

    [Fact]
    public void CloneModels_IsSafeThemeName_Nulls_Rejected()
    {
        Assert.False(CloneModels.IsSafeThemeName(null));
        Assert.False(CloneModels.IsSafeThemeName(""));
        Assert.False(CloneModels.IsSafeThemeName("."));
        Assert.False(CloneModels.IsSafeThemeName(".."));
        Assert.False(CloneModels.IsSafeThemeName("/etc/passwd"));
        Assert.False(CloneModels.IsSafeThemeName("theme/sub"));
    }
}
