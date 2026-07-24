using System.Diagnostics;
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

namespace Bukit.Engine.Tests;

public sealed class ImageProcessingPluginTests
{
    [Fact]
    public void AfterBuild_NotEnabled_DoesNothing()
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

            new ImageProcessingPlugin(config).AfterBuild(ctx);
            Assert.False(ctx.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void AfterBuild_NoAssetsDir_DoesNothing()
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

            new ImageProcessingPlugin(config).AfterBuild(ctx);
            Assert.False(ctx.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void AfterBuild_NoImageTool_LogsWarningGracefully()
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

            new ImageProcessingPlugin(config).AfterBuild(ctx);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void AfterBuild_OnlyProcessesImageExtensions()
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

            new ImageProcessingPlugin(config).AfterBuild(ctx);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    private static string GetTempDir() => Path.Combine(Path.GetTempPath(), "bukit_img_test_" + Guid.NewGuid().ToString("N"));

    private static AppConfig CreateConfig(ImageOptimizationConfig images) => new()
    {
        Site = new SiteConfig { Name = "t", Title = "t" },
        Content = TestContent.Markdown(),
        Theme = new ThemeConfig { Images = images }
    };
}
