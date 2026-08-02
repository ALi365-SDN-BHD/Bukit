using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class ImageProcessingPluginTests
{
    [Fact]
    public async Task AfterBuild_NotEnabled_DoesNothing()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = false });
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            await new ImageProcessingPlugin(config).AfterBuildAsync(ctx);
            Assert.False(ctx.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_NoAssetsDir_DoesNothing()
    {
        var outDir = GetTempDir();
        try
        {
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true });
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            await new ImageProcessingPlugin(config).AfterBuildAsync(ctx);
            Assert.False(ctx.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_NoImageTool_LogsWarningGracefully()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllText(Path.Combine(assetsDir, "test.jpg"), "fake-jpeg");

            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 768 } });
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            await new ImageProcessingPlugin(config).AfterBuildAsync(ctx);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_OnlyProcessesImageExtensions()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllText(Path.Combine(assetsDir, "doc.pdf"), "fake-pdf");
            File.WriteAllText(Path.Combine(assetsDir, "style.css"), "fake-css");

            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true });
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            await new ImageProcessingPlugin(config).AfterBuildAsync(ctx);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CancellationKillsChildAndRemovesPartialOutput()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalMarker = Environment.GetEnvironmentVariable("BUKIT_IMAGE_TEST_MARKER");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            File.WriteAllText(Path.Combine(assetsDir, "test.jpg"), "fake-jpeg");
            var marker = Path.Combine(outDir, "late-marker");
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                printf partial > "$last"
                ( sleep 1; printf late > "$BUKIT_IMAGE_TEST_MARKER" ) &
                sleep 5
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TEST_MARKER", marker);
            var context = CreateContext(outDir);
            var plugin = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            using var cancellation = new CancellationTokenSource();
            var buildTask = plugin.AfterBuildAsync(context, cancellation.Token);
            await WaitUntilAsync(
                () => Directory.EnumerateFiles(assetsDir, ".test-480w.bukit-*.jpg").Any(),
                TimeSpan.FromSeconds(2));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                buildTask);

            await Task.Delay(1200);
            Assert.False(File.Exists(Path.Combine(assetsDir, "test-480w.jpg")));
            Assert.False(File.Exists(marker));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TEST_MARKER", originalMarker);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_DoesNotProcessGeneratedSizedImageAsSource()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            File.WriteAllText(Path.Combine(assetsDir, "photo.jpg"), "original");
            File.WriteAllText(Path.Combine(assetsDir, "photo-480w.jpg"), "existing-sized");
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                printf resized > "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-480w-480w.jpg")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_WhenOnlyOneResizeSucceeds_ProjectsOnlyExistingVariant()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            File.WriteAllText(Path.Combine(assetsDir, "photo.jpg"), "original");
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                case "$*" in *-768w*) exit 1;; esac
                for last in "$@"; do :; done
                printf resized > "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 768 } }))
                .AfterBuildAsync(context);

            var images = Assert.IsType<Dictionary<string, object>>(context.Data["__image_srcsets"]);
            var photo = Assert.IsType<Dictionary<string, object>>(images["photo.jpg"]);
            Assert.Equal("/assets/photo-480w.jpg 480w", photo["srcset"]);
            Assert.Equal(new[] { 480 }, Assert.IsType<int[]>(photo["sizes"]));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-768w.jpg")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_WhenNoResizeSucceeds_OmitsImageSrcsetProjection()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            File.WriteAllText(Path.Combine(assetsDir, "photo.jpg"), "original");
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                exit 1
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(context);

            Assert.False(context.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceImageChanged_RegeneratesVariant()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                cat "$1" > "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            // Round 1: create source and generate variant
            var sourceFile = Path.Combine(assetsDir, "photo.jpg");
            File.WriteAllText(sourceFile, "original-content");
            var plugin = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            await plugin.AfterBuildAsync(CreateContext(outDir));

            var variantFile = Path.Combine(assetsDir, "photo-480w.jpg");
            Assert.True(File.Exists(variantFile));
            var v1Content = File.ReadAllText(variantFile);

            // Round 2: update source (ensure newer mtime)
            await Task.Delay(50);
            File.WriteAllText(sourceFile, "updated-content");
            var plugin2 = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            await plugin2.AfterBuildAsync(CreateContext(outDir));

            var v2Content = File.ReadAllText(variantFile);
            Assert.NotEqual(v1Content, v2Content);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SizeConfigReduced_DeletesStaleVariants()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                printf resized > "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            // Round 1: generate with sizes [480, 768]
            File.WriteAllText(Path.Combine(assetsDir, "photo.jpg"), "original");
            var plugin1 = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 768 } }));
            await plugin1.AfterBuildAsync(CreateContext(outDir));

            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-768w.jpg")));

            // Round 2: reduce sizes to [480] only
            var plugin2 = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            await plugin2.AfterBuildAsync(CreateContext(outDir));

            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-768w.jpg")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    private static string GetTempDir() => Path.Combine(Path.GetTempPath(), "bukit_img_test_" + Guid.NewGuid().ToString("N"));

    private static BuildContext CreateContext(string outDir) => new()
    {
        RootDir = outDir,
        OutputDir = outDir,
        BaseUrl = "/",
        LayoutsDir = outDir,
        RoutedDocuments = Array.Empty<RoutedContentDocument>(),
        Logger = new ConsoleLogger(LogLevel.Error)
    };

    private static void WriteTool(string directory, string name, string body)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void RequireUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("This process-tree probe uses temporary Unix executables.");
        }
    }

    private static string PrependPath(string directory, string? originalPath) =>
        string.IsNullOrEmpty(originalPath) ? directory : directory + Path.PathSeparator + originalPath;

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Timed out waiting for the fake image tool to create its partial output.");
            }

            await Task.Delay(20);
        }
    }

    private static AppConfig CreateConfig(ImageOptimizationConfig images) => new()
    {
        Site = new SiteConfig { Name = "t", Title = "t" },
        Content = TestContent.Markdown(),
        Theme = new ThemeConfig { Images = images }
    };
}
