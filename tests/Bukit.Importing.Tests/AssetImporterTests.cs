using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class AssetImporterTests : IDisposable
{
    private readonly string _tempDir;

    public AssetImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-asset-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    private static DiscoveredPage MakePageWithAssets(params string[] assets)
    {
        return new DiscoveredPage
        {
            FilePath = "/test/page.html",
            RelativePath = "page.html",
            Slug = "page",
            Type = PageType.Page,
            Title = "Test",
            FullHtml = "<html></html>",
            BodyContent = "",
            BodyOpening = "",
            UniqueBody = "",
            BodyClosing = "",
            AssetPaths = assets.ToList()
        };
    }

    [Fact]
    public void Import_ImageAsset_CopiedToAssetsDir()
    {
        var assetPath = Path.Combine(_tempDir, "img", "hero.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, "fake image");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage> { MakePageWithAssets("img/hero.jpg") };

        var result = AssetImporter.Import(options, pages);

        Assert.Equal(1, result.Count);
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "test-theme", "assets", "img", "hero.jpg")));
    }

    [Fact]
    public void Import_SensitiveFile_Rejected()
    {
        var envPath = Path.Combine(_tempDir, ".env");
        File.WriteAllText(envPath, "SECRET=xxx");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage> { MakePageWithAssets(".env") };

        var result = AssetImporter.Import(options, pages);

        Assert.Equal(0, result.Count);
        Assert.Contains(result.Warnings, w => w.Contains(".env"));
    }

    [Fact]
    public void Import_PathTraversal_Rejected()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage> { MakePageWithAssets("../etc/passwd") };

        var result = AssetImporter.Import(options, pages);

        Assert.Equal(0, result.Count);
        Assert.Contains(result.Warnings, w => w.Contains("路径穿越"));
    }

    [Fact]
    public void Import_KeyFile_Rejected()
    {
        var keyPath = Path.Combine(_tempDir, "secret.key");
        File.WriteAllText(keyPath, "key-data");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage> { MakePageWithAssets("secret.key") };

        var result = AssetImporter.Import(options, pages);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Import_CssAsset_CopiedToStaticDir()
    {
        var assetPath = Path.Combine(_tempDir, "css", "style.css");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, "body {}");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage> { MakePageWithAssets("css/style.css") };

        var result = AssetImporter.Import(options, pages);

        Assert.Equal(1, result.Count);
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "test-theme", "static", "css", "style.css")));
    }

    [Fact]
    public void Import_DuplicateAsset_CopiedOnce()
    {
        var assetPath = Path.Combine(_tempDir, "img", "hero.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, "fake image");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage>
        {
            MakePageWithAssets("img/hero.jpg"),
            MakePageWithAssets("img/hero.jpg"),
        };

        var result = AssetImporter.Import(options, pages);

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void Import_ReturnsPathMappings()
    {
        var assetPath = Path.Combine(_tempDir, "img", "logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, "fake png");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-map",
            RootDir = _tempDir
        };

        var pages = new List<DiscoveredPage> { MakePageWithAssets("img/logo.png") };

        var result = AssetImporter.Import(options, pages);

        Assert.True(result.PathMappings.ContainsKey("img/logo.png"));
        Assert.Equal("/img/logo.png", result.PathMappings["img/logo.png"]);
    }
}
