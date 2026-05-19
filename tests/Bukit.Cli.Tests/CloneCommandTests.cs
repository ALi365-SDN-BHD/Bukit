using Bukit.Cli.Commands;
using Bukit.Cli.Cli.Binding;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
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
            Shadow = "0 2px 8px rgba(0,0,0,0.04)",
            CardShadow = "0 4px 12px rgba(0,0,0,0.1)",
            ModalShadow = "0 20px 60px rgba(0,0,0,0.2)",
            DropdownShadow = "0 4px 16px rgba(0,0,0,0.08)",
            NavPadding = "12px 32px",
            ContainerPadding = "48px 32px 80px",
            SectionGap = "48px",
            FontFamily = "Inter, sans-serif",
            CodeFontFamily = "Fira Code, monospace",
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

    [Fact]
    public void CloneTokens_FromJson_HoverFields_Parses()
    {
        var json = """{"hoverLift":"5px","hoverShadow":"0 5px 15px rgba(0,0,0,0.15)"}""";
        var t = CloneTokens.FromJson(json);
        Assert.Equal("5px", t.HoverLift);
        Assert.Equal("0 5px 15px rgba(0,0,0,0.15)", t.HoverShadow);
    }

    [Fact]
    public void CloneBehaviors_FromJson_ScrollThreshold_Parses()
    {
        var json = """{"scrollShrinkNav":true,"scrollThreshold":100}""";
        var b = CloneBehaviors.FromJson(json);
        Assert.True(b.ScrollShrinkNav);
        Assert.Equal(100, b.ScrollThreshold);
    }

    [Fact]
    public void CloneBehaviors_Default_ScrollThresholdIs60()
    {
        var b = new CloneBehaviors();
        Assert.Equal(60, b.ScrollThreshold);
    }

    [Fact]
    public void CloneBehaviors_FromJson_AnimationStyle_Parses()
    {
        var json = """{"animateOnScroll":true,"animationStyle":"slideUp"}""";
        var b = CloneBehaviors.FromJson(json);
        Assert.True(b.AnimateOnScroll);
        Assert.Equal("slideUp", b.AnimationStyle);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_SectionWithResponsive()
    {
        var json = """{"extraSections":[{"heading":"Grid","responsive":{"columnsDesktop":"repeat(3, 1fr)","columnsMobile":"1fr"}}]}""";
        var l = CloneLayoutInfo.FromJson(json);
        Assert.Single(l.ExtraSections);
        Assert.NotNull(l.ExtraSections[0].Responsive);
        Assert.Equal("repeat(3, 1fr)", l.ExtraSections[0].Responsive!.ColumnsDesktop);
        Assert.True(l.ExtraSections[0].HasResponsive);
    }

    [Fact]
    public void SectionResponsiveInfo_Defaults()
    {
        var r = new SectionResponsiveInfo();
        Assert.Null(r.ColumnsDesktop);
        Assert.Null(r.MaxWidthMobile);
    }

    [Fact]
    public void CloneBehaviors_FromJson_UseLenis_Parses()
    {
        var json = """{"useLenis":true}""";
        var b = CloneBehaviors.FromJson(json);
        Assert.True(b.UseLenis);
        Assert.True(b.HasAnyJsBehavior);
    }

    [Fact]
    public void CloneBehaviors_Default_UseLenisFalse()
    {
        var b = new CloneBehaviors();
        Assert.False(b.UseLenis);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_ParsesSectionStates()
    {
        var json = """{"extraSections":[{"heading":"Pricing","states":[{"label":"Monthly","contentHtml":"<p>$9/mo</p>"},{"label":"Annual","contentHtml":"<p>$90/yr</p>"}]}]}""";
        var l = CloneLayoutInfo.FromJson(json);
        Assert.Single(l.ExtraSections);
        Assert.Equal(2, l.ExtraSections[0].States.Count);
        Assert.Equal("Monthly", l.ExtraSections[0].States[0].Label);
        Assert.Equal("<p>$90/yr</p>", l.ExtraSections[0].States[1].ContentHtml);
        Assert.True(l.ExtraSections[0].HasStates);
    }

    [Fact]
    public void SectionInfo_NoStates_HasStatesFalse()
    {
        var s = new SectionInfo { Heading = "About" };
        Assert.False(s.HasStates);
    }

    [Fact]
    public void CloneIcon_AllFields_Parsed()
    {
        var icon = new CloneIcon { Name = "search", Svg = "<svg></svg>", Width = "24", Height = "24" };
        Assert.Equal("search", icon.Name);
        Assert.Equal("<svg></svg>", icon.Svg);
    }

    [Fact]
    public void CloneAsset_AllFields_Parsed()
    {
        var asset = new CloneAsset { Type = "logo", Src = "https://example.com/logo.png", Alt = "Logo" };
        Assert.Equal("logo", asset.Type);
        Assert.Equal("https://example.com/logo.png", asset.Src);
    }

    [Fact]
    public void CloneGenerationSummary_Defaults()
    {
        var summary = new CloneGenerationSummary();
        Assert.Equal(0, summary.FileCount);
        Assert.Equal(0, summary.BehaviorCount);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void ClonePageInfo_FromJson_ParsesSeoAndBody()
    {
        var json = """{"title":"Home","url":"https://example.com","bodyMarkdown":"# Home","seo":{"description":"SEO desc","image":"/og.png"}}""";

        var page = ClonePageInfo.FromJson(json);

        Assert.Equal("Home", page.Title);
        Assert.Equal("https://example.com", page.Url);
        Assert.Equal("# Home", page.BodyMarkdown);
        Assert.Equal("SEO desc", page.Seo?.Description);
        Assert.Equal("/og.png", page.Seo?.Image);
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_ParsesWrappedAndArrayForms()
    {
        var wrapped = """{"sections":[{"type":"hero","heading":"Welcome"}]}""";
        var array = """[{"type":"faq","title":"Questions"}]""";

        var wrappedSections = CloneSectionsDocument.FromJson(wrapped);
        var arraySections = CloneSectionsDocument.FromJson(array);

        Assert.Single(wrappedSections);
        Assert.Equal("hero", wrappedSections[0].Type);
        Assert.Single(arraySections);
        Assert.Equal("Questions", arraySections[0].Title);
    }
}

public sealed class CloneThemeGeneratorStateTests : IDisposable
{
    private readonly string _rootDir;

    public CloneThemeGeneratorStateTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-clone-state-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public void WriteTo_WithStateSection_GeneratesStateTabs()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            ExtraSections =
            [
                new SectionInfo
                {
                    Heading = "Pricing",
                    States =
                    [
                        new() { Label = "Monthly", ContentHtml = "<p>$9/mo</p>" },
                        new() { Label = "Annual", ContentHtml = "<p>$90/yr</p>" }
                    ]
                }
            ]
        };

        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-state", tokens, layout);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-state", "layouts", "pages", "index.html"));
        Assert.Contains("state-tabs", html, StringComparison.Ordinal);
        Assert.Contains("state-tab", html, StringComparison.Ordinal);
        Assert.Contains("state-panel", html, StringComparison.Ordinal);
        Assert.Contains("$9/mo", html, StringComparison.Ordinal);
        Assert.Contains("aria-selected=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls", html, StringComparison.Ordinal);
        Assert.Contains("<script>(function()", html, StringComparison.Ordinal);
        Assert.Equal(1, summary.SectionCount);
    }

    [Fact]
    public void WriteTo_WithStateSection_SingleState_EmitsWarning()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            ExtraSections =
            [
                new SectionInfo
                {
                    Heading = "BadSection",
                    States = [new() { Label = "Only", ContentHtml = "<p>one</p>" }]
                }
            ]
        };

        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-warn", tokens, layout);

        Assert.Contains(summary.Warnings, w => w.Contains("needs at least 2 states"));
    }

    [Fact]
    public void WriteTo_HasCTASection_GeneratesCtaSection()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo { HasCTASection = true };

        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-cta", tokens, layout);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-cta", "layouts", "pages", "index.html"));
        Assert.Contains("call_to_action", html, StringComparison.Ordinal);
        Assert.Contains("cta-section", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_StaticSection_GeneratesStaticSection()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            ExtraSections =
            [
                new SectionInfo
                {
                    Heading = "About",
                    ContentHtml = "<p>We build things.</p>",
                    ImageUrls = ["/img/a.png"]
                }
            ]
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-static", tokens, layout);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-static", "layouts", "pages", "index.html"));
        Assert.Contains("We build things.", html, StringComparison.Ordinal);
        Assert.Contains("/img/a.png", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithIcons_WritesIconFiles()
    {
        var icons = new List<CloneIcon>
        {
            new() { Name = "search", Svg = "<svg viewBox=\"0 0 24 24\"><circle cx=\"11\" cy=\"11\" r=\"7\"/></svg>" },
            new() { Name = "arrow-right", Svg = "<svg><path d=\"M5 12h14\"/></svg>" }
        };

        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-icons", new CloneTokens(), CloneLayoutInfo.Default, icons: icons);

        Assert.Equal(2, summary.IconCount);
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-icons", "assets", "icons", "search.svg")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-icons", "assets", "icons", "arrow-right.svg")));

        var svg = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-icons", "assets", "icons", "search.svg"));
        Assert.Contains("viewBox", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithIcons_SanitizesNames()
    {
        var icons = new List<CloneIcon>
        {
            new() { Name = "search / magnify?", Svg = "<svg></svg>" }
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-sanitize", new CloneTokens(), CloneLayoutInfo.Default, icons: icons);

        Assert.False(File.Exists(Path.Combine(_rootDir, "themes", "test-sanitize", "assets", "icons", "search / magnify?.svg")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-sanitize", "assets", "icons", "search___magnify_.svg")));
    }

    [Fact]
    public void WriteTo_WithIcons_SkipsEmptySvg()
    {
        var icons = new List<CloneIcon>
        {
            new() { Name = "empty", Svg = "" },
            new() { Name = "good", Svg = "<svg></svg>" }
        };

        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-skip", new CloneTokens(), CloneLayoutInfo.Default, icons: icons);

        Assert.Equal(1, summary.IconCount);
        Assert.False(File.Exists(Path.Combine(_rootDir, "themes", "test-skip", "assets", "icons", "empty.svg")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "test-skip", "assets", "icons", "good.svg")));
    }

    [Fact]
    public void WriteTo_WithAssets_CreatesAssetDir()
    {
        var assets = new List<CloneAsset>
        {
            new() { Type = "logo", Src = "https://example.com/logo.png" }
        };

        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-assets", new CloneTokens(), CloneLayoutInfo.Default, assets: assets);

        Assert.Equal(1, summary.AssetCount);
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "themes", "test-assets", "assets", "images")));
    }

    [Fact]
    public void WriteTo_Summary_CountsAllFiles()
    {
        var behaviors = new CloneBehaviors { HasModal = true, StickyHeader = true };
        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-summary", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        Assert.True(summary.FileCount >= 17);
        Assert.Equal(2, summary.BehaviorCount);
    }

    [Fact]
    public void WriteTo_BehaviorsCss_IncludesStateSectionStyles()
    {
        CloneThemeGenerator.WriteTo(_rootDir, "test-state-css", new CloneTokens(), CloneLayoutInfo.Default);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-state-css", "assets", "style.css"));
        Assert.Contains(".state-tabs", css, StringComparison.Ordinal);
        Assert.Contains(".state-tab[aria-selected", css, StringComparison.Ordinal);
        Assert.Contains(".state-panel.hidden", css, StringComparison.Ordinal);
        Assert.Contains(".cta-section", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_HoverLift_FromTokens_PreciseValues()
    {
        var tokens = new CloneTokens { Primary = "#ff0000", HoverLift = "6px", HoverShadow = "0 8px 30px rgba(0,0,0,0.2)" };
        var behaviors = new CloneBehaviors { CardHoverLift = true };

        CloneThemeGenerator.WriteTo(_rootDir, "test-hover-precise", tokens, CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-hover-precise", "assets", "style.css"));
        Assert.Contains("translateY(-6px)", css, StringComparison.Ordinal);
        Assert.Contains("0 8px 30px rgba(0,0,0,0.2)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_AnimationStyle_SlideUp_GeneratesSlideKeyframes()
    {
        var behaviors = new CloneBehaviors { AnimateOnScroll = true, AnimationStyle = "slideUp" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-slide", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-slide", "assets", "style.css"));
        Assert.Contains("@keyframes slideUp", css, StringComparison.Ordinal);
        Assert.Contains("animation: slideUp", css, StringComparison.Ordinal);
        Assert.DoesNotContain("@keyframes fadeInUp", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_AnimationStyle_ScaleIn_GeneratesScaleKeyframes()
    {
        var behaviors = new CloneBehaviors { AnimateOnScroll = true, AnimationStyle = "scaleIn" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-scale", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-scale", "assets", "style.css"));
        Assert.Contains("@keyframes scaleIn", css, StringComparison.Ordinal);
        Assert.Contains("animation: scaleIn", css, StringComparison.Ordinal);
        Assert.Contains("transform: scale(0.92)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_AnimationStyle_FadeIn_GeneratesFadeOnly()
    {
        var behaviors = new CloneBehaviors { AnimateOnScroll = true, AnimationStyle = "fadeIn" };
        CloneThemeGenerator.WriteTo(_rootDir, "test-fade", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-fade", "assets", "style.css"));
        Assert.Contains("@keyframes fadeIn", css, StringComparison.Ordinal);
        Assert.Contains("animation: fadeIn", css, StringComparison.Ordinal);
        Assert.Contains("transform: translateY(0)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_AnimationStyle_Default_FadeInUp()
    {
        var behaviors = new CloneBehaviors { AnimateOnScroll = true, AnimationStyle = null };
        CloneThemeGenerator.WriteTo(_rootDir, "test-default-anim", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-default-anim", "assets", "style.css"));
        Assert.Contains("@keyframes fadeInUp", css, StringComparison.Ordinal);
        Assert.Contains("animation: fadeInUp", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_ScrollThreshold_CustomValue_InJs()
    {
        var behaviors = new CloneBehaviors { ScrollShrinkNav = true, ScrollThreshold = 120 };
        CloneThemeGenerator.WriteTo(_rootDir, "test-threshold", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-threshold", "assets", "behaviors.js"));
        Assert.Contains("n>120", js, StringComparison.Ordinal);
        Assert.Contains("n<20", js, StringComparison.Ordinal);
        Assert.DoesNotContain("n>60", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_ScrollThreshold_Default_Uses60()
    {
        var behaviors = new CloneBehaviors { ScrollShrinkNav = true, ScrollThreshold = 0 };
        CloneThemeGenerator.WriteTo(_rootDir, "test-thresh-default", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-thresh-default", "assets", "behaviors.js"));
        Assert.Contains("n>60", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_SectionResponsive_GeneratesPerSectionCss()
    {
        var tokens = new CloneTokens { Primary = "#ff0000" };
        var layout = new CloneLayoutInfo
        {
            ExtraSections =
            [
                new SectionInfo
                {
                    Heading = "Grid Section",
                    Responsive = new SectionResponsiveInfo
                    {
                        ColumnsDesktop = "repeat(3, 1fr)",
                        ColumnsTablet = "repeat(2, 1fr)",
                        ColumnsMobile = "1fr"
                    }
                }
            ]
        };

        CloneThemeGenerator.WriteTo(_rootDir, "test-resp", tokens, layout);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-resp", "layouts", "pages", "index.html"));
        Assert.Contains("grid-template-columns: repeat(3, 1fr)", html, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(2, 1fr)", html, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 1fr", html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: var(--bp-tablet))", html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: var(--bp-mobile))", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_UseLenis_InjectsLenisCdnAndInit()
    {
        var behaviors = new CloneBehaviors { UseLenis = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-lenis", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-lenis", "layouts", "layouts", "base.html"));
        Assert.Contains("lenis.min.js", html, StringComparison.Ordinal);
        Assert.Contains("behaviors.js", html, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-lenis", "assets", "behaviors.js"));
        Assert.Contains("new Lenis(", js, StringComparison.Ordinal);
        Assert.Contains("requestAnimationFrame(raf)", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_NoLenis_NoLenisCdn()
    {
        var behaviors = new CloneBehaviors { ScrollShrinkNav = true };
        CloneThemeGenerator.WriteTo(_rootDir, "test-no-lenis", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);

        var html = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-no-lenis", "layouts", "layouts", "base.html"));
        Assert.DoesNotContain("lenis.min.js", html, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(_rootDir, "themes", "test-no-lenis", "assets", "behaviors.js"));
        Assert.DoesNotContain("new Lenis(", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_UseLenis_CountsAsBehavior()
    {
        var behaviors = new CloneBehaviors { UseLenis = true };
        var summary = CloneThemeGenerator.WriteTo(_rootDir, "test-lenis-count", new CloneTokens(), CloneLayoutInfo.Default, behaviors: behaviors);
        Assert.Equal(1, summary.BehaviorCount);
        Assert.True(summary.FileCount >= 16);
    }
}

public sealed class CloneContentWriterTests : IDisposable
{
    private readonly string _rootDir;

    public CloneContentWriterTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-clone-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), """
site:
  name: clone
  title: Clone
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
theme:
  layouts: layouts
  assets: assets
  static: static
""");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public void WriteTo_StructuredClone_GeneratesContentDataThemeAndSourcesConfig()
    {
        var sections = new[]
        {
            new CloneSectionInfo
            {
                Id = "main-hero",
                Type = "hero",
                Heading = "Launch faster",
                Subheading = "Real content",
                ContentHtml = "<p>Original hero copy</p><img src=\"https://cdn.test/hero.png\" />",
                Styles = new Dictionary<string, string> { ["padding"] = "64px 0" },
                ComputedStyles = new Dictionary<string, string> { ["font-size"] = "72px" },
                Bounds = new CloneBox { X = 0, Y = 0, Width = 1440, Height = 640 },
                Components =
                [
                    new CloneComponentInfo
                    {
                        Type = "button",
                        Selector = ".hero a",
                        Text = "Start",
                        ComputedStyles = new Dictionary<string, string> { ["border-radius"] = "999px" }
                    }
                ],
                Interactions =
                [
                    new CloneInteractionInfo { Type = "click", Trigger = "click", Target = ".hero a", Description = "Primary CTA" }
                ]
            },
            new CloneSectionInfo
            {
                Type = "features",
                Title = "Features",
                Items =
                [
                    new() { Title = "Fast", Description = "Ships quickly" },
                    new() { Title = "Stable", Description = "Keeps content editable" }
                ]
            }
        };
        var assets = new[]
        {
            new CloneAsset { Type = "hero", Src = "https://cdn.test/hero.png" }
        };

        var summary = CloneContentWriter.WriteTo(
            _rootDir,
            "target-clone",
            new CloneTokens { Primary = "#123456" },
            new ClonePageInfo { Title = "Target", Url = "https://target.test" },
            sections,
            assets,
            new CloneBehaviors { MobileHamburger = true },
            "Target");

        Assert.Equal(1, summary.ContentFileCount);
        Assert.Equal(3, summary.DataFileCount);
        Assert.Equal(2, summary.SectionCount);
        Assert.True(summary.ConfigUpdated);

        var content = File.ReadAllText(Path.Combine(_rootDir, "content", "index.md"));
        Assert.Contains("source_url: 'https://target.test'", content, StringComparison.Ordinal);

        var heroData = File.ReadAllText(Path.Combine(_rootDir, "data", "main-hero.md"));
        Assert.Contains("type: 'hero'", heroData, StringComparison.Ordinal);
        Assert.Contains("clone_key: 'main-hero'", heroData, StringComparison.Ordinal);
        Assert.Contains("/assets/images/hero.png", heroData, StringComparison.Ordinal);
        Assert.Contains("content_html: |-", heroData, StringComparison.Ordinal);
        Assert.Contains("components_json:", heroData, StringComparison.Ordinal);
        Assert.Contains("computed_styles_json:", heroData, StringComparison.Ordinal);
        Assert.Contains("bounds_json:", heroData, StringComparison.Ordinal);
        Assert.Contains("interactions_json:", heroData, StringComparison.Ordinal);

        var assetData = File.ReadAllText(Path.Combine(_rootDir, "data", "clone-assets.md"));
        Assert.Contains("assets_json:", assetData, StringComparison.Ordinal);

        var indexTemplate = File.ReadAllText(Path.Combine(_rootDir, "themes", "target-clone", "layouts", "pages", "index.html"));
        Assert.Contains("site.modules.hero", indexTemplate, StringComparison.Ordinal);
        Assert.Contains("site.modules.features", indexTemplate, StringComparison.Ordinal);
        Assert.True(indexTemplate.IndexOf("main-hero", StringComparison.Ordinal) < indexTemplate.IndexOf("clone-002-features", StringComparison.Ordinal));

        var partial = File.ReadAllText(Path.Combine(_rootDir, "themes", "target-clone", "layouts", "partials", "clone-section.html"));
        Assert.Contains("section.fields.content_html.value", partial, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(_rootDir, "themes", "target-clone", "assets", "style.css"));
        Assert.Contains(".clone-section-001", css, StringComparison.Ordinal);
        Assert.Contains("padding: 64px 0;", css, StringComparison.Ordinal);

        var config = File.ReadAllText(Path.Combine(_rootDir, "site.yaml"));
        Assert.Contains("provider: sources", config, StringComparison.Ordinal);
        Assert.Contains("name: modules", config, StringComparison.Ordinal);
        Assert.Contains("dir: data", config, StringComparison.Ordinal);
        Assert.Contains("name: target-clone", config, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(_rootDir, "docs", "research", "DESIGN_TOKENS.md")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "docs", "research", "PAGE_TOPOLOGY.md")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "docs", "research", "BEHAVIORS.md")));
        var spec = File.ReadAllText(Path.Combine(_rootDir, "docs", "research", "components", "001-main-hero.spec.md"));
        Assert.Contains("Computed Styles", spec, StringComparison.Ordinal);
        Assert.Contains("Primary CTA", spec, StringComparison.Ordinal);
    }
}

public sealed class CloneCommandStructuredTests : IDisposable
{
    private readonly string _rootDir;

    public CloneCommandStructuredTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-clone-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), """
site:
  name: clone
  title: Clone
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
theme:
  layouts: layouts
  assets: assets
  static: static
""");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_WithPageAndSections_GeneratesStructuredClone()
    {
        var tokensPath = Path.Combine(_rootDir, "tokens.json");
        var pagePath = Path.Combine(_rootDir, "page.json");
        var sectionsPath = Path.Combine(_rootDir, "sections.json");
        var assetsPath = Path.Combine(_rootDir, "assets.json");

        File.WriteAllText(tokensPath, """{"primary":"#0055ff","fontFamily":"Inter, sans-serif"}""");
        File.WriteAllText(pagePath, """{"title":"Acme","url":"https://acme.test","summary":"Landing page"}""");
        File.WriteAllText(sectionsPath, """
{
  "sections": [
    {
      "type": "hero",
      "heading": "Acme launch",
      "contentHtml": "<p>Build with Acme</p>",
      "buttons": [{ "label": "Start", "url": "/start" }]
    }
  ]
}
""");
        File.WriteAllText(assetsPath, """[]""");

        var result = await CloneCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = Path.Combine(_rootDir, "site.yaml"),
                ["--tokens"] = tokensPath,
                ["--page"] = pagePath,
                ["--sections"] = sectionsPath,
                ["--assets"] = assetsPath,
                ["--theme"] = "acme-clone",
                ["--verify"] = "true"
            },
            Array.Empty<string>()));

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "acme-clone", "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "content", "index.md")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "data", "clone-001-hero.md")));

        var data = File.ReadAllText(Path.Combine(_rootDir, "data", "clone-001-hero.md"));
        Assert.Contains("type: 'hero'", data, StringComparison.Ordinal);
        Assert.Contains("Build with Acme", data, StringComparison.Ordinal);

        var config = File.ReadAllText(Path.Combine(_rootDir, "site.yaml"));
        Assert.Contains("provider: sources", config, StringComparison.Ordinal);
        Assert.Contains("name: acme-clone", config, StringComparison.Ordinal);

        var verifyReport = File.ReadAllText(Path.Combine(_rootDir, "docs", "research", "VERIFY_REPORT.md"));
        Assert.Contains("Build: `passed`", verifyReport, StringComparison.Ordinal);
        Assert.Contains("Screenshot Comparisons", verifyReport, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_FailOnVisualDiff_ReturnsNonZeroWhenThresholdExceeded()
    {
        var tokensPath = Path.Combine(_rootDir, "tokens.json");
        var pagePath = Path.Combine(_rootDir, "page.json");
        var sectionsPath = Path.Combine(_rootDir, "sections.json");
        var assetsPath = Path.Combine(_rootDir, "assets.json");

        File.WriteAllText(tokensPath, """{"primary":"#0055ff"}""");
        File.WriteAllText(pagePath, """{"title":"Acme"}""");
        File.WriteAllText(sectionsPath, """{"sections":[{"id":"hero","type":"hero","heading":"Acme","bounds":{"x":0,"y":0,"width":2,"height":1}}]}""");
        File.WriteAllText(assetsPath, """[]""");

        var targetDir = Path.Combine(_rootDir, "docs", "design-references");
        var localDir = Path.Combine(_rootDir, "docs", "research", "local-screenshots");
        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(localDir);
        CloneScreenshotDiffTests.WritePngForTest(Path.Combine(targetDir, "target-1440.png"), 2, 1, [255, 0, 0, 255, 0, 255, 0, 255]);
        CloneScreenshotDiffTests.WritePngForTest(Path.Combine(localDir, "local-1440.png"), 2, 1, [255, 0, 0, 255, 0, 0, 255, 255]);

        var result = await CloneCommand.RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = Path.Combine(_rootDir, "site.yaml"),
                ["--tokens"] = tokensPath,
                ["--page"] = pagePath,
                ["--sections"] = sectionsPath,
                ["--assets"] = assetsPath,
                ["--theme"] = "visual-clone",
                ["--verify"] = "true",
                ["--fail-on-visual-diff"] = "true",
                ["--visual-threshold"] = "0.01"
            },
            Array.Empty<string>()));

        Assert.Equal(1, result);
        var report = File.ReadAllText(Path.Combine(_rootDir, "docs", "research", "VERIFY_REPORT.md"));
        Assert.Contains("target-1440.png: `fail`", report, StringComparison.Ordinal);
        Assert.Contains("diff=50.00", report, StringComparison.Ordinal);
        Assert.Contains("bbox=1,0-1,0", report, StringComparison.Ordinal);
        Assert.Contains("Likely Affected Sections", report, StringComparison.Ordinal);
        Assert.Contains("section 1: `hero`", report, StringComparison.Ordinal);
        Assert.Contains("data: `data/hero.md`", report, StringComparison.Ordinal);
        Assert.Contains("spec: `docs/research/components/001-hero.spec.md`", report, StringComparison.Ordinal);
        Assert.Contains("Visual threshold exceeded", report, StringComparison.Ordinal);
        Assert.Contains("768: target=`missing`", report, StringComparison.Ordinal);

        var jsonReport = File.ReadAllText(Path.Combine(_rootDir, "docs", "research", "VERIFY_REPORT.json"));
        Assert.Contains("\"buildPassed\": true", jsonReport, StringComparison.Ordinal);
        Assert.Contains("\"failedComparisons\": 1", jsonReport, StringComparison.Ordinal);
        Assert.Contains("\"sectionKey\": \"hero\"", jsonReport, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"dataPath\": \"data/hero.md\"", jsonReport, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"specPath\": \"docs/research/components/001-hero.spec.md\"", jsonReport, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class CloneScreenshotDiffTests : IDisposable
{
    private readonly string _rootDir;

    public CloneScreenshotDiffTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-clone-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public void ComparePngScreenshots_ReportsPixelMismatchRatio()
    {
        var target = Path.Combine(_rootDir, "target.png");
        var local = Path.Combine(_rootDir, "local.png");
        WritePngForTest(target, 2, 1, [255, 0, 0, 255, 0, 255, 0, 255]);
        WritePngForTest(local, 2, 1, [255, 0, 0, 255, 0, 0, 255, 255]);

        var result = CloneCommand.ComparePngScreenshots("target.png", target, local);

        Assert.Equal("pixel-different", result.Status);
        Assert.Equal(2, result.ComparedPixels);
        Assert.Equal(1, result.MismatchedPixels);
        Assert.Equal(0.5, result.DiffRatio);
        Assert.Equal(1, result.MismatchMinX);
        Assert.Equal(0, result.MismatchMinY);
        Assert.Equal(1, result.MismatchMaxX);
        Assert.Equal(0, result.MismatchMaxY);
    }

    internal static void WritePngForTest(string path, int width, int height, byte[] rgba)
    {
        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(file, "IHDR", ihdr);

        using var raw = new MemoryStream();
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * stride, stride);
        }
        raw.Position = 0;
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Compress, leaveOpen: true))
            raw.CopyTo(zlib);
        WriteChunk(file, "IDAT", compressed.ToArray());
        WriteChunk(file, "IEND", []);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write([0, 0, 0, 0]);
    }
}
