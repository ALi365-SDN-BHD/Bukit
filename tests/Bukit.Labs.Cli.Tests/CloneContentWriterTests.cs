using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneContentWriterTests : IDisposable
{
    private readonly string _tempDir;

    public CloneContentWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-labs-clone-content-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void WriteTo_WithBareScope_WritesContentThemeAndResearchFiles()
    {
        var result = CloneContentWriter.WriteTo(
            _tempDir,
            "clone-kit",
            new CloneTokens
            {
                Primary = "#123456",
                Accent = "#abcdef",
                GoogleFontsUrl = "https://fonts.googleapis.com/css2?family=Inter:wght@400;700&display=swap"
            },
            new ClonePageInfo
            {
                Title = "Landing",
                Url = "https://example.com",
                Summary = "Fast launch",
                BodyMarkdown = "# Welcome"
            },
            [
                new CloneSectionInfo
                {
                    Type = "hero",
                    Title = "Hero",
                    ContentHtml = "<p><img src=\"https://cdn.example.com/hero.png\" /></p>",
                    Buttons =
                    [
                        new CloneSectionButton
                        {
                            Label = "Get started",
                            Url = "/start"
                        }
                    ]
                }
            ],
            [
                new CloneAsset
                {
                    Type = "image",
                    Src = "https://cdn.example.com/hero.png"
                }
            ],
            new CloneBehaviors
            {
                MobileHamburger = true,
                DarkModeToggle = true,
                HasModal = true,
                HasDropdown = true,
                HasTabs = true,
                UseLenis = true
            },
            "Acme",
            templateScope: TemplateScope.Bare,
            includePageTemplate: false);

        Assert.Equal(24, result.ThemeFileCount);
        Assert.Equal(1, result.ContentFileCount);
        Assert.Equal(2, result.DataFileCount);
        Assert.Equal(1, result.SectionCount);
        Assert.False(result.ConfigUpdated);
        Assert.Contains("site.yaml not found; skipped content source configuration.", result.Warnings);

        Assert.True(File.Exists(Path.Combine(_tempDir, "content", "index.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "data", "clone-001-hero.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "data", "clone-assets.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "assets", "behaviors.js")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "partials", "modal.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "partials", "dropdown.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "partials", "tabs.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "pages", "page.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "pages", "post.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "pages", "list.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "bukit.templates.yaml")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "docs", "research", "DESIGN_TOKENS.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "docs", "research", "components", "001-hero.spec.md")));

        var themeYaml = File.ReadAllText(Path.Combine(_tempDir, "themes", "clone-kit", "theme.yaml"));
        Assert.Contains("tags: [cloned, content-data, dark-mode, responsive]", themeYaml, StringComparison.Ordinal);

        var content = File.ReadAllText(Path.Combine(_tempDir, "content", "index.md"));
        Assert.Contains("source_url: 'https://example.com'", content, StringComparison.Ordinal);
        Assert.Contains("summary: 'Fast launch'", content, StringComparison.Ordinal);
        Assert.Contains("# Welcome", content, StringComparison.Ordinal);

        var data = File.ReadAllText(Path.Combine(_tempDir, "data", "clone-001-hero.md"));
        Assert.Contains("/assets/images/hero.png", data, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_WithFullScopeAndSiteYaml_WritesTemplatesAndUpdatesConfig()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "site.yaml"),
            """
            content:
              provider: notion
            theme:
              name: old-theme
            """);

        var result = CloneContentWriter.WriteTo(
            _tempDir,
            "clone-kit",
            new CloneTokens
            {
                Primary = "#222222",
                Accent = "#555555"
            },
            new ClonePageInfo
            {
                Title = "Docs",
                Seo = new ClonePageSeo
                {
                    Description = "SEO summary",
                    Image = "/og.png"
                }
            },
            [],
            [],
            behaviors: null,
            brand: "Acme",
            templateScope: TemplateScope.Full,
            includePageTemplate: true);

        Assert.Equal(24, result.ThemeFileCount);
        Assert.Equal(1, result.ContentFileCount);
        Assert.Equal(1, result.DataFileCount);
        Assert.Equal(0, result.SectionCount);
        Assert.True(result.ConfigUpdated);
        Assert.Empty(result.Warnings);

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "pages", "page.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "pages", "post.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "pages", "list.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "clone-kit", "layouts", "bukit.templates.yaml")));

        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "site.yaml"));
        Assert.Contains("provider: sources", siteYaml, StringComparison.Ordinal);
        Assert.Contains("name: clone-kit", siteYaml, StringComparison.Ordinal);
        Assert.Contains("brand: Acme", siteYaml, StringComparison.Ordinal);
        Assert.Contains("footer_text: Acme", siteYaml, StringComparison.Ordinal);
        Assert.Contains("primary_color: '#222222'", siteYaml, StringComparison.Ordinal);
        Assert.Contains("accent_color: '#555555'", siteYaml, StringComparison.Ordinal);

        var content = File.ReadAllText(Path.Combine(_tempDir, "content", "index.md"));
        Assert.Contains("summary: 'SEO summary'", content, StringComparison.Ordinal);
        Assert.Contains("og_image: '/og.png'", content, StringComparison.Ordinal);
        Assert.Contains("# Docs", content, StringComparison.Ordinal);
    }

    [Fact]
    public void HelperWrappers_ExposeEscapingAndAssetDelegation()
    {
        var asset = new CloneAsset
        {
            Type = "icon",
            Src = "https://cdn.example.com/logo.svg"
        };

        Assert.Equal("&lt;tag&gt;&amp;", CloneContentWriter.Html("<tag>&"));
        Assert.Equal("&quot;a&quot;&amp;b", CloneContentWriter.HtmlAttr("\"a\"&b"));
        Assert.Equal("clone-001-hero", CloneContentWriter.SectionDataKey(new CloneSectionInfo { Type = "hero" }, 0));
        Assert.Equal("001-clone-hero.spec.md", CloneContentWriter.SectionSpecFileName(new CloneSectionInfo { Id = "Clone Hero" }, 0));
        Assert.Equal("icons", CloneContentWriter.AssetSubdir("icon"));
        Assert.Equal("/assets/icons/logo.svg", CloneContentWriter.LocalAssetPath(asset, 1));
        Assert.Equal("logo.svg", CloneContentWriter.AssetFileName(asset, 1));
        Assert.Equal("/assets/icons/logo.svg", CloneContentWriter.BuildAssetMap([asset])["https://cdn.example.com/logo.svg"]);
    }
}
