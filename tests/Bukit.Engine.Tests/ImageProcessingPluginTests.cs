using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using SixLabors.ImageSharp;
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
            WriteValidImage(Path.Combine(assetsDir, "test.jpg"), "fake-jpeg");

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
            WriteValidImage(Path.Combine(assetsDir, "test.jpg"), "fake-jpeg");
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "original");
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "original");
            var validImage = Path.Combine(toolDir, "valid.jpg");
            WriteValidImage(validImage, "resized");
            WriteTool(toolDir, "magick", $"""
                if [ "$1" = "--version" ]; then exit 0; fi
                case "$*" in *-768w*) exit 1;; esac
                for last in "$@"; do :; done
                cp '{EscapeSingleQuoted(validImage)}' "$last"
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "original");
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
            WriteValidImage(sourceFile, "original-content");
            var plugin = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            await plugin.AfterBuildAsync(CreateContext(outDir));

            var variantFile = Path.Combine(assetsDir, "photo-480w.jpg");
            Assert.True(File.Exists(variantFile));
            var v1Content = File.ReadAllText(variantFile);

            // Round 2: update source (ensure newer mtime)
            await Task.Delay(50);
            WriteValidImage(sourceFile, "updated-content");
            var plugin2 = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            await plugin2.AfterBuildAsync(CreateContextWithPriorImageOwnership(outDir, variantFile));

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
    public async Task AfterBuildAsync_StaleVariantRebuildFails_RemovesVariantAndProjection()
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
            var sourceFile = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(sourceFile, "original");
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                cp "$1" "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));
            var variantFile = Path.Combine(assetsDir, "photo-480w.jpg");
            var freshnessFile = variantFile + ".bukit-freshness.json";
            Assert.True(File.Exists(variantFile));
            Assert.True(File.Exists(freshnessFile));

            WriteValidImage(sourceFile, "changed");
            WriteTool(toolDir, "magick", """
                if [ "$1" = "--version" ]; then exit 0; fi
                exit 1
                """);
            var secondContext = CreateContextWithPriorImageOwnership(outDir, variantFile);

            await new ImageProcessingPlugin(config).AfterBuildAsync(secondContext);

            Assert.False(File.Exists(variantFile));
            Assert.False(File.Exists(freshnessFile));
            Assert.False(secondContext.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceHashChangedWithSameSizeAndMtime_RegeneratesVariant()
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
            var sourceFile = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(sourceFile, "same-metadata");
            await File.AppendAllTextAsync(sourceFile, "A");
            var sourceTimestamp = File.GetLastWriteTimeUtc(sourceFile);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            var variantFile = Path.Combine(assetsDir, "photo-480w.jpg");
            var firstVariant = await File.ReadAllBytesAsync(variantFile);
            var sourceBytes = await File.ReadAllBytesAsync(sourceFile);
            sourceBytes[^1] = (byte)'B';
            await File.WriteAllBytesAsync(sourceFile, sourceBytes);
            File.SetLastWriteTimeUtc(sourceFile, sourceTimestamp);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContextWithPriorImageOwnership(outDir, variantFile));

            Assert.NotEqual(firstVariant, await File.ReadAllBytesAsync(variantFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_TracksVariantAndFreshnessSidecarAsOwnedOutputs()
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
            var validImage = Path.Combine(toolDir, "valid.jpg");
            WriteValidImage(validImage, "tracked");
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            WriteTool(toolDir, "magick", $"""
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                cp '{EscapeSingleQuoted(validImage)}' "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(context);

            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Data["__plugin_outputs"]);
            Assert.Contains(outputs, output => output.Path == "assets/photo-480w.jpg");
            Assert.Contains(outputs, output => output.Path == "assets/photo-480w.jpg.bukit-freshness.json");
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
            var validImage = Path.Combine(toolDir, "valid.jpg");
            WriteValidImage(validImage, "resized");
            WriteTool(toolDir, "magick", $"""
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                cp '{EscapeSingleQuoted(validImage)}' "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            // Round 1: generate with sizes [480, 768]
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "original");
            var plugin1 = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 768 } }));
            await plugin1.AfterBuildAsync(CreateContext(outDir));

            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-768w.jpg")));

            // Round 2: reduce sizes to [480] only
            var plugin2 = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            await plugin2.AfterBuildAsync(CreateContextWithPriorImageOwnership(
                outDir,
                Path.Combine(assetsDir, "photo-480w.jpg"),
                Path.Combine(assetsDir, "photo-768w.jpg")));

            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-768w.jpg")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-768w.jpg.bukit-freshness.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CurrentSizeUserLookalikeWithoutOwnership_IsNeverOverwritten()
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            var userOwnedVariant = Path.Combine(assetsDir, "photo-300w.jpg");
            WriteValidImage(userOwnedVariant, "user-owned");
            var userOwnedBytes = await File.ReadAllBytesAsync(userOwnedVariant);
            var userOwnedSidecar = userOwnedVariant + ".bukit-freshness.json";
            const string userOwnedSidecarContent = """
                { "owner": "user", "size": 300, "format": ".jpg" }
                """;
            await File.WriteAllTextAsync(userOwnedSidecar, userOwnedSidecarContent);
            var generatedImage = Path.Combine(toolDir, "generated.jpg");
            WriteValidImage(generatedImage, "generated");
            WriteTool(toolDir, "magick", $"""
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                cp '{EscapeSingleQuoted(generatedImage)}' "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } }))
                .AfterBuildAsync(context);

            Assert.Equal(userOwnedBytes, await File.ReadAllBytesAsync(userOwnedVariant));
            Assert.Equal(userOwnedSidecarContent, await File.ReadAllTextAsync(userOwnedSidecar));
            Assert.False(context.Data.ContainsKey("__image_srcsets"));
            Assert.False(context.Data.ContainsKey("__plugin_outputs"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_HistoricalSizeUserLookalikeWithoutOwnership_IsNeverDeleted()
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            var userOwnedVariant = Path.Combine(assetsDir, "photo-300w.jpg");
            WriteValidImage(userOwnedVariant, "user-owned");
            var userOwnedBytes = await File.ReadAllBytesAsync(userOwnedVariant);
            var generatedImage = Path.Combine(toolDir, "generated.jpg");
            WriteValidImage(generatedImage, "generated");
            WriteTool(toolDir, "magick", $"""
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                cp '{EscapeSingleQuoted(generatedImage)}' "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            Assert.Equal(userOwnedBytes, await File.ReadAllBytesAsync(userOwnedVariant));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_NoResizeTool_CleansOwnedStaleHistoricalVariant()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            var noToolDir = Path.Combine(outDir, "no-tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            Directory.CreateDirectory(noToolDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            var generatedImage = Path.Combine(toolDir, "generated.jpg");
            WriteValidImage(generatedImage, "generated");
            WriteTool(toolDir, "magick", $"""
                if [ "$1" = "--version" ]; then exit 0; fi
                for last in "$@"; do :; done
                cp '{EscapeSingleQuoted(generatedImage)}' "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 768 } }))
                .AfterBuildAsync(CreateContext(outDir));

            var retained = Path.Combine(assetsDir, "photo-480w.jpg");
            var stale = Path.Combine(assetsDir, "photo-768w.jpg");
            Assert.True(File.Exists(retained));
            Assert.True(File.Exists(stale));
            Assert.True(File.Exists(stale + ".bukit-freshness.json"));
            Environment.SetEnvironmentVariable("PATH", noToolDir);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContextWithPriorImageOwnership(outDir, retained, stale));

            Assert.True(File.Exists(retained));
            Assert.False(File.Exists(stale));
            Assert.False(File.Exists(stale + ".bukit-freshness.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CopiedValidSidecarAcrossDirectories_DoesNotAuthorizeProjection()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var ownedDir = Path.Combine(outDir, "assets", "owned");
            var copiedDir = Path.Combine(outDir, "assets", "copied");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(ownedDir);
            Directory.CreateDirectory(copiedDir);
            Directory.CreateDirectory(toolDir);
            var ownedSource = Path.Combine(ownedDir, "photo.jpg");
            WriteValidImage(ownedSource, "same-source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var ownedVariant = Path.Combine(ownedDir, "photo-300w.jpg");
            var copiedSource = Path.Combine(copiedDir, "photo.jpg");
            var copiedVariant = Path.Combine(copiedDir, "photo-300w.jpg");
            var copiedSidecar = copiedVariant + ".bukit-freshness.json";
            File.Copy(ownedSource, copiedSource);
            File.SetLastWriteTimeUtc(copiedSource, File.GetLastWriteTimeUtc(ownedSource));
            File.Copy(ownedVariant, copiedVariant);
            File.Copy(ownedVariant + ".bukit-freshness.json", copiedSidecar);
            var copiedVariantBytes = await File.ReadAllBytesAsync(copiedVariant);
            var copiedSidecarBytes = await File.ReadAllBytesAsync(copiedSidecar);
            var context = CreateContextWithPriorImageOwnership(outDir, ownedVariant);

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            Assert.Equal(copiedVariantBytes, await File.ReadAllBytesAsync(copiedVariant));
            Assert.Equal(copiedSidecarBytes, await File.ReadAllBytesAsync(copiedSidecar));
            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Data["__plugin_outputs"]);
            Assert.DoesNotContain(outputs, output => output.Path == "assets/copied/photo-300w.jpg");
            var images = Assert.IsType<Dictionary<string, object>>(context.Data["__image_srcsets"]);
            Assert.DoesNotContain("copied/photo.jpg", images.Keys);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CurrentManagedPathWithUserReplacedBytes_IsNotProjectedOrOverwritten()
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-300w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            WriteValidImage(variant, "user-replacement");
            var userBytes = await File.ReadAllBytesAsync(variant);
            var sidecarBytes = await File.ReadAllBytesAsync(sidecar);
            var context = CreateContextWithPriorImageOwnership(outDir, variant);

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            Assert.Equal(userBytes, await File.ReadAllBytesAsync(variant));
            Assert.Equal(sidecarBytes, await File.ReadAllBytesAsync(sidecar));
            Assert.False(context.Data.ContainsKey("__plugin_outputs"));
            Assert.False(context.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_HistoricalManagedPathWithUserReplacedBytes_IsNotDeleted()
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } }))
                .AfterBuildAsync(CreateContext(outDir));

            var historicalVariant = Path.Combine(assetsDir, "photo-300w.jpg");
            var historicalSidecar = historicalVariant + ".bukit-freshness.json";
            WriteValidImage(historicalVariant, "user-replacement");
            var userBytes = await File.ReadAllBytesAsync(historicalVariant);
            var sidecarBytes = await File.ReadAllBytesAsync(historicalSidecar);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContextWithPriorImageOwnership(outDir, historicalVariant));

            Assert.Equal(userBytes, await File.ReadAllBytesAsync(historicalVariant));
            Assert.Equal(sidecarBytes, await File.ReadAllBytesAsync(historicalSidecar));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceRenamedWithoutResizeTool_RemovesOwnedOrphan()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            var noToolDir = Path.Combine(outDir, "no-tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            Directory.CreateDirectory(noToolDir);
            var source = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(source, "source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-480w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            File.Move(source, Path.Combine(assetsDir, "renamed.jpg"));
            Environment.SetEnvironmentVariable("PATH", noToolDir);
            var context = CreateContextWithPriorImageOwnership(outDir, variant);

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            Assert.False(File.Exists(variant));
            Assert.False(File.Exists(sidecar));
            Assert.False(context.Data.ContainsKey("__plugin_outputs"));
            Assert.False(context.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceRemovedWithoutResizeTool_RemovesOwnedOrphan()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            var noToolDir = Path.Combine(outDir, "no-tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            Directory.CreateDirectory(noToolDir);
            var source = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(source, "source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-480w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            File.Delete(source);
            Environment.SetEnvironmentVariable("PATH", noToolDir);
            var context = CreateContextWithPriorImageOwnership(outDir, variant);

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            Assert.False(File.Exists(variant));
            Assert.False(File.Exists(sidecar));
            Assert.False(context.Data.ContainsKey("__plugin_outputs"));
            Assert.False(context.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CurrentFullyRecomputedSpoofWithoutPriorManifest_IsNotProjected()
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-300w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            WriteValidImage(variant, "user-spoof");
            RewriteVariantIdentity(sidecar, variant);
            var spoofBytes = await File.ReadAllBytesAsync(variant);
            var spoofSidecar = await File.ReadAllBytesAsync(sidecar);
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            Assert.Equal(spoofBytes, await File.ReadAllBytesAsync(variant));
            Assert.Equal(spoofSidecar, await File.ReadAllBytesAsync(sidecar));
            Assert.False(context.Data.ContainsKey("__plugin_outputs"));
            Assert.False(context.Data.ContainsKey("__image_srcsets"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_HistoricalFullyRecomputedSpoofWithoutPriorManifest_IsNotDeleted()
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
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } }))
                .AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-300w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            WriteValidImage(variant, "user-spoof");
            RewriteVariantIdentity(sidecar, variant);
            var spoofBytes = await File.ReadAllBytesAsync(variant);
            var spoofSidecar = await File.ReadAllBytesAsync(sidecar);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            Assert.Equal(spoofBytes, await File.ReadAllBytesAsync(variant));
            Assert.Equal(spoofSidecar, await File.ReadAllBytesAsync(sidecar));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_OrphanFullyRecomputedSpoofWithoutPriorManifest_IsNotDeleted()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            var toolDir = Path.Combine(outDir, "tools");
            var noToolDir = Path.Combine(outDir, "no-tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            Directory.CreateDirectory(noToolDir);
            var source = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(source, "source");
            WriteCopyTool(toolDir);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-300w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            WriteValidImage(variant, "user-spoof");
            RewriteVariantIdentity(sidecar, variant);
            var spoofBytes = await File.ReadAllBytesAsync(variant);
            var spoofSidecar = await File.ReadAllBytesAsync(sidecar);
            File.Delete(source);
            Environment.SetEnvironmentVariable("PATH", noToolDir);

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            Assert.Equal(spoofBytes, await File.ReadAllBytesAsync(variant));
            Assert.Equal(spoofSidecar, await File.ReadAllBytesAsync(sidecar));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    private static void WriteValidImage(string path, string seed)
    {
        var hash = 0;
        foreach (var character in seed)
        {
            hash = unchecked(hash * 31 + character);
        }

        var color = new SixLabors.ImageSharp.PixelFormats.Rgba32(
            (byte)(hash & 0xFF), (byte)((hash >> 8) & 0xFF), (byte)((hash >> 16) & 0xFF));
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(8, 8);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                image[x, y] = color;
            }
        }

        image[7, 7] = new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255);
        image.SaveAsJpeg(path);
    }

    private static string EscapeSingleQuoted(string value) => value.Replace("'", "'\\''", StringComparison.Ordinal);

    private static void WriteCopyTool(string toolDir) => WriteTool(toolDir, "magick", """
        if [ "$1" = "--version" ]; then exit 0; fi
        for last in "$@"; do :; done
        cp "$1" "$last"
        """);

    private static void RewriteVariantIdentity(string sidecar, string variant)
    {
        var variantBytes = File.ReadAllBytes(variant);
        var json = JsonNode.Parse(File.ReadAllText(sidecar))!.AsObject();
        json["variantLength"] = variantBytes.LongLength;
        json["variantSha256"] = Convert.ToHexString(SHA256.HashData(variantBytes));
        File.WriteAllText(sidecar, json.ToJsonString());
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

    private static BuildContext CreateContextWithPriorImageOwnership(
        string outDir,
        params string[] variants)
    {
        var context = CreateContext(outDir);
        var outputs = new HashSet<PluginOutputTrackingInfo>();
        foreach (var variant in variants)
        {
            foreach (var path in new[] { variant, variant + ".bukit-freshness.json" })
            {
                outputs.Add(new PluginOutputTrackingInfo(
                    "image-processing",
                    "after-build",
                    BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(outDir, path))));
            }
        }

        context.Data[BuildContextDataKeys.PriorPluginOutputs] = outputs;
        return context;
    }

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
