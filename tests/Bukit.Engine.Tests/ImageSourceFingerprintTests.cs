using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ImageSourceFingerprintTests
{
    [Fact]
    public void Compute_ChangesWhenImageContentChangesWithSameLengthAndTimestamp()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-image-fingerprint-" + Guid.NewGuid().ToString("N"));
        var media = Path.Combine(root, "media");
        Directory.CreateDirectory(media);
        var image = Path.Combine(media, "photo.png");
        try
        {
            var timestamp = DateTime.UnixEpoch.AddDays(1);
            File.WriteAllText(image, "AAAA");
            File.SetLastWriteTimeUtc(image, timestamp);
            var config = Config();
            var first = ImageSourceFingerprint.Compute(config, root, Path.Combine(root, ".cache", "media"), null, null, null, null, new ConsoleLogger(LogLevel.Error));

            File.WriteAllText(image, "BBBB");
            File.SetLastWriteTimeUtc(image, timestamp);
            var second = ImageSourceFingerprint.Compute(config, root, Path.Combine(root, ".cache", "media"), null, null, null, null, new ConsoleLogger(LogLevel.Error));

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first, second);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compute_ChangesWhenThemeAssetImageChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-image-fingerprint-assets-" + Guid.NewGuid().ToString("N"));
        var assets = Path.Combine(root, "assets");
        Directory.CreateDirectory(assets);
        var image = Path.Combine(assets, "photo.png");
        try
        {
            var config = Config();
            var cache = Path.Combine(root, ".cache", "media");
            File.WriteAllText(image, "asset-one");
            var first = ImageSourceFingerprint.Compute(config, root, cache, assets, null, null, null, new ConsoleLogger(LogLevel.Error));

            File.WriteAllText(image, "asset-two");
            var second = ImageSourceFingerprint.Compute(config, root, cache, assets, null, null, null, new ConsoleLogger(LogLevel.Error));

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first, second);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Compute_ChangesWhenStaticAssetImageChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-image-fingerprint-static-" + Guid.NewGuid().ToString("N"));
        var staticDir = Path.Combine(root, "static");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets"));
        var image = Path.Combine(staticDir, "assets", "photo.png");
        try
        {
            var config = Config();
            var cache = Path.Combine(root, ".cache", "media");
            File.WriteAllText(image, "static-one");
            var first = ImageSourceFingerprint.Compute(config, root, cache, null, null, staticDir, null, new ConsoleLogger(LogLevel.Error));

            File.WriteAllText(image, "static-two");
            var second = ImageSourceFingerprint.Compute(config, root, cache, null, null, staticDir, null, new ConsoleLogger(LogLevel.Error));

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first, second);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig { Name = "image", Title = "Image" },
            Content = TestContent.Markdown() with
            {
                Media = new MediaConfig
                {
                    DownloadToLocal = false,
                    DownloadDir = "media",
                    UrlBase = "/assets/uploads"
                }
            },
            Build = new BuildConfig { Output = "dist" },
            Theme = new ThemeConfig
            {
                Images = new ImageOptimizationConfig { Enabled = true, Sizes = [480] }
            }
        };
}
