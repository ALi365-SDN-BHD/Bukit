using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneInputLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public CloneInputLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-labs-clone-input-loader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LoadTokensAsync_WhenFileMissing_ReturnsErrorCodeTwo()
    {
        var (result, stdErr) = await CaptureStdErrAsync(() => CloneInputLoader.LoadTokensAsync(Path.Combine(_tempDir, "missing-tokens.json")));

        Assert.Equal(2, result.errorCode);
        Assert.Contains("Tokens file not found:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadTokensAsync_WhenJsonInvalid_ReturnsErrorCodeTwo()
    {
        var path = WriteFile("tokens.json", "{");

        var (result, stdErr) = await CaptureStdErrAsync(() => CloneInputLoader.LoadTokensAsync(path));

        Assert.Equal(2, result.errorCode);
        Assert.Contains("Failed to parse tokens file:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadTokensAsync_WhenJsonValid_ReturnsTokens()
    {
        var path = WriteFile(
            "tokens.json",
            """
            {
              "tokens": {
                "primary": "#123456",
                "googleFontsUrl": "https://fonts.example.com/inter",
                "externalJsUrls": ["https://cdn.example.com/app.js"]
              }
            }
            """);

        var (tokens, errorCode) = await CloneInputLoader.LoadTokensAsync(path);

        Assert.Equal(0, errorCode);
        Assert.Equal("#123456", tokens.Primary);
        Assert.Equal("https://fonts.example.com/inter", tokens.GoogleFontsUrl);
        Assert.Equal("https://cdn.example.com/app.js", Assert.Single(tokens.ExternalJsUrls!));
    }

    [Fact]
    public async Task LoadLayoutAsync_WhenPathMissing_ReturnsErrorCodeTwo()
    {
        var (result, stdErr) = await CaptureStdErrAsync(() => CloneInputLoader.LoadLayoutAsync(Path.Combine(_tempDir, "missing-layout.json")));

        Assert.Equal(2, result.errorCode);
        Assert.Contains("Layout file not found:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadLayoutAsync_WhenPathNull_ReturnsDefaultLayout()
    {
        var (layout, errorCode) = await CloneInputLoader.LoadLayoutAsync(null);

        Assert.Equal(0, errorCode);
        Assert.Empty(layout.NavLinks);
        Assert.Empty(layout.FooterLinks);
        Assert.Empty(layout.ExtraSections);
    }

    [Fact]
    public async Task LoadLayoutAsync_WhenJsonValid_ReturnsLayout()
    {
        var path = WriteFile(
            "layout.json",
            """
            {
              "siteTitle": "Acme",
              "heroHeading": "Ship faster",
              "navLinks": [
                {
                  "label": "Docs",
                  "url": "/docs"
                }
              ],
              "footerLinks": [
                {
                  "label": "GitHub",
                  "url": "https://github.com/acme/docs"
                }
              ]
            }
            """);

        var (layout, errorCode) = await CloneInputLoader.LoadLayoutAsync(path);

        Assert.Equal(0, errorCode);
        Assert.Equal("Acme", layout.SiteTitle);
        Assert.Equal("Ship faster", layout.HeroHeading);
        Assert.Equal("Docs", Assert.Single(layout.NavLinks).Label);
        Assert.Equal("GitHub", Assert.Single(layout.FooterLinks).Label);
    }

    [Fact]
    public async Task LoadBehaviorsAsync_WhenFileMissing_ReturnsErrorCodeTwo()
    {
        var (result, stdErr) = await CaptureStdErrAsync(() => CloneInputLoader.LoadBehaviorsAsync(Path.Combine(_tempDir, "missing-behaviors.json")));

        Assert.Equal(2, result.errorCode);
        Assert.Contains("Behaviors file not found:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadBehaviorsAsync_WhenPathNull_ReturnsDefaultBehaviors()
    {
        var (behaviors, errorCode) = await CloneInputLoader.LoadBehaviorsAsync(null);

        Assert.Equal(0, errorCode);
        Assert.False(behaviors.HasAnyCssBehavior);
        Assert.False(behaviors.HasAnyJsBehavior);
    }

    [Fact]
    public async Task LoadBehaviorsAsync_WhenJsonValid_ReturnsBehaviorFlags()
    {
        var path = WriteFile(
            "behaviors.json",
            """
            {
              "stickyHeader": true,
              "mobileHamburger": true,
              "useLenis": true
            }
            """);

        var (behaviors, errorCode) = await CloneInputLoader.LoadBehaviorsAsync(path);

        Assert.Equal(0, errorCode);
        Assert.True(behaviors.StickyHeader);
        Assert.True(behaviors.MobileHamburger);
        Assert.True(behaviors.UseLenis);
    }

    [Fact]
    public async Task LoadIconsAsync_WhenJsonInvalid_ReturnsErrorCodeTwo()
    {
        var path = WriteFile("icons.json", "not-json");

        var (result, _) = await CaptureStdErrAsync(() => CloneInputLoader.LoadIconsAsync(path));

        Assert.Equal(2, result.errorCode);
    }

    [Fact]
    public async Task LoadIconsAsync_WhenFileMissing_ReturnsErrorCodeTwo()
    {
        var (result, stdErr) = await CaptureStdErrAsync(() => CloneInputLoader.LoadIconsAsync(Path.Combine(_tempDir, "missing-icons.json")));

        Assert.Equal(2, result.errorCode);
        Assert.Contains("Icons file not found:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadIconsAsync_WhenPathNull_ReturnsEmptyList()
    {
        var (icons, errorCode) = await CloneInputLoader.LoadIconsAsync(null);

        Assert.Equal(0, errorCode);
        Assert.Empty(icons);
    }

    [Fact]
    public async Task LoadIconsAsync_WhenJsonValid_ReturnsIcons()
    {
        var path = WriteFile(
            "icons.json",
            """
            [
              {
                "name": "logo",
                "svg": "<svg />"
              }
            ]
            """);

        var (icons, errorCode) = await CloneInputLoader.LoadIconsAsync(path);

        Assert.Equal(0, errorCode);
        var icon = Assert.Single(icons);
        Assert.Equal("logo", icon.Name);
        Assert.Equal("<svg />", icon.Svg);
    }

    [Fact]
    public async Task LoadAssetsAsync_WhenPathNull_ReturnsEmptyList()
    {
        var (assets, errorCode) = await CloneInputLoader.LoadAssetsAsync(null);

        Assert.Equal(0, errorCode);
        Assert.Empty(assets);
    }

    [Fact]
    public async Task LoadAssetsAsync_WhenFileMissing_ReturnsErrorCodeTwo()
    {
        var (result, stdErr) = await CaptureStdErrAsync(() => CloneInputLoader.LoadAssetsAsync(Path.Combine(_tempDir, "missing-assets.json")));

        Assert.Equal(2, result.errorCode);
        Assert.Contains("Assets file not found:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAssetsAsync_WhenJsonInvalid_ReturnsErrorCodeTwo()
    {
        var path = WriteFile("assets.json", "{");

        var (result, stdErr) = await CaptureStdErrAsync(() => CloneInputLoader.LoadAssetsAsync(path));

        Assert.Equal(2, result.errorCode);
        Assert.Contains("Failed to parse assets file:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAssetsAsync_WhenJsonValid_ReturnsAssets()
    {
        var path = WriteFile(
            "assets.json",
            """
            [
              {
                "type": "image",
                "src": "https://cdn.example.com/hero.png",
                "localPath": "/assets/images/hero.png"
              }
            ]
            """);

        var (assets, errorCode) = await CloneInputLoader.LoadAssetsAsync(path);

        Assert.Equal(0, errorCode);
        var asset = Assert.Single(assets);
        Assert.Equal("image", asset.Type);
        Assert.Equal("https://cdn.example.com/hero.png", asset.Src);
        Assert.Equal("/assets/images/hero.png", asset.LocalPath);
    }

    [Fact]
    public async Task LoadPageAsync_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_tempDir, "missing-page.json");

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => CloneInputLoader.LoadPageAsync(path));

        Assert.Equal(path, ex.FileName);
    }

    [Fact]
    public async Task LoadPageAsync_WhenJsonValid_ReturnsPage()
    {
        var path = WriteFile(
            "page.json",
            """
            {
              "title": "Landing",
              "summary": "Fast launch",
              "seo": {
                "title": "Landing SEO"
              }
            }
            """);

        var page = await CloneInputLoader.LoadPageAsync(path);

        Assert.Equal("Landing", page.Title);
        Assert.Equal("Fast launch", page.Summary);
        Assert.Equal("Landing SEO", page.Seo!.Title);
    }

    [Fact]
    public async Task LoadSectionsAsync_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_tempDir, "missing-sections.json");

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => CloneInputLoader.LoadSectionsAsync(path));

        Assert.Equal(path, ex.FileName);
    }

    [Fact]
    public async Task LoadSectionsAsync_WhenJsonValid_ReturnsSections()
    {
        var path = WriteFile(
            "sections.json",
            """
            {
              "sections": [
                {
                  "type": "hero",
                  "title": "Hero",
                  "imageUrls": ["/img/hero.png"]
                }
              ]
            }
            """);

        var sections = await CloneInputLoader.LoadSectionsAsync(path);

        var section = Assert.Single(sections);
        Assert.Equal("hero", section.Type);
        Assert.Equal("Hero", section.Title);
        Assert.Equal("/img/hero.png", Assert.Single(section.ImageUrls));
    }

    private string WriteFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content.Replace("\r\n", "\n"));
        return path;
    }

    private static async Task<(T Result, string StdErr)> CaptureStdErrAsync<T>(Func<Task<T>> action)
    {
        using var writer = new StringWriter();
        var original = Console.Error;

        try
        {
            Console.SetError(writer);
            var result = await action();
            return (result, writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
