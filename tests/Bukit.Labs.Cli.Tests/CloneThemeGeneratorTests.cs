using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneThemeGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public CloneThemeGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-labs-clone-theme-generator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void WriteTo_WithFullScopeAndBehaviors_WritesOptionalFilesAndSummaries()
    {
        var summary = CloneThemeGenerator.WriteTo(
            _tempDir,
            "clone-theme",
            new CloneTokens
            {
                Primary = "#123456",
                Accent = "#abcdef"
            },
            new CloneLayoutInfo
            {
                SiteTitle = "Acme",
                HeroHeading = "Ship faster",
                NavLinks =
                [
                    new NavLinkInfo { Label = "Docs", Url = "/docs" }
                ],
                FooterLinks =
                [
                    new FooterLinkInfo { Label = "GitHub", Url = "https://github.com/acme/docs" }
                ],
                ExtraSections =
                [
                    new SectionInfo
                    {
                        Heading = "Gallery",
                        ContentHtml = "<p>Preview</p>"
                    }
                ]
            },
            brand: "Acme",
            behaviors: new CloneBehaviors
            {
                StickyHeader = true,
                DarkModeToggle = true,
                MobileHamburger = true,
                HasModal = true,
                HasDropdown = true,
                HasTabs = true
            },
            icons:
            [
                new CloneIcon { Name = "brand icon?", Svg = "<svg />" },
                new CloneIcon { Name = "blank", Svg = "" }
            ],
            assets:
            [
                new CloneAsset { Type = "image", Src = "https://cdn.example.com/hero.png" }
            ],
            templateScope: TemplateScope.Full,
            includePageTemplate: true);

        Assert.Equal(20, summary.FileCount);
        Assert.Equal(6, summary.BehaviorCount);
        Assert.Equal(1, summary.IconCount);
        Assert.Equal(1, summary.AssetCount);
        Assert.Equal(1, summary.SectionCount);
        Assert.Empty(summary.Warnings);

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "assets", "behaviors.js")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "assets", "icons", "brand_icon_.svg")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "assets", "images")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "pages", "page.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "pages", "post.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "pages", "list.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "bukit.templates.yaml")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "partials", "modal.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "partials", "dropdown.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "partials", "tabs.html")));

        var themeYaml = File.ReadAllText(Path.Combine(_tempDir, "themes", "clone-theme", "theme.yaml"));
        Assert.Contains("tags: [cloned, dark-mode, sticky-header, responsive]", themeYaml, StringComparison.Ordinal);
        Assert.Contains("default: \"#123456\"", themeYaml, StringComparison.Ordinal);
        Assert.Contains("default: \"#abcdef\"", themeYaml, StringComparison.Ordinal);

        var header = File.ReadAllText(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "partials", "header.html"));
        Assert.Contains("Acme", header, StringComparison.Ordinal);
        Assert.Contains("{{ base_url }}/docs", header, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithBareScope_WritesBareTemplateCapabilitiesAndSkipsPostList()
    {
        var summary = CloneThemeGenerator.WriteTo(
            _tempDir,
            "clone-theme",
            CloneTokens.Default,
            CloneLayoutInfo.Default,
            templateScope: TemplateScope.Bare,
            includePageTemplate: true);

        Assert.Equal(14, summary.FileCount);
        Assert.Equal(0, summary.BehaviorCount);
        Assert.Equal(0, summary.IconCount);
        Assert.Equal(0, summary.AssetCount);
        Assert.Equal(0, summary.SectionCount);

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "pages", "page.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "pages", "post.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "pages", "list.html")));

        var capabilities = File.ReadAllText(Path.Combine(_tempDir, "themes", "clone-theme", "layouts", "bukit.templates.yaml"));
        Assert.Contains("pages/index.html:", capabilities, StringComparison.Ordinal);
        Assert.Contains("supports_search_snippets: true", capabilities, StringComparison.Ordinal);
        Assert.DoesNotContain("pages/post.html", capabilities, StringComparison.Ordinal);
    }
}
