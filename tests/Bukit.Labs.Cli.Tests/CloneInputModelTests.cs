using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneInputModelTests
{
    [Fact]
    public void CloneLayoutInfo_FromJson_InvalidJson_ReturnsDefaultInsteadOfThrowing()
    {
        CloneLayoutInfo? result = null;

        var ex = Record.Exception(() => result = CloneLayoutInfo.FromJson("{"));

        Assert.Null(ex);
        Assert.NotNull(result);
        Assert.Empty(result.NavLinks);
        Assert.Empty(result.FooterLinks);
        Assert.Empty(result.ExtraSections);
    }

    [Fact]
    public void ClonePageInfo_FromJson_InvalidJson_ReturnsDefaultInsteadOfThrowing()
    {
        ClonePageInfo? result = null;

        var ex = Record.Exception(() => result = ClonePageInfo.FromJson("{"));

        Assert.Null(ex);
        Assert.NotNull(result);
        Assert.Empty(result.Screenshots);
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_InvalidJson_ReturnsEmptyInsteadOfThrowing()
    {
        IReadOnlyList<CloneSectionInfo>? result = null;

        var ex = Record.Exception(() => result = CloneSectionsDocument.FromJson("{"));

        Assert.Null(ex);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_NormalizesNullNestedCollections()
    {
        const string json = """
{
  "sections": [
    {
      "title": "Hero",
      "buttons": null,
      "items": null,
      "components": [
        {
          "type": "card",
          "states": null,
          "interactions": null
        }
      ],
      "imageUrls": null,
      "assets": null,
      "states": null,
      "interactions": null
    }
  ]
}
""";

        var sections = CloneSectionsDocument.FromJson(json);

        var section = Assert.Single(sections);
        Assert.Empty(section.Buttons);
        Assert.Empty(section.Items);
        Assert.Empty(section.ImageUrls);
        Assert.Empty(section.Assets);
        Assert.Empty(section.States);
        Assert.Empty(section.Interactions);
        var component = Assert.Single(section.Components);
        Assert.Empty(component.States);
        Assert.Empty(component.Interactions);
    }

    [Fact]
    public void CloneTokens_FromJson_WrappedPayload_UsesWrapperContext()
    {
        const string json = """
{
  "tokens": {
    "bg": "#ffffff",
    "spacingScale": {
      "md": "1rem"
    },
    "responsiveBreakpoints": {
      "mobile": "640px"
    },
    "externalCssUrls": ["https://cdn.example.com/app.css"]
  }
}
""";

        var (tokens, error) = CloneTokens.FromJson(json);

        Assert.Null(error);
        Assert.Equal("#ffffff", tokens.Bg);
        Assert.Equal("1rem", tokens.SpacingScale!.Md);
        Assert.Equal("640px", tokens.ResponsiveBreakpoints!.Mobile);
        Assert.Equal("https://cdn.example.com/app.css", Assert.Single(tokens.ExternalCssUrls!));
    }

    [Fact]
    public void CloneLayoutInfo_FromJson_PopulatesNavigationFooterAndResponsiveSections()
    {
        const string json = """
{
  "siteTitle": "Docs",
  "heroHeading": "Ship faster",
  "navLinks": [
    {
      "label": "Home",
      "url": "/"
    }
  ],
  "footerLinks": [
    {
      "label": "GitHub",
      "url": "https://github.com/acme/docs"
    }
  ],
  "extraSections": [
    {
      "semantic": "gallery",
      "heading": "Gallery",
      "contentHtml": "<p>Hello</p>",
      "imageUrls": ["/img/hero.png"],
      "states": [
        {
          "label": "default",
          "contentHtml": "<p>State</p>",
          "screenshot": "hero-default.png",
          "computedStyles": {
            "display": "grid"
          }
        }
      ],
      "responsive": {
        "columnsDesktop": "3",
        "viewports": {
          "mobile": {
            "screenshot": "hero-mobile.png",
            "styles": {
              "display": "block"
            },
            "bounds": {
              "x": 1,
              "y": 2,
              "width": 320,
              "height": 180
            }
          }
        }
      }
    }
  ]
}
""";

        var layout = CloneLayoutInfo.FromJson(json);

        Assert.Equal("Docs", layout.SiteTitle);
        Assert.Equal("Ship faster", layout.HeroHeading);
        var nav = Assert.Single(layout.NavLinks);
        Assert.Equal("Home", nav.Label);
        Assert.Equal("/", nav.Url);
        var footer = Assert.Single(layout.FooterLinks);
        Assert.Equal("GitHub", footer.Label);
        var section = Assert.Single(layout.ExtraSections);
        Assert.True(section.HasStates);
        Assert.True(section.HasResponsive);
        Assert.Equal("/img/hero.png", Assert.Single(section.ImageUrls));
        var state = Assert.Single(section.States);
        Assert.Equal("default", state.Label);
        Assert.Equal("grid", state.ComputedStyles!["display"]);
        var mobile = section.Responsive!.Viewports!["mobile"];
        Assert.Equal("hero-mobile.png", mobile.Screenshot);
        Assert.Equal("block", mobile.Styles!["display"]);
        Assert.Equal(320, mobile.Bounds!.Width);
    }

    [Fact]
    public void ClonePageInfo_FromJson_PopulatesSeoAndScreenshots()
    {
        const string json = """
{
  "title": "Welcome",
  "slug": "welcome",
  "seo": {
    "title": "SEO Title",
    "description": "SEO Description",
    "image": "/cover.png",
    "robots": "index,follow"
  },
  "screenshots": [
    {
      "name": "desktop",
      "width": 1440,
      "height": 900,
      "screenshot": "desktop.png"
    }
  ]
}
""";

        var page = ClonePageInfo.FromJson(json);

        Assert.Equal("Welcome", page.Title);
        Assert.Equal("welcome", page.Slug);
        Assert.NotNull(page.Seo);
        Assert.Equal("SEO Title", page.Seo!.Title);
        var shot = Assert.Single(page.Screenshots);
        Assert.Equal("desktop", shot.Name);
        Assert.Equal(1440, shot.Width);
        Assert.Equal("desktop.png", shot.Screenshot);
    }

    [Fact]
    public void CloneSectionsDocument_FromJson_ArrayPayload_PopulatesButtonsAssetsAndResponsiveData()
    {
        const string json = """
[
  {
    "type": "hero",
    "title": "Hero",
    "buttons": [
      {
        "label": "Get started",
        "url": "/start",
        "variant": "primary"
      }
    ],
    "items": [
      {
        "title": "Fast",
        "text": "Fast enough",
        "description": "Details",
        "url": "/fast",
        "image": "/fast.png",
        "icon": "zap",
        "bounds": {
          "x": 10,
          "y": 20,
          "width": 30,
          "height": 40
        },
        "computedStyles": {
          "color": "#111"
        }
      }
    ],
    "assets": [
      {
        "type": "image",
        "src": "/hero.png",
        "alt": "Hero",
        "localPath": "assets/hero.png",
        "media": "(min-width: 640px)",
        "width": "1280",
        "height": "720"
      }
    ],
    "interactions": [
      {
        "type": "click",
        "trigger": ".cta",
        "target": "#modal",
        "description": "Open modal",
        "states": {
          "aria-expanded": "true"
        }
      }
    ],
    "responsive": {
      "columnsDesktop": "2",
      "maxWidthMobile": "100%",
      "viewports": {
        "desktop": {
          "screenshot": "hero-desktop.png",
          "styles": {
            "gap": "24px"
          },
          "bounds": {
            "x": 0,
            "y": 0,
            "width": 1280,
            "height": 600
          }
        }
      }
    }
  }
]
""";

        var sections = CloneSectionsDocument.FromJson(json);

        var section = Assert.Single(sections);
        Assert.True(section.DisplayTitle == "Hero");
        var button = Assert.Single(section.Buttons);
        Assert.Equal("primary", button.Variant);
        var item = Assert.Single(section.Items);
        Assert.Equal("zap", item.Icon);
        Assert.Equal(30, item.Bounds!.Width);
        Assert.Equal("#111", item.ComputedStyles!["color"]);
        var asset = Assert.Single(section.Assets);
        Assert.Equal("image", asset.Type);
        Assert.Equal("assets/hero.png", asset.LocalPath);
        var interaction = Assert.Single(section.Interactions);
        Assert.Equal("true", interaction.States!["aria-expanded"]);
        Assert.Equal("hero-desktop.png", section.Responsive!.Viewports!["desktop"].Screenshot);
        Assert.Equal("24px", section.Responsive.Viewports["desktop"].Styles!["gap"]);
    }

    [Fact]
    public void CloneBehaviors_FromJson_ComputesDerivedFlags()
    {
        const string json = """
{
  "stickyHeader": true,
  "scrollShrinkNav": true,
  "darkModeToggle": true,
  "mobileHamburger": true,
  "hasModal": true,
  "useLenis": true
}
""";

        var behaviors = CloneBehaviors.FromJson(json);

        Assert.True(behaviors.StickyHeader);
        Assert.True(behaviors.HasExtraPartials);
        Assert.True(behaviors.HasAnyCssBehavior);
        Assert.True(behaviors.HasAnyJsBehavior);
        Assert.True(behaviors.UseLenis);
    }

    [Fact]
    public void CloneJson_SerializeIndented_UsesCamelCaseAndIndentedOutput()
    {
        var report = new CloneVerifyReportJson(
            BuildPassed: true,
            ConfigPath: "site.yaml",
            VisualThreshold: 0.02,
            Passed: true,
            Summary: new CloneVerifyReportSummary(1, 0, 0, 1),
            Comparisons:
            [
                new CloneVerifyScreenshotComparison(
                    Name: "home-desktop",
                    Passed: true,
                    Status: "pass",
                    ComparedPixels: 100,
                    MismatchedPixels: 0,
                    DiffRatio: 0,
                    TargetWidth: 1280,
                    TargetHeight: 720,
                    LocalWidth: 1280,
                    LocalHeight: 720,
                    MismatchBounds: new CloneVerifyMismatchBounds(0, 0, 0, 0))
            ],
            MissingScreenshots: [],
            AffectedSections:
            [
                new CloneVerifyAffectedSection(
                    Screenshot: "home-desktop.png",
                    Viewport: "desktop",
                    SectionIndex: 0,
                    SectionKey: "hero",
                    SectionId: "hero",
                    SectionType: "hero",
                    SectionOrder: 1,
                    SectionLabel: "Hero",
                    DataPath: "data/sections/hero.json",
                    SpecPath: "specs/hero.json",
                    SectionY: 10,
                    SectionHeight: 200,
                    MismatchMinY: 20,
                    MismatchMaxY: 24)
            ]);

        var json = CloneJson.SerializeIndented(report);

        Assert.Contains("\"buildPassed\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"configPath\": \"site.yaml\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"BuildPassed\"", json, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine + "  \"summary\": {", json, StringComparison.Ordinal);
    }
}
