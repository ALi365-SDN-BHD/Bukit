using System.Text;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneIndexPageGeneratorTests
{
    [Fact]
    public void GenerateIndex_WithoutHeroHeading_UsesSiteFallbackHero()
    {
        var html = CloneIndexPageGenerator.GenerateIndex(
            CloneTokens.Default,
            new CloneLayoutInfo(),
            brand: null);

        Assert.Contains("<h1>{{ site.title }}</h1>", html, StringComparison.Ordinal);
        Assert.Contains("{{ if site.description }}<p>{{ site.description }}</p>{{ end }}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("hero-cta", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateIndex_WithHeroFeaturesCtaAndResponsiveStaticSection_RendersExpectedMarkup()
    {
        var layout = new CloneLayoutInfo
        {
            SiteTitle = "Acme <Site>",
            HeroHeading = "Ship faster",
            HeroSubtext = "Ship <safer> too",
            HasHeroCta = true,
            HeroCtaText = "Start <now>",
            HeroCtaUrl = "/get-started?x=1&y=2",
            HasFeaturesSection = true,
            HasCTASection = true,
            ExtraSections =
            [
                new SectionInfo
                {
                    Heading = "Gallery",
                    ContentHtml = "<p>Preview</p>",
                    ImageUrls = ["/img/hero.png"],
                    Responsive = new SectionResponsiveInfo
                    {
                        MaxWidthDesktop = "72rem",
                        ColumnsDesktop = "repeat(3, minmax(0, 1fr))",
                        MaxWidthTablet = "48rem",
                        ColumnsTablet = "1fr 1fr",
                        MaxWidthMobile = "24rem",
                        ColumnsMobile = "1fr"
                    }
                }
            ]
        };

        var html = CloneIndexPageGenerator.GenerateIndex(CloneTokens.Default, layout, "Fallback Brand");

        Assert.Contains("<p class=\"eyebrow\">Acme &lt;Site&gt;</p>", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Ship faster</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Ship &lt;safer&gt; too</p>", html, StringComparison.Ordinal);
        Assert.Contains("<a class=\"hero-cta\" href=\"/get-started?x=1&amp;y=2\">Start &lt;now&gt;</a>", html, StringComparison.Ordinal);
        Assert.Contains("{{ for feature in site.modules.features }}", html, StringComparison.Ordinal);
        Assert.Contains("{{ cta = site.modules.call_to_action[0] }}", html, StringComparison.Ordinal);
        Assert.Contains("<style>", html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: var(--bp-tablet))", html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: var(--bp-mobile))", html, StringComparison.Ordinal);
        Assert.Contains("<section class=\"sec-r-", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"section-heading\">Gallery</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/img/hero.png\" alt=\"\" loading=\"lazy\" />", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateIndex_WithSingleStateSection_FallsBackToStaticAndDoesNotInjectStateScript()
    {
        var warnings = new List<string>();
        var layout = new CloneLayoutInfo
        {
            ExtraSections =
            [
                new SectionInfo
                {
                    Heading = "Pricing",
                    ContentHtml = "<p>Static pricing</p>",
                    States =
                    [
                        new SectionState
                        {
                            Label = "Monthly",
                            ContentHtml = "<p>Monthly pricing</p>"
                        }
                    ]
                }
            ]
        };

        var html = CloneIndexPageGenerator.GenerateIndex(CloneTokens.Default, layout, "Acme", warnings);

        Assert.Contains("Skipped multi-state section \"Pricing\": needs at least 2 states.", warnings);
        Assert.Contains("<h2 class=\"section-heading\">Pricing</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Static pricing</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"state-tab\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("document.querySelectorAll('.state-section')", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateIndex_WithMultiStateSection_RendersTabsPanelsAndStateScript()
    {
        var warnings = new List<string>();
        var layout = new CloneLayoutInfo
        {
            ExtraSections =
            [
                new SectionInfo
                {
                    Heading = "Demo",
                    States =
                    [
                        new SectionState
                        {
                            Label = "Desktop",
                            ContentHtml = "<p>Desktop state</p>"
                        },
                        new SectionState
                        {
                            Label = "Mobile",
                            ContentHtml = "<p>Mobile state</p>"
                        }
                    ]
                }
            ]
        };

        var html = CloneIndexPageGenerator.GenerateIndex(CloneTokens.Default, layout, "Acme", warnings);

        Assert.Empty(warnings);
        Assert.Contains("class=\"state-section\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"state-tab\" role=\"tab\" aria-selected=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"state-panel hidden\" role=\"tabpanel\"", html, StringComparison.Ordinal);
        Assert.Contains("<p>Desktop state</p>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Mobile state</p>", html, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('.state-section')", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateResponsiveCss_WithoutTabletOrMobileValues_OnlyEmitsDesktopRules()
    {
        var section = new SectionInfo
        {
            Heading = "Gallery",
            Responsive = new SectionResponsiveInfo
            {
                MaxWidthDesktop = "72rem",
                ColumnsDesktop = "repeat(3, minmax(0, 1fr))"
            }
        };

        var css = CloneIndexPageGenerator.GenerateResponsiveCss(section);

        Assert.Contains("<style>", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 72rem;", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(3, minmax(0, 1fr));", css, StringComparison.Ordinal);
        Assert.DoesNotContain("@media (max-width: var(--bp-tablet))", css, StringComparison.Ordinal);
        Assert.DoesNotContain("@media (max-width: var(--bp-mobile))", css, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateStaticSection_WritesHeadingContentAndImages()
    {
        var sb = new StringBuilder();
        var section = new SectionInfo
        {
            Heading = "Highlights",
            ContentHtml = "<p>Fast</p>",
            ImageUrls = ["/img/a.png", "/img/b.png"]
        };

        CloneIndexPageGenerator.GenerateStaticSection(sb, section);

        var html = sb.ToString();
        Assert.Contains("<section>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"section-heading\">Highlights</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Fast</p>", html, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/img/a.png\" alt=\"\" loading=\"lazy\" />", html, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/img/b.png\" alt=\"\" loading=\"lazy\" />", html, StringComparison.Ordinal);
    }
}
