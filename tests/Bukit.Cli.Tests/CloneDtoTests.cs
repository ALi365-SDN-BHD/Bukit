using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneDtoTests
{
    [Fact]
    public void CloneComponentInfo_Defaults()
    {
        var info = new CloneComponentInfo();
        Assert.Null(info.Id);
        Assert.Null(info.Type);
        Assert.Null(info.Selector);
        Assert.Null(info.Text);
        Assert.Null(info.Html);
        Assert.Null(info.Bounds);
        Assert.Null(info.ComputedStyles);
        Assert.Empty(info.States);
        Assert.Empty(info.Interactions);
    }

    [Fact]
    public void CloneComponentInfo_WithValues()
    {
        var bounds = new CloneBox { X = 10, Y = 20, Width = 100, Height = 200 };
        var state = new SectionState { Label = "hover" };
        var interaction = new CloneInteractionInfo { Type = "click", Trigger = "onClick" };
        var info = new CloneComponentInfo
        {
            Id = "hero-1",
            Type = "hero",
            Selector = ".hero",
            Text = "Hello",
            Html = "<div>Hello</div>",
            Bounds = bounds,
            ComputedStyles = new Dictionary<string, string> { ["color"] = "red" },
            States = [state],
            Interactions = [interaction]
        };

        Assert.Equal("hero-1", info.Id);
        Assert.Equal("hero", info.Type);
        Assert.Equal(".hero", info.Selector);
        Assert.Equal("Hello", info.Text);
        Assert.Equal("<div>Hello</div>", info.Html);
        Assert.Same(bounds, info.Bounds);
        Assert.Equal("red", info.ComputedStyles!["color"]);
        Assert.Single(info.States);
        Assert.Single(info.Interactions);
    }

    [Fact]
    public void CloneInteractionInfo_Defaults()
    {
        var info = new CloneInteractionInfo();
        Assert.Null(info.Type);
        Assert.Null(info.Trigger);
        Assert.Null(info.Target);
        Assert.Null(info.Description);
        Assert.Null(info.States);
    }

    [Fact]
    public void CloneInteractionInfo_WithValues()
    {
        var info = new CloneInteractionInfo
        {
            Type = "click",
            Trigger = "onClick",
            Target = "#btn",
            Description = "Button click",
            States = new Dictionary<string, string> { ["active"] = "true" }
        };

        Assert.Equal("click", info.Type);
        Assert.Equal("#btn", info.Target);
        Assert.Equal("true", info.States!["active"]);
    }

    [Fact]
    public void CloneSectionAsset_Defaults()
    {
        var asset = new CloneSectionAsset();
        Assert.Equal("content", asset.Type);
        Assert.Equal("", asset.Src);
        Assert.Null(asset.Alt);
        Assert.Null(asset.LocalPath);
        Assert.Null(asset.Media);
        Assert.Null(asset.Width);
        Assert.Null(asset.Height);
    }

    [Fact]
    public void CloneSectionAsset_WithValues()
    {
        var asset = new CloneSectionAsset
        {
            Type = "image",
            Src = "hero.jpg",
            Alt = "Hero",
            LocalPath = "assets/hero.jpg",
            Media = "desktop",
            Width = "800",
            Height = "600"
        };

        Assert.Equal("image", asset.Type);
        Assert.Equal("hero.jpg", asset.Src);
        Assert.Equal("Hero", asset.Alt);
        Assert.Equal("assets/hero.jpg", asset.LocalPath);
        Assert.Equal("desktop", asset.Media);
        Assert.Equal("800", asset.Width);
        Assert.Equal("600", asset.Height);
    }

    [Fact]
    public void CloneBox_Defaults()
    {
        var box = new CloneBox();
        Assert.Null(box.X);
        Assert.Null(box.Y);
        Assert.Null(box.Width);
        Assert.Null(box.Height);
    }

    [Fact]
    public void CloneBox_WithValues()
    {
        var box = new CloneBox { X = 0, Y = 0, Width = 1920, Height = 1080 };
        Assert.Equal(0, box.X);
        Assert.Equal(0, box.Y);
        Assert.Equal(1920, box.Width);
        Assert.Equal(1080, box.Height);
    }

    [Fact]
    public void CloneContentWriteResult_Creation()
    {
        var result = new CloneContentWriteResult(5, 3, 2, 10, true, ["Warning msg"]);

        Assert.Equal(5, result.ThemeFileCount);
        Assert.Equal(3, result.ContentFileCount);
        Assert.Equal(2, result.DataFileCount);
        Assert.Equal(10, result.SectionCount);
        Assert.True(result.ConfigUpdated);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void CloneContentWriteResult_Defaults()
    {
        var result = new CloneContentWriteResult(0, 0, 0, 0, false, []);

        Assert.Equal(0, result.ThemeFileCount);
        Assert.False(result.ConfigUpdated);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void CloneAssetManifestEntry_Creation()
    {
        var entry = new CloneAssetManifestEntry("image", "hero.jpg", "Hero", "desktop", "800", "600", "local/hero.jpg", "sha256-abc", null);

        Assert.Equal("image", entry.Type);
        Assert.Equal("hero.jpg", entry.Src);
        Assert.Equal("Hero", entry.Alt);
        Assert.Equal("desktop", entry.Media);
        Assert.Equal("800", entry.Width);
        Assert.Equal("600", entry.Height);
        Assert.Equal("local/hero.jpg", entry.LocalPath);
        Assert.Equal("sha256-abc", entry.Integrity);
        Assert.Null(entry.Failure);
    }

    [Fact]
    public void CloneTokens_Defaults()
    {
        var tokens = new CloneTokens();
        Assert.Null(tokens.Bg);
        Assert.Null(tokens.Surface);
        Assert.Null(tokens.Text);
        Assert.Null(tokens.Primary);
        Assert.Null(tokens.Accent);
        Assert.Null(tokens.Radius);
        Assert.Null(tokens.ContentMax);
        Assert.Null(tokens.Shadow);
        Assert.Null(tokens.FontFamily);
        Assert.Null(tokens.HeadingFontFamily);
        Assert.Null(tokens.GoogleFontsUrl);
    }

    [Fact]
    public void CloneTokens_WithValues()
    {
        var tokens = new CloneTokens
        {
            Bg = "#ffffff",
            Surface = "#f0f0f0",
            Text = "#333333",
            Primary = "#0066cc",
            Accent = "#ff6600",
            Radius = "8px",
            ContentMax = "1200px",
            Shadow = "0 2px 4px rgba(0,0,0,0.1)",
            FontFamily = "Inter, sans-serif",
            HeadingFontFamily = "Georgia, serif",
            SpacingScale = new SpacingScale { Xs = "4px", Md = "16px", Xl = "48px" },
            ResponsiveBreakpoints = new ResponsiveBreakpoints { Mobile = "640px", Desktop = "1024px" }
        };

        Assert.Equal("#ffffff", tokens.Bg);
        Assert.Equal("#f0f0f0", tokens.Surface);
        Assert.Equal("#333333", tokens.Text);
        Assert.Equal("#0066cc", tokens.Primary);
        Assert.Equal("8px", tokens.Radius);
        Assert.Equal("1200px", tokens.ContentMax);
        Assert.Equal("Inter, sans-serif", tokens.FontFamily);
        Assert.NotNull(tokens.SpacingScale);
        Assert.Equal("16px", tokens.SpacingScale!.Md);
        Assert.NotNull(tokens.ResponsiveBreakpoints);
        Assert.Equal("640px", tokens.ResponsiveBreakpoints!.Mobile);
    }

    [Fact]
    public void CloneTokens_FromJson_Valid()
    {
        var result = CloneTokens.FromJson("""{"bg":"#fff","text":"#000"}""").tokens;
        Assert.Equal("#fff", result.Bg);
        Assert.Equal("#000", result.Text);
    }

    [Fact]
    public void CloneTokens_FromJson_Empty_ReturnsNew()
    {
        var result = CloneTokens.FromJson("").tokens;
        Assert.NotNull(result);
    }

    [Fact]
    public void CloneTokens_FromJson_Invalid_ReturnsNew()
    {
        var result = CloneTokens.FromJson("{invalid}").tokens;
        Assert.NotNull(result);
    }

    [Fact]
    public void CloneTokens_FromJson_Null_ReturnsNew()
    {
        var result = CloneTokens.FromJson(null!).tokens;
        Assert.NotNull(result);
    }

    [Fact]
    public void CloneLayoutInfo_Default_ReturnsNewInstance()
    {
        var info = CloneLayoutInfo.Default;
        Assert.NotNull(info);
        Assert.Null(info.SiteTitle);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_Empty_ReturnsDefault()
    {
        var info = CloneLayoutInfo.FromJson("");
        Assert.NotNull(info);
        Assert.False(info.HasFeaturesSection);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_Valid_ReturnsParsed()
    {
        var info = CloneLayoutInfo.FromJson("""{"siteTitle":"My Site","hasFeaturesSection":true}""");
        Assert.Equal("My Site", info.SiteTitle);
        Assert.True(info.HasFeaturesSection);
    }

    [Fact]
    public void ClonePageInfo_Default_ReturnsNewInstance()
    {
        var info = ClonePageInfo.Default;
        Assert.NotNull(info);
        Assert.Null(info.Title);
    }

    [Fact]
    public void ClonePageInfo_FromJson_Empty_ReturnsDefault()
    {
        var info = ClonePageInfo.FromJson("");
        Assert.NotNull(info);
    }

    [Fact]
    public void ClonePageInfo_FromJson_Valid_ReturnsParsed()
    {
        var info = ClonePageInfo.FromJson("""{"title":"About","slug":"about"}""");
        Assert.Equal("About", info.Title);
        Assert.Equal("about", info.Slug);
    }

    [Fact]
    public void CloneGenerationSummary_Defaults()
    {
        var summary = new CloneGenerationSummary();
        Assert.Equal(0, summary.FileCount);
        Assert.Equal(0, summary.BehaviorCount);
        Assert.Equal(0, summary.IconCount);
        Assert.Equal(0, summary.AssetCount);
        Assert.Equal(0, summary.SectionCount);
        Assert.Equal(0, summary.ContentFileCount);
        Assert.Equal(0, summary.DataFileCount);
        Assert.False(summary.ConfigUpdated);
        Assert.False(summary.VerifyPassed);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void CloneGenerationSummary_WithValues()
    {
        var summary = new CloneGenerationSummary
        {
            FileCount = 10,
            SectionCount = 5,
            ContentFileCount = 3,
            ConfigUpdated = true,
            VerifyPassed = true,
            Warnings = ["missing image"]
        };

        Assert.Equal(10, summary.FileCount);
        Assert.Equal(5, summary.SectionCount);
        Assert.Equal(3, summary.ContentFileCount);
        Assert.True(summary.ConfigUpdated);
        Assert.True(summary.VerifyPassed);
        Assert.Single(summary.Warnings);
    }

    [Fact]
    public void SpacingScale_WithValues()
    {
        var scale = new SpacingScale { Xs = "4px", Sm = "8px", Md = "16px", Lg = "24px", Xl = "48px" };
        Assert.Equal("4px", scale.Xs);
        Assert.Equal("8px", scale.Sm);
        Assert.Equal("16px", scale.Md);
        Assert.Equal("24px", scale.Lg);
        Assert.Equal("48px", scale.Xl);
    }

    [Fact]
    public void ResponsiveBreakpoints_WithValues()
    {
        var bp = new ResponsiveBreakpoints { Mobile = "480px", Tablet = "768px", Desktop = "1024px" };
        Assert.Equal("480px", bp.Mobile);
        Assert.Equal("768px", bp.Tablet);
        Assert.Equal("1024px", bp.Desktop);
    }

    [Fact]
    public void ResponsiveBreakpoints_Defaults()
    {
        var bp = new ResponsiveBreakpoints();
        Assert.Null(bp.Mobile);
        Assert.Null(bp.Tablet);
        Assert.Null(bp.Desktop);
    }

    [Fact]
    public void CloneLayoutInfo_Defaults()
    {
        var info = new CloneLayoutInfo();
        Assert.Null(info.SiteTitle);
        Assert.Null(info.HeroHeading);
        Assert.Null(info.HeroSubtext);
        Assert.False(info.HasFeaturesSection);
        Assert.False(info.HasCTASection);
        Assert.False(info.HasHeroCta);
        Assert.Null(info.HeroCtaText);
        Assert.Null(info.HeroCtaUrl);
        Assert.Empty(info.NavLinks);
        Assert.Empty(info.FooterLinks);
        Assert.Empty(info.ExtraSections);
    }

    [Fact]
    public void ClonePageInfo_Defaults()
    {
        var info = new ClonePageInfo();
        Assert.Null(info.Title);
        Assert.Null(info.Slug);
        Assert.Null(info.Url);
        Assert.Null(info.Summary);
        Assert.Null(info.Description);
        Assert.Null(info.BodyMarkdown);
        Assert.Null(info.ContentHtml);
        Assert.Null(info.Seo);
        Assert.Empty(info.Screenshots);
    }

    [Fact]
    public void ClonePageSeo_AllProperties()
    {
        var seo = new ClonePageSeo
        {
            Title = "Test Page",
            Description = "A test page",
            Image = "https://example.com/img.jpg"
        };

        Assert.Equal("Test Page", seo.Title);
        Assert.Equal("A test page", seo.Description);
        Assert.Equal("https://example.com/img.jpg", seo.Image);
    }

    [Fact]
    public void ClonePageInfo_WithFullValues()
    {
        var seo = new ClonePageSeo { Title = "SEO", Description = "SEO desc" };
        var screenshots = new List<CloneViewportCapture> { new() { Width = 1920 } };
        var info = new ClonePageInfo
        {
            Title = "Page",
            Slug = "page",
            Url = "/page/",
            Summary = "Sum",
            Description = "Desc",
            BodyMarkdown = "# Hello",
            ContentHtml = "<h1>Hello</h1>",
            Seo = seo,
            Screenshots = screenshots
        };

        Assert.Equal("Page", info.Title);
        Assert.Equal("# Hello", info.BodyMarkdown);
        Assert.Same(seo, info.Seo);
        Assert.Single(info.Screenshots);
    }

    [Fact]
    public void CloneViewportCapture_AllProperties()
    {
        var capture = new CloneViewportCapture
        {
            Name = "hero",
            Width = 1920,
            Height = 1080,
            Screenshot = "hero.png"
        };

        Assert.Equal("hero", capture.Name);
        Assert.Equal(1920, capture.Width);
        Assert.Equal(1080, capture.Height);
        Assert.Equal("hero.png", capture.Screenshot);
    }

    [Fact]
    public void NavLinkInfo_Defaults()
    {
        var link = new NavLinkInfo();
        Assert.Null(link.Label);
        Assert.Null(link.Url);
    }

    [Fact]
    public void NavLinkInfo_WithValues()
    {
        var link = new NavLinkInfo { Label = "Home", Url = "/" };
        Assert.Equal("Home", link.Label);
        Assert.Equal("/", link.Url);
    }

    [Fact]
    public void FooterLinkInfo_Defaults()
    {
        var link = new FooterLinkInfo();
        Assert.Null(link.Label);
        Assert.Null(link.Url);
    }

    [Fact]
    public void FooterLinkInfo_WithValues()
    {
        var link = new FooterLinkInfo { Label = "About", Url = "/about" };
        Assert.Equal("About", link.Label);
        Assert.Equal("/about", link.Url);
    }

    [Fact]
    public void SectionInfo_Defaults()
    {
        var info = new SectionInfo();
        Assert.Equal("content", info.Semantic);
        Assert.Null(info.Heading);
        Assert.Null(info.ContentHtml);
        Assert.Empty(info.ImageUrls);
        Assert.Empty(info.States);
        Assert.Null(info.Responsive);
        Assert.False(info.HasStates);
        Assert.False(info.HasResponsive);
    }

    [Fact]
    public void SectionInfo_WithValues()
    {
        var info = new SectionInfo
        {
            Semantic = "hero",
            Heading = "Hero Title",
            ContentHtml = "<h1>Hero</h1>",
            ImageUrls = ["hero.jpg"],
            States = [new SectionState { Label = "hover" }],
            Responsive = new SectionResponsiveInfo { ColumnsDesktop = "3" }
        };

        Assert.Equal("hero", info.Semantic);
        Assert.Equal("Hero Title", info.Heading);
        Assert.Single(info.ImageUrls);
        Assert.Single(info.States);
        Assert.NotNull(info.Responsive);
        Assert.True(info.HasStates);
        Assert.True(info.HasResponsive);
    }

    [Fact]
    public void SectionState_Defaults()
    {
        var state = new SectionState();
        Assert.Null(state.Label);
        Assert.Null(state.ContentHtml);
        Assert.Null(state.Screenshot);
        Assert.Null(state.ComputedStyles);
    }

    [Fact]
    public void SectionState_WithValues()
    {
        var styles = new Dictionary<string, string> { ["color"] = "red" };
        var state = new SectionState
        {
            Label = "hover",
            ContentHtml = "<span>Hover</span>",
            Screenshot = "hover.png",
            ComputedStyles = styles
        };

        Assert.Equal("hover", state.Label);
        Assert.Equal("<span>Hover</span>", state.ContentHtml);
        Assert.Equal("hover.png", state.Screenshot);
        Assert.Same(styles, state.ComputedStyles);
    }

    [Fact]
    public void SectionResponsiveInfo_Defaults()
    {
        var info = new SectionResponsiveInfo();
        Assert.Null(info.ColumnsDesktop);
        Assert.Null(info.ColumnsTablet);
        Assert.Null(info.ColumnsMobile);
        Assert.Null(info.MaxWidthDesktop);
        Assert.Null(info.MaxWidthTablet);
        Assert.Null(info.MaxWidthMobile);
        Assert.Null(info.Viewports);
    }

    [Fact]
    public void SectionResponsiveInfo_WithValues()
    {
        var info = new SectionResponsiveInfo
        {
            ColumnsDesktop = "3",
            ColumnsTablet = "2",
            ColumnsMobile = "1",
            MaxWidthDesktop = "1200px",
            MaxWidthTablet = "768px",
            MaxWidthMobile = "480px",
            Viewports = new Dictionary<string, CloneViewportSectionInfo>
            {
                ["desktop"] = new CloneViewportSectionInfo { Screenshot = "desktop.png" }
            }
        };

        Assert.Equal("3", info.ColumnsDesktop);
        Assert.Equal("1200px", info.MaxWidthDesktop);
        Assert.NotNull(info.Viewports);
        Assert.Equal("desktop.png", info.Viewports!["desktop"].Screenshot);
    }

    [Fact]
    public void CloneAsset_Defaults()
    {
        var asset = new CloneAsset();
        Assert.Equal("content", asset.Type);
        Assert.Equal("", asset.Src);
        Assert.Null(asset.Alt);
        Assert.Null(asset.LocalPath);
        Assert.Null(asset.Media);
        Assert.Null(asset.Width);
        Assert.Null(asset.Height);
        Assert.Null(asset.Integrity);
        Assert.Null(asset.Failure);
    }

    [Fact]
    public void CloneAsset_WithValues()
    {
        var asset = new CloneAsset
        {
            Type = "image",
            Src = "hero.jpg",
            Alt = "Hero",
            LocalPath = "assets/hero.jpg",
            Media = "desktop",
            Width = "800",
            Height = "600",
            Integrity = "sha256-abc"
        };

        Assert.Equal("image", asset.Type);
        Assert.Equal("hero.jpg", asset.Src);
        Assert.Equal("Hero", asset.Alt);
        Assert.Equal("assets/hero.jpg", asset.LocalPath);
        Assert.Equal("desktop", asset.Media);
        Assert.Equal("800", asset.Width);
        Assert.Equal("600", asset.Height);
        Assert.Equal("sha256-abc", asset.Integrity);
    }

    [Fact]
    public void CloneIcon_Defaults()
    {
        var icon = new CloneIcon();
        Assert.Equal("icon", icon.Name);
        Assert.Equal("", icon.Svg);
        Assert.Null(icon.Width);
        Assert.Null(icon.Height);
    }

    [Fact]
    public void CloneIcon_WithValues()
    {
        var icon = new CloneIcon { Name = "menu", Svg = "<svg/>", Width = "24", Height = "24" };
        Assert.Equal("menu", icon.Name);
        Assert.Equal("<svg/>", icon.Svg);
        Assert.Equal("24", icon.Width);
        Assert.Equal("24", icon.Height);
    }

    [Fact]
    public void DeployContext_AllProperties()
    {
        var ctx = new DeployContext
        {
            OutputDir = "/dist",
            SiteUrl = "https://example.com",
            BaseUrl = "/",
            Branch = "gh-pages",
            Message = "Deploy site",
            Cname = "example.com",
            KeepHistory = true,
            Logger = new TestLogger()
        };

        Assert.Equal("/dist", ctx.OutputDir);
        Assert.Equal("https://example.com", ctx.SiteUrl);
        Assert.Equal("/", ctx.BaseUrl);
        Assert.Equal("gh-pages", ctx.Branch);
        Assert.Equal("Deploy site", ctx.Message);
        Assert.Equal("example.com", ctx.Cname);
        Assert.True(ctx.KeepHistory);
        Assert.NotNull(ctx.Logger);
    }

    [Fact]
    public void DeployResult_Success()
    {
        var result = new DeployResult { Success = true, DeployedUrl = "https://example.com" };
        Assert.True(result.Success);
        Assert.Equal("https://example.com", result.DeployedUrl);
        Assert.Null(result.Error);
    }

    [Fact]
    public void DeployResult_Failure()
    {
        var result = new DeployResult { Success = false, Error = "Auth failed" };
        Assert.False(result.Success);
        Assert.Equal("Auth failed", result.Error);
        Assert.Null(result.DeployedUrl);
    }

    private sealed class TestLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    [Fact]
    public void CloneBehaviors_Defaults()
    {
        var b = new CloneBehaviors();
        Assert.False(b.StickyHeader);
        Assert.False(b.CardHoverLift);
        Assert.False(b.AnimateOnScroll);
        Assert.False(b.ScrollShrinkNav);
        Assert.False(b.DarkModeToggle);
        Assert.False(b.MobileHamburger);
        Assert.False(b.SmoothScroll);
        Assert.False(b.BackToTop);
        Assert.False(b.HasModal);
        Assert.False(b.HasDropdown);
        Assert.False(b.HasTabs);
        Assert.Null(b.AnimationStyle);
        Assert.Equal(60, b.ScrollThreshold);
        Assert.False(b.UseLenis);
    }

    [Fact]
    public void CloneBehaviors_WithValues()
    {
        var b = new CloneBehaviors
        {
            StickyHeader = true,
            DarkModeToggle = true,
            SmoothScroll = true,
            BackToTop = true,
            AnimationStyle = "fade",
            ScrollThreshold = 100
        };

        Assert.True(b.StickyHeader);
        Assert.True(b.DarkModeToggle);
        Assert.True(b.SmoothScroll);
        Assert.True(b.BackToTop);
        Assert.Equal("fade", b.AnimationStyle);
        Assert.Equal(100, b.ScrollThreshold);
    }

    [Fact]
    public void CloneBehaviors_ComputedProperties()
    {
        var b = new CloneBehaviors { HasModal = true };
        Assert.True(b.HasExtraPartials);
        Assert.True(b.HasAnyCssBehavior);
        Assert.True(b.HasAnyJsBehavior);

        var b2 = new CloneBehaviors();
        Assert.False(b2.HasExtraPartials);
        Assert.False(b2.HasAnyCssBehavior);
        Assert.False(b2.HasAnyJsBehavior);
    }

    [Fact]
    public void CloneBehaviors_Default_ReturnsNewInstance()
    {
        Assert.NotNull(CloneBehaviors.Default);
        Assert.False(CloneBehaviors.Default.StickyHeader);
    }

    [Fact]
    public void CloneBehaviors_FromJson_Null_ReturnsDefault()
    {
        var result = CloneBehaviors.FromJson(null!);
        Assert.False(result.StickyHeader);
    }

    [Fact]
    public void CloneBehaviors_FromJson_Empty_ReturnsDefault()
    {
        var result = CloneBehaviors.FromJson("");
        Assert.False(result.StickyHeader);
    }

    [Fact]
    public void CloneBehaviors_FromJson_Valid_ReturnsParsed()
    {
        var result = CloneBehaviors.FromJson("""{"stickyHeader":true}""");
        Assert.True(result.StickyHeader);
    }

    [Fact]
    public void CloneBehaviors_FromJson_Invalid_ReturnsDefault()
    {
        var result = CloneBehaviors.FromJson("{invalid json}");
        Assert.False(result.StickyHeader);
    }

    [Fact]
    public void CloneVerifyReportJson_Creation()
    {
        var summary = new CloneVerifyReportSummary(10, 2, 1, 0);
        var report = new CloneVerifyReportJson(
            true, "/site.yaml", 0.05, false, summary, [], [], []);

        Assert.True(report.BuildPassed);
        Assert.Equal("/site.yaml", report.ConfigPath);
        Assert.Equal(0.05, report.VisualThreshold);
        Assert.False(report.Passed);
        Assert.Equal(10, report.Summary.Comparisons);
        Assert.Equal(2, report.Summary.FailedComparisons);
        Assert.Equal(1, report.Summary.MissingScreenshots);
        Assert.Empty(report.Comparisons);
        Assert.Empty(report.MissingScreenshots);
        Assert.Empty(report.AffectedSections);
    }

    [Fact]
    public void CloneVerifyReportSummary_Creation()
    {
        var s = new CloneVerifyReportSummary(10, 2, 1, 0);
        Assert.Equal(10, s.Comparisons);
        Assert.Equal(2, s.FailedComparisons);
        Assert.Equal(1, s.MissingScreenshots);
        Assert.Equal(0, s.AffectedSections);
    }

    [Fact]
    public void CloneVerifyScreenshotComparison_Creation()
    {
        var bounds = new CloneVerifyMismatchBounds(10, 20, 100, 200);
        var comp = new CloneVerifyScreenshotComparison(
            "hero", true, "pass", 1000, 50, 0.05, 1920, 1080, 1910, 1078, bounds);

        Assert.Equal("hero", comp.Name);
        Assert.True(comp.Passed);
        Assert.Equal("pass", comp.Status);
        Assert.Equal(1000, comp.ComparedPixels);
        Assert.Equal(50, comp.MismatchedPixels);
        Assert.Equal(0.05, comp.DiffRatio);
        Assert.Equal(1920, comp.TargetWidth);
        Assert.Equal(1080, comp.TargetHeight);
        Assert.Equal(1910, comp.LocalWidth);
        Assert.Equal(1078, comp.LocalHeight);
        Assert.NotNull(comp.MismatchBounds);
        Assert.Equal(10, comp.MismatchBounds.MinX);
        Assert.Equal(20, comp.MismatchBounds.MinY);
    }

    [Fact]
    public void CloneVerifyMismatchBounds_Creation()
    {
        var b = new CloneVerifyMismatchBounds(null, null, null, null);
        Assert.Null(b.MinX);
        Assert.Null(b.MinY);
        Assert.Null(b.MaxX);
        Assert.Null(b.MaxY);
    }

    [Fact]
    public void CloneVerifyMissingScreenshot_Creation()
    {
        var ms = new CloneVerifyMissingScreenshot("desktop", "target.png", "local.png", true, true);

        Assert.Equal("desktop", ms.Viewport);
        Assert.Equal("target.png", ms.TargetPath);
        Assert.Equal("local.png", ms.LocalPath);
        Assert.True(ms.TargetExists);
        Assert.True(ms.LocalExists);
    }

    [Fact]
    public void SanitizeError_WithToken_ReplacesToken()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("SanitizeError",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "Error: token ghp_abc123", "ghp_abc123" })!;

        Assert.Equal("Error: token ***", result);
    }

    [Fact]
    public void SanitizeError_NullToken_ReturnsOriginal()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("SanitizeError",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "Error: test", null! })!;

        Assert.Equal("Error: test", result);
    }

    [Fact]
    public void SanitizeError_EmptyToken_ReturnsOriginal()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("SanitizeError",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "Error: test", "" })!;

        Assert.Equal("Error: test", result);
    }

    [Fact]
    public void AugmentErrorHint_403_AddsScopeHint()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("AugmentErrorHint",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "HTTP 403: Forbidden" })!;

        Assert.Contains("Ensure your GITHUB_TOKEN", result);
    }

    [Fact]
    public void AugmentErrorHint_Forbidden_AddsScopeHint()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("AugmentErrorHint",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "Forbidden" })!;

        Assert.Contains("Ensure your GITHUB_TOKEN", result);
    }

    [Fact]
    public void AugmentErrorHint_HostResolution_AddsNetworkHint()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("AugmentErrorHint",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "Could not resolve host: github.com" })!;

        Assert.Contains("Check your network", result);
    }

    [Fact]
    public void AugmentErrorHint_UnableToAccess_AddsNetworkHint()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("AugmentErrorHint",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "unable to access repository" })!;

        Assert.Contains("Check your network", result);
    }

    [Fact]
    public void AugmentErrorHint_PermissionDenied_AddsTokenHint()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("AugmentErrorHint",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "Permission denied" })!;

        Assert.Contains("Verify your GITHUB_TOKEN", result);
    }

    [Fact]
    public void AugmentErrorHint_NotAuthorized_AddsTokenHint()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("AugmentErrorHint",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "not authorized" })!;

        Assert.Contains("Verify your GITHUB_TOKEN", result);
    }

    [Fact]
    public void AugmentErrorHint_UnknownError_ReturnsOriginal()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("AugmentErrorHint",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, new object[] { "Unknown error occurred" })!;

        Assert.Equal("Unknown error occurred", result);
    }

    [Fact]
    public void CreateGitProcess_SetsProperties()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("CreateGitProcess",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var psi = (System.Diagnostics.ProcessStartInfo)method.Invoke(null,
            new object[] { "/usr/bin/git", "/repo", new[] { "status", "--short" } })!;

        Assert.Equal("/usr/bin/git", psi.FileName);
        Assert.Equal("/repo", psi.WorkingDirectory);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.True(psi.CreateNoWindow);
        Assert.Contains("status", psi.ArgumentList);
        Assert.Contains("--short", psi.ArgumentList);
    }

    [Fact]
    public void CreateGitProcess_NullWorkingDir_NoWorkingDirSet()
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod("CreateGitProcess",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var psi = (System.Diagnostics.ProcessStartInfo)method.Invoke(null,
            new object[] { "git", null!, Array.Empty<string>() })!;

        Assert.Equal("git", psi.FileName);
        Assert.Equal("", psi.WorkingDirectory);
    }
}
