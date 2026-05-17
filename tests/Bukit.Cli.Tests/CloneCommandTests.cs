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

    [Fact]
    public void WriteTo_StickyHeader_AddsStickyCss()
    {
        var behaviors = new CloneBehaviors { StickyHeader = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-sticky", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-sticky", "assets", "style.css"));
        Assert.Contains("position: sticky", css, StringComparison.Ordinal);
        Assert.Contains("z-index: 100", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_CardHoverLift_AddsHoverCss()
    {
        var behaviors = new CloneBehaviors { CardHoverLift = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-lift", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-lift", "assets", "style.css"));
        Assert.Contains("translateY(-3px)", css, StringComparison.Ordinal);
        Assert.Contains(".card:hover", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_AnimateOnScroll_AddsKeyframes()
    {
        var behaviors = new CloneBehaviors { AnimateOnScroll = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-anim", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-anim", "assets", "style.css"));
        Assert.Contains("@keyframes fadeInUp", css, StringComparison.Ordinal);
        Assert.Contains("animate-in", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_ScrollShrinkNav_AddsNavHiddenCss()
    {
        var behaviors = new CloneBehaviors { ScrollShrinkNav = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-shrink", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-shrink", "assets", "style.css"));
        Assert.Contains(".nav-hidden", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_MobileHamburger_AddsHamburgerCssAndHtml()
    {
        var behaviors = new CloneBehaviors { MobileHamburger = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-ham", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-ham", "assets", "style.css"));
        Assert.Contains(".hamburger", css, StringComparison.Ordinal);

        var headerHtml = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-ham", "layouts", "partials", "header.html"));
        Assert.Contains("hamburger-bar", headerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_DarkModeToggle_AddsDarkCss()
    {
        var behaviors = new CloneBehaviors { DarkModeToggle = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-dark", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-dark", "assets", "style.css"));
        Assert.Contains("body.dark", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithJsBehaviors_GeneratesBehaviorsJs()
    {
        var behaviors = new CloneBehaviors { ScrollShrinkNav = true, BackToTop = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-js", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var jsPath = Path.Combine(_rootDir, "themes", "test-js", "assets", "behaviors.js");
        Assert.True(File.Exists(jsPath));
        var js = File.ReadAllText(jsPath);
        Assert.Contains("nav-hidden", js, StringComparison.Ordinal);
        Assert.Contains("back-to-top", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithJsBehaviors_BaseHasScriptTag()
    {
        var behaviors = new CloneBehaviors { SmoothScroll = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-base-js", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-base-js", "layouts", "layouts", "base.html"));
        Assert.Contains("behaviors.js", html, StringComparison.Ordinal);
        Assert.Contains("defer", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithoutBehaviors_NoBehaviorsJs()
    {
        CloneThemeGenerator.WriteTo(_rootDir, "test-no-beh", new CloneTokens(), CloneLayoutInfo.Default);

        var jsPath = Path.Combine(_rootDir, "themes", "test-no-beh", "assets", "behaviors.js");
        Assert.False(File.Exists(jsPath));

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-no-beh", "layouts", "layouts", "base.html"));
        Assert.DoesNotContain("behaviors.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_DefaultBehaviors_NoSideEffects()
    {
        var defaultBehaviors = CloneBehaviors.Default;
        CloneThemeGenerator.WriteTo(_rootDir, "test-default-beh", new CloneTokens(), CloneLayoutInfo.Default, behaviors: defaultBehaviors);

        var jsPath = Path.Combine(_rootDir, "themes", "test-default-beh", "assets", "behaviors.js");
        Assert.False(File.Exists(jsPath));
    }

    [Fact]
    public void WriteTo_DarkModeJs_ContainsLocalStorage()
    {
        var behaviors = new CloneBehaviors { DarkModeToggle = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-dark-js", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-dark-js", "assets", "behaviors.js"));
        Assert.Contains("localStorage", js, StringComparison.Ordinal);
        Assert.Contains("dark", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_HasModal_WritesModalPartialAndCss()
    {
        var behaviors = new CloneBehaviors { HasModal = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-modal", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var partialPath = Path.Combine(_rootDir, "themes", "test-modal", "layouts", "partials", "modal.html");
        Assert.True(File.Exists(partialPath));
        var html = File.ReadAllText(partialPath);
        Assert.Contains("modal-overlay", html, StringComparison.Ordinal);
        Assert.Contains("site-modal", html, StringComparison.Ordinal);
        Assert.Contains("site.modules.modal", html, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-modal", "assets", "style.css"));
        Assert.Contains(".modal-overlay", css, StringComparison.Ordinal);
        Assert.Contains(".modal-container", css, StringComparison.Ordinal);
        Assert.Contains(".modal-close", css, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-modal", "assets", "behaviors.js"));
        Assert.Contains("site-modal", js, StringComparison.Ordinal);
        Assert.Contains("data-modal-trigger", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_HasDropdown_WritesDropdownPartialAndCss()
    {
        var behaviors = new CloneBehaviors { HasDropdown = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-dropdown", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var partialPath = Path.Combine(_rootDir, "themes", "test-dropdown", "layouts", "partials", "dropdown.html");
        Assert.True(File.Exists(partialPath));
        var html = File.ReadAllText(partialPath);
        Assert.Contains("dropdown-menu", html, StringComparison.Ordinal);
        Assert.Contains("dropdown_items", html, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-dropdown", "assets", "style.css"));
        Assert.Contains(".dropdown-menu", css, StringComparison.Ordinal);
        Assert.Contains(".dropdown-trigger", css, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-dropdown", "assets", "behaviors.js"));
        Assert.Contains("dropdown-trigger", js, StringComparison.Ordinal);
        Assert.Contains(".dropdown.open", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_HasTabs_WritesTabsPartialAndCss()
    {
        var behaviors = new CloneBehaviors { HasTabs = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-tabs", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var partialPath = Path.Combine(_rootDir, "themes", "test-tabs", "layouts", "partials", "tabs.html");
        Assert.True(File.Exists(partialPath));
        var html = File.ReadAllText(partialPath);
        Assert.Contains("tab-nav", html, StringComparison.Ordinal);
        Assert.Contains("tab-panel", html, StringComparison.Ordinal);
        Assert.Contains("site.modules.tabs", html, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-tabs", "assets", "style.css"));
        Assert.Contains(".tab-nav", css, StringComparison.Ordinal);
        Assert.Contains(".tab-btn[aria-selected", css, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-tabs", "assets", "behaviors.js"));
        Assert.Contains("tab-nav", js, StringComparison.Ordinal);
        Assert.Contains("aria-controls", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_AllThreePartials_AllWritten()
    {
        var behaviors = new CloneBehaviors { HasModal = true, HasDropdown = true, HasTabs = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-all-parts", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-all-parts", "layouts", "partials", "modal.html")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-all-parts", "layouts", "partials", "dropdown.html")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-all-parts", "layouts", "partials", "tabs.html")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-all-parts", "assets", "behaviors.js")));
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

    [Fact]
    public void CloneBehaviors_FromJson_ParsesAllFlags()
    {
        var json = """{"stickyHeader":true,"cardHoverLift":true,"scrollShrinkNav":true,"darkModeToggle":true}""";
        var b = CloneBehaviors.FromJson(json);
        Assert.True(b.StickyHeader);
        Assert.True(b.CardHoverLift);
        Assert.True(b.ScrollShrinkNav);
        Assert.True(b.DarkModeToggle);
        Assert.False(b.MobileHamburger);
    }

    [Fact]
    public void CloneBehaviors_FromJson_Empty_AllFalse()
    {
        var b = CloneBehaviors.FromJson("{}");
        Assert.False(b.StickyHeader);
        Assert.False(b.CardHoverLift);
        Assert.False(b.HasAnyCssBehavior);
        Assert.False(b.HasAnyJsBehavior);
    }

    [Fact]
    public void CloneBehaviors_FromJson_Null_ReturnsDefault()
    {
        var b = CloneBehaviors.FromJson(null!);
        Assert.False(b.StickyHeader);
    }

    [Fact]
    public void CloneBehaviors_HasAnyCssBehavior_DetectsCorrectly()
    {
        Assert.True(new CloneBehaviors { StickyHeader = true }.HasAnyCssBehavior);
        Assert.True(new CloneBehaviors { CardHoverLift = true }.HasAnyCssBehavior);
        Assert.True(new CloneBehaviors { DarkModeToggle = true }.HasAnyCssBehavior);
        Assert.False(new CloneBehaviors { BackToTop = true }.HasAnyCssBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasAnyJsBehavior_DetectsCorrectly()
    {
        Assert.True(new CloneBehaviors { ScrollShrinkNav = true }.HasAnyJsBehavior);
        Assert.True(new CloneBehaviors { SmoothScroll = true }.HasAnyJsBehavior);
        Assert.False(new CloneBehaviors { StickyHeader = true }.HasAnyJsBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasModal_ParsesAndDetects()
    {
        var json = """{"hasModal":true,"hasDropdown":false}""";
        var b = CloneBehaviors.FromJson(json);
        Assert.True(b.HasModal);
        Assert.False(b.HasDropdown);
        Assert.True(b.HasExtraPartials);
        Assert.True(b.HasAnyCssBehavior);
        Assert.True(b.HasAnyJsBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasTabs_Parses()
    {
        var json = """{"hasTabs":true}""";
        var b = CloneBehaviors.FromJson(json);
        Assert.True(b.HasTabs);
        Assert.False(b.HasModal);
        Assert.False(b.HasDropdown);
        Assert.True(b.HasAnyCssBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasDropdown_HasExtraPartials()
    {
        Assert.True(new CloneBehaviors { HasDropdown = true }.HasExtraPartials);
        Assert.False(new CloneBehaviors { BackToTop = true }.HasExtraPartials);
    }
}
