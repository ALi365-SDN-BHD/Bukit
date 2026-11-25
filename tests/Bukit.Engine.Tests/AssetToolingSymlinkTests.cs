using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;
using SkiaSharp;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class AssetToolingSymlinkTests
{
    [Fact]
    public async Task ImageOptimizer_DoesNotObserveImagesThroughDirectorySymlink()
    {
        var root = CreateFixture(out var assetsDir, out var externalDir);
        try
        {
            File.WriteAllText(Path.Combine(externalDir, "secret.jpg"), "secret");
            CreateDirectorySymlinkOrSkip(Path.Combine(assetsDir, "linked-external"), externalDir);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await ImageOptimizer.OptimizeIfEnabled(
                assetsDir,
                new ImageOptimizationConfig { Enabled = true, Formats = new[] { "webp" } },
                new ConsoleLogger(LogLevel.Error),
                cancellation.Token);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImageProcessingPlugin_DoesNotIndexImagesThroughDirectorySymlink()
    {
        var root = CreateFixture(out var assetsDir, out var externalDir);
        try
        {
            // Resized variants are planned for media images only, so scan the media dir.
            var mediaDir = Path.Combine(assetsDir, "uploads");
            Directory.CreateDirectory(mediaDir);
            WriteValidImage(Path.Combine(mediaDir, "local.jpg"), "local");
            File.WriteAllText(Path.Combine(externalDir, "secret.jpg"), "secret");
            CreateDirectorySymlinkOrSkip(Path.Combine(mediaDir, "linked-external"), externalDir);
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "Test" },
                Content = TestContent.Markdown(),
                Theme = new ThemeConfig
                {
                    Images = new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }
                }
            };
            var context = new BuildContext
            {
                RootDir = root,
                OutputDir = root,
                BaseUrl = "/",
                LayoutsDir = root,
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.Contains(outputs, output => output.Path == "assets/uploads/local-480w.jpg");
            Assert.DoesNotContain(outputs, output => output.Path.Contains("linked-external", StringComparison.Ordinal));
            Assert.Equal(["secret.jpg"], Directory.GetFiles(externalDir).Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteValidImage(string path, string seed)
    {
        var hash = 0;
        foreach (var character in seed)
        {
            hash = unchecked(hash * 31 + character);
        }

        using var image = new SKBitmap(1600, 2);
        image.SetPixel(0, 0, new SKColor(
            (byte)(hash & 0xFF), (byte)((hash >> 8) & 0xFF), (byte)((hash >> 16) & 0xFF)));
        image.SetPixel(1599, 1, SKColors.White);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static string CreateFixture(out string assetsDir, out string externalDir)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-asset-symlink-" + Guid.NewGuid().ToString("N"));
        assetsDir = Path.Combine(root, "assets");
        externalDir = Path.Combine(root, "external");
        Directory.CreateDirectory(assetsDir);
        Directory.CreateDirectory(externalDir);
        return root;
    }

    private static void CreateDirectorySymlinkOrSkip(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
        }
    }
}
