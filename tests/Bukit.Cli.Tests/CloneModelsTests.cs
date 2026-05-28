using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneModelsDeserializationTests
{
    [Fact]
    public void CloneTokens_FromJson_DeserializesAllFields()
    {
        var json = """
        {
            "bg": "#ffffff",
            "surface": "#f5f5f5",
            "surfaceMuted": "#e0e0e0",
            "text": "#111111",
            "muted": "#666666",
            "border": "#cccccc",
            "primary": "#3366ff",
            "primaryStrong": "#2255dd",
            "accent": "#ff6633",
            "radius": "8px",
            "contentMax": "1200px",
            "wideMax": "1400px",
            "shadow": "0 2px 8px rgba(0,0,0,0.15)",
            "cardShadow": "0 4px 12px rgba(0,0,0,0.1)",
            "modalShadow": "0 8px 32px rgba(0,0,0,0.2)",
            "dropdownShadow": "0 4px 16px rgba(0,0,0,0.15)",
            "navPadding": "1rem",
            "containerPadding": "2rem",
            "sectionGap": "4rem",
            "fontFamily": "'Inter', sans-serif",
            "headingFontFamily": "'Poppins', sans-serif",
            "codeFontFamily": "'Fira Code', monospace",
            "googleFontsUrl": "https://fonts.googleapis.com/css2?family=Inter",
            "hoverLift": "translateY(-4px)",
            "hoverShadow": "0 8px 24px rgba(0,0,0,0.15)",
            "fontSizeXs": "0.75rem",
            "fontSizeSm": "0.875rem",
            "fontSizeBase": "1rem",
            "fontSizeLg": "1.125rem",
            "fontSizeXl": "1.25rem",
            "fontSize2xl": "1.5rem",
            "fontSize3xl": "1.875rem",
            "fontSize4xl": "2.25rem",
            "fontSizeDisplay": "3rem",
            "fontWeightNormal": "400",
            "fontWeightBold": "700",
            "lineHeightTight": "1.25",
            "lineHeightNormal": "1.5",
            "lineHeightRelaxed": "1.75",
            "zHeader": "1000",
            "zDropdown": "1100",
            "zModal": "1200",
            "zTooltip": "1300"
        }
        """;

        var tokens = CloneTokens.FromJson(json).tokens;

        Assert.Equal("#ffffff", tokens.Bg);
        Assert.Equal("#3366ff", tokens.Primary);
        Assert.Equal("#ff6633", tokens.Accent);
        Assert.Equal("8px", tokens.Radius);
        Assert.Equal("1200px", tokens.ContentMax);
        Assert.Equal("0 2px 8px rgba(0,0,0,0.15)", tokens.Shadow);
        Assert.Equal("1rem", tokens.NavPadding);
        Assert.Equal("'Inter', sans-serif", tokens.FontFamily);
        Assert.Equal("'Poppins', sans-serif", tokens.HeadingFontFamily);
        Assert.Equal("'Fira Code', monospace", tokens.CodeFontFamily);
        Assert.Equal("https://fonts.googleapis.com/css2?family=Inter", tokens.GoogleFontsUrl);
        Assert.Equal("0 8px 24px rgba(0,0,0,0.15)", tokens.HoverShadow);
        Assert.Equal("1rem", tokens.FontSizeBase);
        Assert.Equal("3rem", tokens.FontSizeDisplay);
        Assert.Equal("400", tokens.FontWeightNormal);
        Assert.Equal("700", tokens.FontWeightBold);
        Assert.Equal("1.5", tokens.LineHeightNormal);
        Assert.Equal("1000", tokens.ZHeader);
        Assert.Equal("1300", tokens.ZTooltip);
    }

    [Fact]
    public void CloneTokens_FromJson_NullOrEmpty_ReturnsDefault()
    {
        var fromNull = CloneTokens.FromJson(null!).tokens;
        Assert.NotNull(fromNull);
        Assert.Null(fromNull.Primary);

        var fromEmpty = CloneTokens.FromJson("").tokens;
        Assert.NotNull(fromEmpty);
        Assert.Null(fromEmpty.Bg);

        var fromWhitespace = CloneTokens.FromJson("   ").tokens;
        Assert.NotNull(fromWhitespace);
        Assert.Null(fromWhitespace.Accent);
    }

    [Fact]
    public void CloneTokens_FromJson_InvalidJson_ReturnsDefault()
    {
        var tokens = CloneTokens.FromJson("{ invalid json!!! }").tokens;
        Assert.NotNull(tokens);
        Assert.Null(tokens.Primary);
    }

    [Fact]
    public void CloneTokens_FromJson_WithWrappedTokens_Deserializes()
    {
        var json = """
        {
            "tokens": {
                "primary": "#ff0000",
                "bg": "#ffffff"
            }
        }
        """;

        var tokens = CloneTokens.FromJson(json).tokens;

        Assert.Equal("#ff0000", tokens.Primary);
        Assert.Equal("#ffffff", tokens.Bg);
    }

    [Fact]
    public void ClonePageInfo_FromJson_DeserializesAllFields()
    {
        var json = """
        {
            "title": "Test Page",
            "slug": "test-page",
            "url": "/test-page/",
            "summary": "A test page summary",
            "description": "Longer description",
            "bodyMarkdown": "# Hello\\n\\nThis is markdown content.",
            "contentHtml": "<h1>Hello</h1><p>This is HTML content.</p>",
            "seo": {
                "title": "SEO Title",
                "description": "SEO Description",
                "image": "/img/og.png",
                "robots": "index,follow"
            },
            "screenshots": [
                { "name": "desktop", "width": 1440, "height": 900, "screenshot": "desktop.png" }
            ]
        }
        """;

        var page = ClonePageInfo.FromJson(json);

        Assert.Equal("Test Page", page.Title);
        Assert.Equal("test-page", page.Slug);
        Assert.Equal("/test-page/", page.Url);
        Assert.Equal("A test page summary", page.Summary);
        Assert.NotNull(page.Seo);
        Assert.Equal("SEO Title", page.Seo!.Title);
        Assert.Equal("SEO Description", page.Seo.Description);
        Assert.Equal("/img/og.png", page.Seo.Image);
        Assert.Equal("index,follow", page.Seo.Robots);
        Assert.Single(page.Screenshots);
        Assert.Equal("desktop", page.Screenshots[0].Name);
    }

    [Fact]
    public void ClonePageInfo_FromJson_NullOrEmpty_ReturnsDefault()
    {
        var fromNull = ClonePageInfo.FromJson(null!);
        Assert.NotNull(fromNull);
        Assert.Null(fromNull.Title);

        var fromEmpty = ClonePageInfo.FromJson("");
        Assert.NotNull(fromEmpty);
        Assert.Null(fromNull.Slug);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_DeserializesNavAndHero()
    {
        var json = """
        {
            "siteTitle": "My Site",
            "heroHeading": "Welcome",
            "heroSubtext": "A beautiful site",
            "hasFeaturesSection": true,
            "hasCTASection": true,
            "hasHeroCta": true,
            "heroCtaText": "Get Started",
            "heroCtaUrl": "/signup/",
            "navLinks": [
                { "label": "Home", "url": "/" },
                { "label": "About", "url": "/about/" }
            ],
            "footerLinks": [
                { "label": "Privacy", "url": "/privacy/" }
            ],
            "extraSections": [
                {
                    "semantic": "features",
                    "heading": "Features",
                    "contentHtml": "<p>Our features</p>"
                }
            ]
        }
        """;

        var layout = CloneLayoutInfo.FromJson(json);

        Assert.Equal("My Site", layout.SiteTitle);
        Assert.Equal("Welcome", layout.HeroHeading);
        Assert.Equal("A beautiful site", layout.HeroSubtext);
        Assert.True(layout.HasFeaturesSection);
        Assert.True(layout.HasCTASection);
        Assert.True(layout.HasHeroCta);
        Assert.Equal("Get Started", layout.HeroCtaText);
        Assert.Equal("/signup/", layout.HeroCtaUrl);
        Assert.Equal(2, layout.NavLinks.Count);
        Assert.Equal("Home", layout.NavLinks[0].Label);
        Assert.Equal("/", layout.NavLinks[0].Url);
        Assert.Single(layout.FooterLinks);
        Assert.Single(layout.ExtraSections);
        Assert.Equal("features", layout.ExtraSections[0].Semantic);
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_Default_IsValid()
    {
        var layout = CloneLayoutInfo.FromJson("{}");
        Assert.NotNull(layout);
        Assert.Empty(layout.NavLinks);
        Assert.Empty(layout.FooterLinks);
        Assert.Empty(layout.ExtraSections);
    }

    [Fact]
    public void CloneBehaviors_FromJson_DeserializesAllFlags()
    {
        var json = """
        {
            "stickyHeader": true,
            "cardHoverLift": true,
            "animateOnScroll": true,
            "scrollShrinkNav": false,
            "darkModeToggle": true,
            "mobileHamburger": true,
            "smoothScroll": true,
            "backToTop": true,
            "hasModal": false,
            "hasDropdown": true,
            "hasTabs": false,
            "animationStyle": "fade-up",
            "scrollThreshold": 100,
            "useLenis": false
        }
        """;

        var behaviors = CloneBehaviors.FromJson(json);

        Assert.True(behaviors.StickyHeader);
        Assert.True(behaviors.CardHoverLift);
        Assert.True(behaviors.AnimateOnScroll);
        Assert.False(behaviors.ScrollShrinkNav);
        Assert.True(behaviors.DarkModeToggle);
        Assert.True(behaviors.MobileHamburger);
        Assert.True(behaviors.SmoothScroll);
        Assert.True(behaviors.BackToTop);
        Assert.False(behaviors.HasModal);
        Assert.True(behaviors.HasDropdown);
        Assert.False(behaviors.HasTabs);
        Assert.Equal("fade-up", behaviors.AnimationStyle);
        Assert.Equal(100, behaviors.ScrollThreshold);
        Assert.False(behaviors.UseLenis);
        Assert.True(behaviors.HasExtraPartials);
    }

    [Fact]
    public void CloneBehaviors_HasAnyCssBehavior_TrueWhenStickyHeader()
    {
        var behaviors = new CloneBehaviors { StickyHeader = true };
        Assert.True(behaviors.HasAnyCssBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasAnyCssBehavior_FalseWhenAllDefault()
    {
        var behaviors = CloneBehaviors.Default;
        Assert.False(behaviors.HasAnyCssBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasAnyJsBehavior_TrueWhenSmoothScroll()
    {
        var behaviors = new CloneBehaviors { SmoothScroll = true };
        Assert.True(behaviors.HasAnyJsBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasAnyJsBehavior_FalseWhenAllDefault()
    {
        var behaviors = CloneBehaviors.Default;
        Assert.False(behaviors.HasAnyJsBehavior);
    }

    [Fact]
    public void CloneBehaviors_HasExtraPartials_TrueWithModal()
    {
        var behaviors = new CloneBehaviors { HasModal = true };
        Assert.True(behaviors.HasExtraPartials);
    }

    [Fact]
    public void CloneBehaviors_FromJson_NullOrEmpty_ReturnsDefault()
    {
        var fromNull = CloneBehaviors.FromJson(null!);
        Assert.False(fromNull.StickyHeader);

        var fromEmpty = CloneBehaviors.FromJson("");
        Assert.False(fromEmpty.DarkModeToggle);
    }

    [Fact]
    public void CloneBehaviors_FromJson_InvalidJson_ReturnsDefault()
    {
        var behaviors = CloneBehaviors.FromJson("<<< INVALID >>>");
        Assert.False(behaviors.SmoothScroll);
    }

    [Fact]
    public void IsSafeThemeName_ValidNames_ReturnTrue()
    {
        Assert.True(CloneModels.IsSafeThemeName("starter"));
        Assert.True(CloneModels.IsSafeThemeName("my-theme"));
        Assert.True(CloneModels.IsSafeThemeName("theme_v1"));
        Assert.True(CloneModels.IsSafeThemeName("a"));
    }

    [Fact]
    public void IsSafeThemeName_InvalidNames_ReturnFalse()
    {
        Assert.False(CloneModels.IsSafeThemeName(null));
        Assert.False(CloneModels.IsSafeThemeName(""));
        Assert.False(CloneModels.IsSafeThemeName("  "));
        Assert.False(CloneModels.IsSafeThemeName("."));
        Assert.False(CloneModels.IsSafeThemeName(".."));
        Assert.False(CloneModels.IsSafeThemeName("/etc/passwd"));
        Assert.False(CloneModels.IsSafeThemeName("theme/with/slash"));
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_ArrayFormat_Deserializes()
    {
        var json = """
        [
            {
                "id": "hero",
                "type": "hero",
                "semantic": "hero",
                "title": "Welcome",
                "heading": "Hero Heading",
                "eyebrow": "New",
                "subheading": "Subheading text",
                "text": "Body text",
                "order": 1,
                "className": "hero-section",
                "buttons": [
                    { "label": "Click me", "url": "/cta/", "variant": "primary" }
                ],
                "items": [
                    { "title": "Item 1", "text": "Description", "url": "/items/1/" }
                ]
            }
        ]
        """;

        var sections = CloneSectionsDocument.FromJson(json);

        Assert.Single(sections);
        var hero = sections[0];
        Assert.Equal("hero", hero.Id);
        Assert.Equal("hero", hero.Type);
        Assert.Equal("Welcome", hero.Title);
        Assert.Equal("Hero Heading", hero.Heading);
        Assert.Equal("New", hero.Eyebrow);
        Assert.Equal("Subheading text", hero.Subheading);
        Assert.Equal("Body text", hero.Text);
        Assert.Equal(1, hero.Order);
        Assert.Equal("hero-section", hero.ClassName);
        Assert.Single(hero.Buttons);
        Assert.Equal("Click me", hero.Buttons[0].Label);
        Assert.Equal("/cta/", hero.Buttons[0].Url);
        Assert.Equal("primary", hero.Buttons[0].Variant);
        Assert.Single(hero.Items);
        Assert.Equal("Item 1", hero.Items[0].Title);
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_WrappedFormat_Deserializes()
    {
        var json = """
        {
            "sections": [
                {
                    "id": "features",
                    "type": "features-grid",
                    "heading": "Our Features"
                }
            ]
        }
        """;

        var sections = CloneSectionsDocument.FromJson(json);

        Assert.Single(sections);
        Assert.Equal("features", sections[0].Id);
        Assert.Equal("features-grid", sections[0].Type);
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_EmptyArray_ReturnsEmpty()
    {
        var sections = CloneSectionsDocument.FromJson("[]");
        Assert.Empty(sections);
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(CloneSectionsDocument.FromJson(null!));
        Assert.Empty(CloneSectionsDocument.FromJson(""));
    }

    [Fact]
    public void CloneSectionInfo_HasStates_ReflectsStateCount()
    {
        var section = new CloneSectionInfo
        {
            Title = "test",
            States = new List<SectionState>
            {
                new() { Label = "hover", ContentHtml = "<div>Hover</div>" }
            }
        };

        Assert.True(section.HasStates);
        Assert.Equal("test", section.DisplayTitle);
    }

    [Fact]
    public void CloneSectionInfo_DisplayTitle_FallsBackThroughFields()
    {
        var byTitle = new CloneSectionInfo { Title = "My Title" };
        Assert.Equal("My Title", byTitle.DisplayTitle);

        var byHeading = new CloneSectionInfo { Heading = "My Heading" };
        Assert.Equal("My Heading", byHeading.DisplayTitle);

        var byType = new CloneSectionInfo { Type = "hero" };
        Assert.Equal("hero", byType.DisplayTitle);

        var bySemantic = new CloneSectionInfo { Semantic = "footer" };
        Assert.Equal("footer", bySemantic.DisplayTitle);

        var fallback = new CloneSectionInfo();
        Assert.Equal("Section", fallback.DisplayTitle);
    }

    [Fact]
    public void SpacingScale_DeserializesAsPartOfTokens()
    {
        var json = """
        {
            "spacingScale": {
                "xs": "4px",
                "sm": "8px",
                "md": "16px",
                "lg": "24px",
                "xl": "32px"
            }
        }
        """;

        var tokens = CloneTokens.FromJson(json).tokens;

        Assert.NotNull(tokens.SpacingScale);
        Assert.Equal("4px", tokens.SpacingScale!.Xs);
        Assert.Equal("8px", tokens.SpacingScale.Sm);
        Assert.Equal("16px", tokens.SpacingScale.Md);
        Assert.Equal("24px", tokens.SpacingScale.Lg);
        Assert.Equal("32px", tokens.SpacingScale.Xl);
    }

    [Fact]
    public void ResponsiveBreakpoints_DeserializesAsPartOfTokens()
    {
        var json = """
        {
            "responsiveBreakpoints": {
                "mobile": "480px",
                "tablet": "768px",
                "desktop": "1024px"
            }
        }
        """;

        var tokens = CloneTokens.FromJson(json).tokens;

        Assert.NotNull(tokens.ResponsiveBreakpoints);
        Assert.Equal("480px", tokens.ResponsiveBreakpoints!.Mobile);
        Assert.Equal("768px", tokens.ResponsiveBreakpoints.Tablet);
        Assert.Equal("1024px", tokens.ResponsiveBreakpoints.Desktop);
    }
}
