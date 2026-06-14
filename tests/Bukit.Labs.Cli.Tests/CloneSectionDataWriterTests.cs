using System.Text;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneSectionDataWriterTests
{
    [Fact]
    public void NormalizeSections_AssignsOrderTypeKeyAndCssClass()
    {
        var sections = new[]
        {
            new CloneSectionInfo
            {
                Id = "Hero Banner",
                Type = "header"
            },
            new CloneSectionInfo
            {
                Semantic = "call-to-action",
                Order = 42
            }
        };

        var normalized = CloneSectionDataWriter.NormalizeSections(sections).ToList();

        Assert.Equal("hero-banner", normalized[0].Key);
        Assert.Equal("navigation", normalized[0].Type);
        Assert.Equal(10, normalized[0].Order);
        Assert.Equal("clone-section-001", normalized[0].CssClass);
        Assert.Equal("cta", normalized[1].Type);
        Assert.Equal(42, normalized[1].Order);
        Assert.Equal("clone-002-cta", normalized[1].Key);
    }

    [Fact]
    public void GenerateSectionData_WithRichSection_RendersYamlAndBodyMarkup()
    {
        var section = new CloneSectionInfo
        {
            Type = "hero",
            Semantic = "hero",
            Title = "Launch",
            Eyebrow = "New",
            Subheading = "Ship faster",
            Text = "Use <fewer> tools & move faster",
            ContentHtml = "<p><img src=\"https://cdn.example.com/hero.png\" /></p>",
            Bounds = new CloneBox { X = 10, Y = 20, Width = 120, Height = 80 },
            Styles = new Dictionary<string, string> { ["color"] = "#111111" },
            ComputedStyles = new Dictionary<string, string> { ["display"] = "grid" },
            Buttons =
            [
                new CloneSectionButton { Label = "Get started", Url = "/start?x=1&y=2", Variant = "primary" }
            ],
            Items =
            [
                new CloneSectionItem
                {
                    Title = "Fast <Card>",
                    Description = "Faster than before",
                    Url = "/fast",
                    Image = "https://cdn.example.com/card.png"
                }
            ],
            Components =
            [
                new CloneComponentInfo
                {
                    Type = "card"
                }
            ],
            ImageUrls = ["https://cdn.example.com/hero.png", "https://cdn.example.com/hero.png"],
            Assets =
            [
                new CloneSectionAsset
                {
                    Src = "https://cdn.example.com/asset.png"
                }
            ],
            Interactions =
            [
                new CloneInteractionInfo
                {
                    Type = "click",
                    Target = "#modal"
                }
            ],
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
            ],
            Responsive = new SectionResponsiveInfo
            {
                ColumnsDesktop = "1fr 1fr",
                ColumnsTablet = "1fr",
                ColumnsMobile = "1fr"
            }
        };

        var normalized = CloneSectionDataWriter.NormalizeSections([section]).Single();
        var markdown = CloneSectionDataWriter.GenerateSectionData(
            normalized,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["https://cdn.example.com/hero.png"] = "/assets/images/hero.png",
                ["https://cdn.example.com/card.png"] = "/assets/images/card.png",
                ["https://cdn.example.com/asset.png"] = "/assets/images/asset.png"
            });

        Assert.Contains("title: 'Launch'", markdown, StringComparison.Ordinal);
        Assert.Contains("type: 'hero'", markdown, StringComparison.Ordinal);
        Assert.Contains("clone_key: 'clone-001-hero'", markdown, StringComparison.Ordinal);
        Assert.Contains("clone_class: 'clone-section-001'", markdown, StringComparison.Ordinal);
        Assert.Contains("semantic: 'hero'", markdown, StringComparison.Ordinal);
        Assert.Contains("eyebrow: 'New'", markdown, StringComparison.Ordinal);
        Assert.Contains("subheading: 'Ship faster'", markdown, StringComparison.Ordinal);
        Assert.Contains("buttons_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("items_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("components_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("styles_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("computed_styles_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("bounds_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("interactions_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("states_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("responsive_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("image_urls_json:", markdown, StringComparison.Ordinal);
        Assert.Contains("/assets/images/hero.png", markdown, StringComparison.Ordinal);
        Assert.Contains("<p>Use &lt;fewer&gt; tools &amp; move faster</p>", markdown, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/assets/images/card.png\" alt=\"\" loading=\"lazy\" />", markdown, StringComparison.Ordinal);
        Assert.Contains("<h3>Fast &lt;Card&gt;</h3>", markdown, StringComparison.Ordinal);
        Assert.Contains("<a class=\"clone-link\" href=\"/fast\">Fast &lt;Card&gt;</a>", markdown, StringComparison.Ordinal);
        Assert.Contains("class=\"state-tab\" role=\"tab\" aria-selected=\"true\"", markdown, StringComparison.Ordinal);
        Assert.Contains("id=\"clone-001-hero-state-1\"", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSectionBody_WhenSectionEmpty_ReturnsPlaceholder()
    {
        var normalized = CloneSectionDataWriter.NormalizeSections([new CloneSectionInfo()]).Single();

        var body = CloneSectionDataWriter.BuildSectionBody(normalized, new Dictionary<string, string>());

        Assert.Equal("<!-- cloned empty section -->", body);
    }

    [Fact]
    public void BuildSectionBody_SkipsButtonsWithoutLabel_AndUsesFallbackStateLabel()
    {
        var normalized = CloneSectionDataWriter.NormalizeSections(
        [
            new CloneSectionInfo
            {
                Buttons =
                [
                    new CloneSectionButton { Label = null, Url = "/ignored" }
                ],
                States =
                [
                    new SectionState { Label = null, ContentHtml = "<p>Default state</p>" }
                ]
            }
        ]).Single();

        var body = CloneSectionDataWriter.BuildSectionBody(normalized, new Dictionary<string, string>());

        Assert.DoesNotContain("/ignored", body, StringComparison.Ordinal);
        Assert.Contains(">State 1</button>", body, StringComparison.Ordinal);
        Assert.Contains("<p>Default state</p>", body, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateStructuredIndex_UsesTypeSpecificPartials()
    {
        var sections = CloneSectionDataWriter.NormalizeSections(
        [
            new CloneSectionInfo { Type = "navigation" },
            new CloneSectionInfo { Type = "features" },
            new CloneSectionInfo { Type = "faq" },
            new CloneSectionInfo { Type = "unknown type" }
        ]).ToList();

        var template = CloneSectionDataWriter.GenerateStructuredIndex(sections);

        Assert.Contains("site.modules.navigation", template, StringComparison.Ordinal);
        Assert.Contains("partials/clone-navigation.html", template, StringComparison.Ordinal);
        Assert.Contains("site.modules.features", template, StringComparison.Ordinal);
        Assert.Contains("partials/clone-feature-grid.html", template, StringComparison.Ordinal);
        Assert.Contains("site.modules.faq", template, StringComparison.Ordinal);
        Assert.Contains("partials/clone-faq.html", template, StringComparison.Ordinal);
        Assert.Contains("site.modules.unknown_type", template, StringComparison.Ordinal);
        Assert.Contains("partials/clone-section.html", template, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendResponsiveCss_WritesDesktopTabletAndMobileRules()
    {
        var section = CloneSectionDataWriter.NormalizeSections(
        [
            new CloneSectionInfo
            {
                Responsive = new SectionResponsiveInfo
                {
                    ColumnsDesktop = "repeat(3, minmax(0, 1fr))",
                    MaxWidthDesktop = "72rem",
                    ColumnsTablet = "1fr 1fr",
                    MaxWidthTablet = "48rem",
                    ColumnsMobile = "1fr",
                    MaxWidthMobile = "24rem"
                }
            }
        ]).Single();

        var sb = new StringBuilder();
        CloneSectionDataWriter.AppendResponsiveCss(sb, section);
        var css = sb.ToString();

        Assert.Contains(".clone-section-001 .clone-items { grid-template-columns: repeat(3, minmax(0, 1fr)); }", css, StringComparison.Ordinal);
        Assert.Contains(".clone-section-001 { max-width: 72rem; }", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: var(--bp-tablet))", css, StringComparison.Ordinal);
        Assert.Contains(".clone-section-001 .clone-items { grid-template-columns: 1fr 1fr; }", css, StringComparison.Ordinal);
        Assert.Contains(".clone-section-001 { max-width: 48rem; }", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: var(--bp-mobile))", css, StringComparison.Ordinal);
        Assert.Contains(".clone-section-001 { max-width: 24rem; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialFor_AndCommonPartials_ReturnExpectedValues()
    {
        Assert.Equal("clone-navigation", CloneSectionDataWriter.PartialFor("navigation"));
        Assert.Equal("clone-cta", CloneSectionDataWriter.PartialFor("cta"));
        Assert.Equal("clone-pricing", CloneSectionDataWriter.PartialFor("pricing"));
        Assert.Equal("clone-footer", CloneSectionDataWriter.PartialFor("footer"));
        Assert.Equal("clone-section", CloneSectionDataWriter.PartialFor("other"));
        Assert.Contains("clone-footer", CloneSectionDataWriter.CommonPartials());
    }
}
