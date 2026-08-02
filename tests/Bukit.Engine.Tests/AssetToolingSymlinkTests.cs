using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;
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
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("This probe uses a temporary Unix executable to isolate image tool discovery.");
        }

        var root = CreateFixture(out var assetsDir, out var externalDir);
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            File.WriteAllText(Path.Combine(assetsDir, "local.jpg"), "local");
            File.WriteAllText(Path.Combine(externalDir, "secret.jpg"), "secret");
            CreateDirectorySymlinkOrSkip(Path.Combine(assetsDir, "linked-external"), externalDir);
            var toolDir = Path.Combine(root, "tools");
            Directory.CreateDirectory(toolDir);
            var toolPath = Path.Combine(toolDir, "magick");
            File.WriteAllText(toolPath, """
                #!/bin/sh
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                printf resized > "$last"
                """);
            File.SetUnixFileMode(
                toolPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable(
                "PATH",
                string.IsNullOrEmpty(originalPath) ? toolDir : toolDir + Path.PathSeparator + originalPath);
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

            var srcsets = Assert.IsType<Dictionary<string, object>>(context.Data["__image_srcsets"]);
            Assert.Contains("local.jpg", srcsets.Keys);
            Assert.DoesNotContain(srcsets.Keys, key => key.Contains("linked-external", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
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
