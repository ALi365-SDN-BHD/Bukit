using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Shared;
using SkiaSharp;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ImageSourceFingerprintTests
{
    private static readonly ILogger Logger = new ConsoleLogger(LogLevel.Error);

    [Fact]
    public void Compute_ChangesWhenMediaImageWidthChanges()
    {
        using var site = new TempSite();
        var image = Path.Combine(site.Root, "media", "photo.png");
        WritePng(image, 1200, 10);
        var first = site.Compute();

        WritePng(image, 180, 10);
        var second = site.Compute();

        Assert.NotNull(first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_IgnoresContentChangeThatKeepsWidth()
    {
        // Page markup only depends on the width; the plugin regenerates variant files itself.
        using var site = new TempSite();
        var image = Path.Combine(site.Root, "media", "photo.png");
        WritePng(image, 1200, 10, SKColors.Red);
        var first = site.Compute();

        WritePng(image, 1200, 20, SKColors.Blue);
        var second = site.Compute();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_ChangesWhenImageIsAddedOrRemoved()
    {
        using var site = new TempSite();
        var first = site.Compute();

        var image = Path.Combine(site.Root, "media", "photo.png");
        WritePng(image, 800, 10);
        var added = site.Compute();

        File.Delete(image);
        var removed = site.Compute();

        Assert.NotEqual(first, added);
        Assert.Equal(first, removed);
    }

    [Fact]
    public void Compute_ChangesWhenThemeAssetImageWidthChanges()
    {
        using var site = new TempSite();
        var assets = Path.Combine(site.Root, "theme-assets");
        var image = Path.Combine(assets, "photo.png");
        WritePng(image, 1200, 10);
        var first = site.Compute(assetsDir: assets);

        WritePng(image, 400, 10);
        var second = site.Compute(assetsDir: assets);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_ChangesWhenStaticAssetImageWidthChanges()
    {
        using var site = new TempSite();
        var staticDir = Path.Combine(site.Root, "static");
        var image = Path.Combine(staticDir, "assets", "photo.png");
        WritePng(image, 1200, 10);
        var first = site.Compute(staticDir: staticDir);

        WritePng(image, 400, 10);
        var second = site.Compute(staticDir: staticDir);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_IgnoresStaticImagesThePluginNeverReads()
    {
        using var site = new TempSite();
        var staticDir = Path.Combine(site.Root, "static");
        Directory.CreateDirectory(staticDir);
        var first = site.Compute(staticDir: staticDir);

        WritePng(Path.Combine(staticDir, "images", "logo.png"), 1200, 10);
        var second = site.Compute(staticDir: staticDir);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_ScansStaticMediaUrlBaseOutsideAssets()
    {
        using var site = new TempSite(urlBase: "/media");
        var staticDir = Path.Combine(site.Root, "static");
        var image = Path.Combine(staticDir, "media", "photo.png");
        WritePng(image, 1200, 10);
        var first = site.Compute(staticDir: staticDir);

        WritePng(image, 300, 10);
        var second = site.Compute(staticDir: staticDir);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_ReturnsNullWhenImageProcessingIsDisabled()
    {
        using var site = new TempSite(imagesEnabled: false);

        Assert.Null(site.Compute());
    }

    private static void WritePng(string path, int width, int height, SKColor? color = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color ?? SKColors.White);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        encoded.SaveTo(stream);
    }

    private sealed class TempSite : IDisposable
    {
        private readonly AppConfig _config;

        public TempSite(string urlBase = "/assets/uploads", bool imagesEnabled = true)
        {
            Root = Path.Combine(Path.GetTempPath(), "bukit-image-fingerprint-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "media"));
            _config = new AppConfig
            {
                Site = new SiteConfig { Name = "image", Title = "Image" },
                Content = TestContent.Markdown() with
                {
                    Media = new MediaConfig
                    {
                        DownloadToLocal = false,
                        DownloadDir = "media",
                        UrlBase = urlBase
                    }
                },
                Build = new BuildConfig { Output = "dist" },
                Theme = new ThemeConfig
                {
                    Images = new ImageOptimizationConfig { Enabled = imagesEnabled, Sizes = [480] }
                }
            };
        }

        public string Root { get; }

        public string? Compute(string? assetsDir = null, string? staticDir = null)
            => ImageSourceFingerprint.Compute(
                _config,
                Root,
                Path.Combine(Root, ".cache", "media"),
                assetsDir,
                null,
                staticDir,
                null,
                Logger);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
