using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Engine.Plugins.BuiltIn.ImageProcessing;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using SkiaSharp;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class ImageProcessingPluginTests
{
    [Fact]
    public async Task SourceHashCache_IsReusedOnlyWithinOneBuild()
    {
        var file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, "first");
            var cache = new Dictionary<string, string>(StringComparer.Ordinal);
            var first = await Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantFreshness.GetOrComputeSourceSha256Async(file, cache, CancellationToken.None);
            await File.WriteAllTextAsync(file, "second");
            var reused = await Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantFreshness.GetOrComputeSourceSha256Async(file, cache, CancellationToken.None);
            var nextBuild = await Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantFreshness.GetOrComputeSourceSha256Async(file, new Dictionary<string, string>(StringComparer.Ordinal), CancellationToken.None);
            Assert.Equal(first, reused);
            Assert.NotEqual(first, nextBuild);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task AfterBuild_ResizePreservesExactFreshnessBytesAndTracking()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads", "nested");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "photo&one.jpg");
            WriteValidImage(source, "contract", width: 1000);
            File.SetLastWriteTimeUtc(source, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = [],
                Sizes = [768, 0, 480, 768, -1, 1200, 1000],
                Quality = 73
            })).AfterBuildAsync(context);

            var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            var info = new FileInfo(source);
            var expectedOutputs = new HashSet<PluginOutputTrackingInfo>();
            foreach (var size in new[] { 480, 768 })
            {
                var relative = $"assets/uploads/nested/photo&one-{size}w.jpg";
                var variant = Path.Combine(outDir, relative);
                var variantBytes = File.ReadAllBytes(variant);
                using (var codec = SKCodec.Create(variant))
                {
                    Assert.Equal(size, codec.Info.Width);
                }
                var variantHash = Convert.ToHexString(SHA256.HashData(variantBytes));
                var expected = $$"""{"schemaVersion":1,"owner":"bukit:image-processing","sourcePath":"assets/uploads/nested/photo\u0026one.jpg","variantPath":"assets/uploads/nested/photo\u0026one-{{size}}w.jpg","sourceSize":{{info.Length}},"sourceMtime":{{info.LastWriteTimeUtc.Ticks}},"sourceSha256":"{{sourceHash}}","variantLength":{{variantBytes.LongLength}},"variantSha256":"{{variantHash}}","quality":73,"size":{{size}},"format":".jpg","tool":"skiasharp-orientation-v3"}""";
                Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(variant + ".bukit-freshness.json"));
                expectedOutputs.Add(new("image-processing", "after-build", relative));
                expectedOutputs.Add(new("image-processing", "after-build", relative + ".bukit-freshness.json"));
            }

            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.True(expectedOutputs.SetEquals(outputs));
            Assert.Equal(5, Directory.GetFiles(assetsDir).Length);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Theory]
    [InlineData(LogLevel.Warn)]
    [InlineData(LogLevel.Error)]
    public async Task AfterBuild_ResizeFailureUsesCountedCallerJsonLoggerBeforeFiltering(LogLevel level)
    {
        var outDir = GetTempDir();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var sink = new ResizeWarningLogger(new ConsoleLogger(level, "json"));
        var logger = new BuildDiagnosticLogger(sink);
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteUndecodableImage(Path.Combine(assetsDir, "photo.jpg"), "failure");
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var context = new BuildContext
            {
                RootDir = outDir,
                OutputDir = outDir,
                LayoutsDir = outDir,
                BaseUrl = "/",
                RoutedDocuments = [],
                Logger = logger
            };

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            { Enabled = true, Formats = [], Sizes = [480] })).AfterBuildAsync(context));
            Assert.Contains("assets/uploads/photo-480w.jpg", failure.Message, StringComparison.Ordinal);
            Assert.Contains("pluginFailMode", failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("image tool", failure.Message, StringComparison.Ordinal);

            const string expected = "event=image_resize.error file=photo.jpg reason=Source pixels could not be fully decoded.";
            Assert.Equal(expected, Assert.Single(sink.Warnings));
            Assert.Equal(1, logger.WarningCount);
            Assert.Equal(0, logger.ErrorCount);
            Assert.Empty(stdout.ToString());
            if (level == LogLevel.Error) Assert.Empty(stderr.ToString());
            else
            {
                var line = Assert.Single(stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                using var json = JsonDocument.Parse(line);
                Assert.Equal("Warn", json.RootElement.GetProperty("level").GetString());
                Assert.Equal(expected, json.RootElement.GetProperty("msg").GetString());
            }
            Assert.False(context.TryGet(BuildDataKeys.PluginOutputs, out _));
            Assert.Equal([Path.Combine(assetsDir, "photo.jpg")], Directory.GetFiles(assetsDir));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    private sealed class ResizeWarningLogger(ILogger inner) : ILogger
    {
        internal List<string> Warnings { get; } = [];
        public void Debug(string message) => inner.Debug(message);
        public void Info(string message) => inner.Info(message);
        public void Warn(string message) { Warnings.Add(message); inner.Warn(message); }
        public void Error(string message) => Assert.Fail("Unexpected Error: " + message);
    }

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
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_SmallSourceDoesNotThrow()
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
    public async Task AfterBuild_UsesSkiaSharpAndDoesNotUpscale()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source", width: 1000);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 1200 } }))
                .AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-480w.jpg");
            using var codec = SKCodec.Create(variant);
            Assert.Equal(480, codec.Info.Width);
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-1200w.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_ResizeNeverProbesOrInvokesImageToolsOnPath()
    {
        RequireUnix();
        var outDir = GetTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            var toolDir = Path.Combine(outDir, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            var marker = Path.Combine(outDir, "invoked.txt");
            string[] tools = ["magick", "convert"];
            foreach (var tool in tools)
            {
                WriteTool(toolDir, tool, $"""
                    printf '{tool} ' >> '{EscapeSingleQuoted(marker)}'
                    exit 0
                    """);
            }
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source", width: 1000);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolDir, originalPath));

            await new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            { Enabled = true, Formats = ["webp"], Sizes = [480] })).AfterBuildAsync(CreateContext(outDir));

            Assert.False(File.Exists(marker));
            var variant = Path.Combine(assetsDir, "photo-480w.jpg");
            using (var codec = SKCodec.Create(variant))
            {
                Assert.Equal(480, codec.Info.Width);
            }
            Assert.Equal(
                ImageVariantGenerator.SkiaSharpResizeIdentity,
                JsonNode.Parse(File.ReadAllText(variant + ".bukit-freshness.json"))!["tool"]!.GetValue<string>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_OrientedSource_UsesDisplayedWidthForCandidates()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllBytes(Path.Combine(assetsDir, "portrait.jpg"), ImageOptimizerTests.CreateOrientedJpeg(SKEncodedOrigin.RightTop));
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true, Formats = new[] { "webp" }, Sizes = new[] { 20, 60 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            Assert.False(File.Exists(Path.Combine(assetsDir, "portrait-60w.webp")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "portrait-60w.jpg")));
            using var full = SKBitmap.Decode(Path.Combine(assetsDir, "portrait-40w.webp"));
            using var resized = SKBitmap.Decode(Path.Combine(assetsDir, "portrait-20w.webp"));
            Assert.Equal((40, 80), (full.Width, full.Height));
            Assert.Equal((20, 40), (resized.Width, resized.Height));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_PngExifUsesDisplayedWidthAndRejectsMalformedPlanning()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllBytes(Path.Combine(assetsDir, "portrait.png"), ImageOptimizerTests.CreateOrientedPng(SKEncodedOrigin.RightTop, true));
            File.WriteAllBytes(Path.Combine(assetsDir, "bad.png"), ImageOptimizerTests.CreateOrientedPng(SKEncodedOrigin.RightTop, false, "ifd-offset"));
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true, Formats = new[] { "webp" }, Sizes = new[] { 20, 60, 100 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            using var full = SKBitmap.Decode(Path.Combine(assetsDir, "portrait-80w.webp"));
            using var resized = SKBitmap.Decode(Path.Combine(assetsDir, "portrait-60w.png"));
            Assert.NotNull(full);
            Assert.NotNull(resized);
            Assert.Equal((80, 40), (full.Width, full.Height));
            Assert.Equal((60, 30), (resized.Width, resized.Height));
            Assert.False(File.Exists(Path.Combine(assetsDir, "portrait-100w.webp")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "portrait-100w.png")));
            Assert.Empty(Directory.GetFiles(assetsDir, "bad-*"));
            Assert.Empty(Directory.GetFiles(assetsDir, ".*.bukit-*"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_ResizeAutoOrientsBeforeDisplayedWidthResize()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllBytes(Path.Combine(assetsDir, "portrait.jpg"), ImageOptimizerTests.CreateOrientedJpeg(SKEncodedOrigin.RightTop, 40, 80));

            await new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            { Enabled = true, Formats = [], Sizes = [60, 100] })).AfterBuildAsync(CreateContext(outDir));

            using var output = SKBitmap.Decode(Path.Combine(assetsDir, "portrait-60w.jpg"));
            Assert.Equal((60, 30), (output.Width, output.Height));
            Assert.False(File.Exists(Path.Combine(assetsDir, "portrait-100w.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Theory]
    [InlineData("magick-auto-orient-v1")]
    [InlineData("convert-auto-orient-v1")]
    [InlineData("magick")]
    public async Task AfterBuild_ResizeRebuildsVariantsRecordedByAnotherTool(string previousTool)
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllBytes(Path.Combine(assetsDir, "portrait.jpg"), ImageOptimizerTests.CreateOrientedJpeg(SKEncodedOrigin.RightTop, 40, 80));
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true, Formats = [], Sizes = [20] });
            var first = CreateContext(outDir);
            await new ImageProcessingPlugin(config).AfterBuildAsync(first);
            var variant = Path.Combine(assetsDir, "portrait-20w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            var freshness = JsonNode.Parse(File.ReadAllText(sidecar))!.AsObject();
            freshness["tool"] = previousTool;
            File.WriteAllText(sidecar, freshness.ToJsonString());
            var oldTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(variant, oldTimestamp);
            var second = CreateContext(outDir);
            second.Set(BuildDataKeys.PriorPluginOutputs, first.Get(BuildDataKeys.PluginOutputs));

            await new ImageProcessingPlugin(config).AfterBuildAsync(second);

            Assert.True(File.GetLastWriteTimeUtc(variant) > oldTimestamp);
            Assert.Equal(
                ImageVariantGenerator.SkiaSharpResizeIdentity,
                JsonNode.Parse(File.ReadAllText(sidecar))!["tool"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_WebpGeneratesOwnedCandidatesAndProgressivePicture()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "photo.png");
            using (var image = new SKBitmap(1000, 8))
            {
                image.Erase(SKColors.Transparent);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(source, encoded.ToArray());
            }
            var htmlFile = Path.Combine(outDir, "index.html");
            const string img = "<img src=\"/assets/photo.png\" sizes=\"50vw\" alt=\"A > B\" width=\"1000\" height=\"8\" loading=\"lazy\" decoding=\"async\" class=\"hero\" id=\"lead\">";
            var config = CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = new[] { "webp", "WEBP" },
                Sizes = new[] { 480, 1200 },
                Quality = 76
            }) with
            {
                Content = TestContent.Markdown() with { Media = new MediaConfig { UrlBase = "/assets" } }
            };
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.MediaDownloadDir, assetsDir);
            context.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
            var plugin = new ImageProcessingPlugin(config);
            var transformed = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(
                context,
                BuildExecutionMode.Production)).Transform(
                    new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
                    img);
            File.WriteAllText(htmlFile, $"<html><body>{transformed}</body></html>");

            await plugin.AfterBuildAsync(context);

            var responsive = Path.Combine(assetsDir, "photo-480w.webp");
            var full = Path.Combine(assetsDir, "photo-1000w.webp");
            using var responsiveCodec = SKCodec.Create(responsive);
            using var fullCodec = SKCodec.Create(full);
            Assert.Equal(480, responsiveCodec.Info.Width);
            Assert.Equal(1000, fullCodec.Info.Width);
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-1200w.webp")));
            Assert.True(File.Exists(responsive + ".bukit-freshness.json"));
            var freshness = JsonNode.Parse(File.ReadAllText(responsive + ".bukit-freshness.json"))!.AsObject();
            Assert.Equal(".webp", freshness["format"]!.GetValue<string>());
            Assert.Equal(76, freshness["quality"]!.GetValue<int>());
            Assert.Equal(480, freshness["size"]!.GetValue<int>());
            Assert.StartsWith("skiasharp-webp-", freshness["tool"]!.GetValue<string>(), StringComparison.Ordinal);

            var html = File.ReadAllText(htmlFile);
            Assert.Contains("<picture><source type=\"image/webp\"", html, StringComparison.Ordinal);
            Assert.Contains("/assets/photo-480w.webp 480w, /assets/photo-1000w.webp 1000w", html, StringComparison.Ordinal);
            Assert.Contains("srcset=\"/assets/photo-480w.png 480w, /assets/photo.png 1000w\"", html, StringComparison.Ordinal);
            Assert.Contains("src=\"/assets/photo.png\" sizes=\"50vw\" alt=\"A > B\" width=\"1000\" height=\"8\" loading=\"lazy\" decoding=\"async\" class=\"hero\" id=\"lead\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-bukit-generated-srcset", html, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(html, "<picture>"));

            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.Contains(outputs, output => output.Path == "assets/photo-480w.webp");
            Assert.Contains(outputs, output => output.Path == "assets/photo-1000w.webp.bukit-freshness.json");
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_WebpSourceGetsWebpVariantsWithoutPicture()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            WriteImage(Path.Combine(assetsDir, "photo.webp"), 1000, 8, SKEncodedImageFormat.Webp);
            var (plugin, context, transformed) = TransformWithWebp(outDir, assetsDir, "<img src=\"/assets/photo.webp\" alt=\"\">");

            await plugin.AfterBuildAsync(context);

            var variant = Path.Combine(assetsDir, "photo-480w.webp");
            using (var codec = SKCodec.Create(variant))
            {
                Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
                Assert.Equal(480, codec.Info.Width);
            }
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-1000w.webp")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-1200w.webp")));
            var freshness = JsonNode.Parse(File.ReadAllText(variant + ".bukit-freshness.json"))!.AsObject();
            Assert.Equal(".webp", freshness["format"]!.GetValue<string>());
            Assert.Equal("assets/photo.webp", freshness["sourcePath"]!.GetValue<string>());

            var html = File.ReadAllText(Path.Combine(outDir, "index.html"));
            Assert.Contains("srcset=\"/assets/photo-480w.webp 480w, /assets/photo.webp 1000w\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<picture>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-bukit-generated-srcset", html, StringComparison.Ordinal);
            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.Contains(outputs, output => output.Path == "assets/photo-480w.webp");
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_WebpSourceBesideSameStemPngIsLeftAlone()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            WriteImage(Path.Combine(assetsDir, "photo.png"), 1000, 8, SKEncodedImageFormat.Png);
            WriteImage(Path.Combine(assetsDir, "photo.webp"), 1000, 8, SKEncodedImageFormat.Webp);
            var (plugin, context, transformed) = TransformWithWebp(outDir, assetsDir, "<img src=\"/assets/photo.webp\" alt=\"\">");

            await plugin.AfterBuildAsync(context);

            Assert.DoesNotContain("srcset", transformed, StringComparison.Ordinal);
            var freshness = JsonNode.Parse(File.ReadAllText(Path.Combine(assetsDir, "photo-480w.webp.bukit-freshness.json")))!.AsObject();
            Assert.Equal("assets/photo.png", freshness["sourcePath"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_AnimatedWebpIsLeftAsUploaded()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "loop.webp");
            WriteAnimatedWebp(source, 1000, 8);
            using (var codec = SKCodec.Create(source))
            {
                Assert.Equal(2, codec.FrameCount);
            }
            Assert.False(ImageVariantGenerator.TryReadImageSize(source, out _, out _));
            var (plugin, context, transformed) = TransformWithWebp(outDir, assetsDir, "<img src=\"/assets/loop.webp\" alt=\"\">");

            await plugin.AfterBuildAsync(context);

            Assert.DoesNotContain("srcset", transformed, StringComparison.Ordinal);
            Assert.Equal(["loop.webp"], Directory.GetFiles(assetsDir).Select(Path.GetFileName));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_VariantShapedWebpNameIsNotASource()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "hero-1600w.webp");
            WriteImage(source, 1000, 8, SKEncodedImageFormat.Webp);
            Assert.False(ImageVariantPlan.IsResizableSource(source));
            var (plugin, context, transformed) = TransformWithWebp(outDir, assetsDir, "<img src=\"/assets/hero-1600w.webp\" alt=\"\">");

            await plugin.AfterBuildAsync(context);

            Assert.DoesNotContain("srcset", transformed, StringComparison.Ordinal);
            Assert.Equal(["hero-1600w.webp"], Directory.GetFiles(assetsDir).Select(Path.GetFileName));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("photo.Jpeg")]
    [InlineData("PHOTO.png")]
    public void IsResizableSource_WebpBesideRasterOfAnyCaseIsSkipped(string rasterName)
    {
        var dir = GetTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            var webp = Path.Combine(dir, "photo.webp");
            WriteImage(webp, 40, 4, SKEncodedImageFormat.Webp);
            Assert.True(ImageVariantPlan.IsResizableSource(webp));

            WriteImage(Path.Combine(dir, rasterName), 40, 4, SKEncodedImageFormat.Png);

            Assert.False(ImageVariantPlan.IsResizableSource(webp));
            Assert.True(ImageVariantPlan.IsResizableSource(Path.Combine(dir, rasterName)));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // SkiaSharp cannot encode animated WebP, so wrap one lossless frame twice
    // in a VP8X/ANIM/ANMF container (RFC 9649).
    private static void WriteAnimatedWebp(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SteelBlue);
        using var pixmap = bitmap.PeekPixels();
        using var encoded = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100))!;
        var still = encoded.ToArray();
        var vp8l = FindRiffChunk(still, "VP8L");

        static byte[] Chunk(string fourCc, byte[] payload)
        {
            var chunk = new List<byte>(System.Text.Encoding.ASCII.GetBytes(fourCc));
            chunk.AddRange(BitConverter.GetBytes((uint)payload.Length));
            chunk.AddRange(payload);
            if (payload.Length % 2 == 1) chunk.Add(0);
            return [.. chunk];
        }
        static byte[] U24(int value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];

        var vp8x = new List<byte> { 0x12, 0, 0, 0 }; // animation + alpha flags
        vp8x.AddRange(U24(width - 1));
        vp8x.AddRange(U24(height - 1));
        var frame = new List<byte>();
        frame.AddRange(U24(0));
        frame.AddRange(U24(0));
        frame.AddRange(U24(width - 1));
        frame.AddRange(U24(height - 1));
        frame.AddRange(U24(100));
        frame.Add(0);
        frame.AddRange(vp8l);
        var body = new List<byte>(System.Text.Encoding.ASCII.GetBytes("WEBP"));
        body.AddRange(Chunk("VP8X", [.. vp8x]));
        body.AddRange(Chunk("ANIM", [0, 0, 0, 0, 0, 0]));
        body.AddRange(Chunk("ANMF", [.. frame]));
        body.AddRange(Chunk("ANMF", [.. frame]));
        var file = new List<byte>(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        file.AddRange(BitConverter.GetBytes((uint)body.Count));
        file.AddRange(body);
        File.WriteAllBytes(path, [.. file]);
    }

    private static byte[] FindRiffChunk(byte[] riff, string fourCc)
    {
        for (var offset = 12; offset + 8 <= riff.Length;)
        {
            var size = (int)BitConverter.ToUInt32(riff, offset + 4);
            var total = 8 + size + (size % 2);
            if (System.Text.Encoding.ASCII.GetString(riff, offset, 4) == fourCc)
            {
                return riff[offset..(offset + total)];
            }
            offset += total;
        }
        throw new InvalidDataException($"{fourCc} chunk not found.");
    }

    private static void WriteImage(string path, int width, int height, SKEncodedImageFormat format)
    {
        using var image = new SKBitmap(width, height);
        image.Erase(SKColors.SteelBlue);
        using var encoded = image.Encode(format, 90);
        File.WriteAllBytes(path, encoded.ToArray());
    }

    private static (ImageProcessingPlugin Plugin, BuildContext Context, string Transformed) TransformWithWebp(
        string outDir,
        string assetsDir,
        string img)
    {
        var config = CreateConfig(new ImageOptimizationConfig { Enabled = true, Formats = new[] { "webp" }, Sizes = new[] { 480, 1200 } }) with
        {
            Content = TestContent.Markdown() with { Media = new MediaConfig { UrlBase = "/assets" } }
        };
        var context = CreateContext(outDir);
        context.Set(BuildDataKeys.MediaDownloadDir, assetsDir);
        context.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
        var plugin = new ImageProcessingPlugin(config);
        var transformed = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(context, BuildExecutionMode.Production)).Transform(
            new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
            img);
        File.WriteAllText(Path.Combine(outDir, "index.html"), $"<html><body>{transformed}</body></html>");
        return (plugin, context, transformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AfterBuild_PreviousEncoderIdentity_RebuildsOwnedWebpAndFallback(bool previousSkia)
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source", width: 700);
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true, Formats = new[] { "webp" }, Sizes = new[] { 480 } });
            var first = CreateContext(outDir);
            await new ImageProcessingPlugin(config).AfterBuildAsync(first);
            var webp = Path.Combine(assetsDir, "photo-480w.webp");
            var fallback = Path.Combine(assetsDir, "photo-480w.jpg");
            var oldTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (var variant in new[] { webp, fallback })
            {
                var sidecar = variant + ".bukit-freshness.json";
                var freshness = JsonNode.Parse(File.ReadAllText(sidecar))!.AsObject();
                freshness["tool"] = previousSkia
                    ? variant == webp ? ImageOptimizer.WebpEncoderIdentity.Replace("orientation-v2-", "", StringComparison.Ordinal) : "skiasharp"
                    : variant == webp ? "imagesharp-webp-3.1.12" : "imagesharp";
                File.WriteAllText(sidecar, freshness.ToJsonString());
                File.SetLastWriteTimeUtc(variant, oldTimestamp);
            }

            var second = CreateContext(outDir);
            second.Set(BuildDataKeys.PriorPluginOutputs, first.Get(BuildDataKeys.PluginOutputs));
            await new ImageProcessingPlugin(config).AfterBuildAsync(second);

            Assert.StartsWith("skiasharp-webp-", JsonNode.Parse(File.ReadAllText(webp + ".bukit-freshness.json"))!["tool"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal("skiasharp-orientation-v3", JsonNode.Parse(File.ReadAllText(fallback + ".bukit-freshness.json"))!["tool"]!.GetValue<string>());
            Assert.True(File.GetLastWriteTimeUtc(webp) > oldTimestamp);
            Assert.True(File.GetLastWriteTimeUtc(fallback) > oldTimestamp);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_WebpRewriteProtectsExistingPictureAndIsIdempotent()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(source, "source", width: 700);
            var htmlFile = Path.Combine(outDir, "index.html");
            const string existing = "<picture><source type=\"image/avif\" srcset=\"photo.avif\"><img src=\"/assets/photo.jpg\" alt=\"existing\"></picture>";
            const string ignored = "<script>const fake='<img src=\"/assets/photo.jpg\">';</script><!-- <img src=\"/assets/photo.jpg\"> --><img src=\"https://cdn.example/photo.jpg\"><img src=\"data:image/png;base64,AA\"><img src=\"/assets/icon.gif\"><img src=\"/assets/photo.jpg\" srcset=\"/custom.jpg 2x\" alt=\"author\">";
            File.WriteAllText(htmlFile, existing + ignored + "<img src=\"/assets/photo.jpg\" alt=\"local\">");
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true, Formats = new[] { "webp" }, Sizes = new[] { 480 } });
            var first = CreateContext(outDir);
            first.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
            await new ImageProcessingPlugin(config).AfterBuildAsync(first);
            var once = File.ReadAllText(htmlFile);

            var second = CreateContext(outDir);
            second.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
            second.Set(BuildDataKeys.PriorPluginOutputs, first.Get(BuildDataKeys.PluginOutputs));
            await new ImageProcessingPlugin(config).AfterBuildAsync(second);
            var twice = File.ReadAllText(htmlFile);

            Assert.Equal(once, twice);
            Assert.Contains(existing, twice, StringComparison.Ordinal);
            Assert.Contains(ignored, twice, StringComparison.Ordinal);
            Assert.Equal(2, CountOccurrences(twice, "<picture>"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_EmptyFormatsPreservesOwnedWebpWithoutProjectingIt()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source", width: 700);
            var first = CreateContext(outDir);
            await new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = new[] { "webp" },
                Sizes = new[] { 480 }
            })).AfterBuildAsync(first);
            var webp = Path.Combine(assetsDir, "photo-480w.webp");
            var originalHash = SHA256.HashData(File.ReadAllBytes(webp));
            var htmlFile = Path.Combine(outDir, "index.html");
            const string html = "<img src=\"/assets/photo.jpg\" alt=\"fallback\">";
            File.WriteAllText(htmlFile, html);

            var second = CreateContext(outDir);
            second.Set(BuildDataKeys.PriorPluginOutputs, first.Get(BuildDataKeys.PluginOutputs));
            second.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
            await new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = Array.Empty<string>(),
                Sizes = new[] { 480 }
            })).AfterBuildAsync(second);

            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(webp)));
            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(second.Get(BuildDataKeys.PluginOutputs));
            Assert.Contains(outputs, output => output.Path == "assets/photo-480w.webp");
            Assert.Equal(html, File.ReadAllText(htmlFile));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_OrphanUnownedSidecarWithoutVariantIsAFailure()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source", width: 700);
            var sidecar = Path.Combine(assetsDir, "photo-480w.jpg.bukit-freshness.json");
            File.WriteAllText(sidecar, "user");

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Formats = [], Sizes = [480] })).AfterBuildAsync(CreateContext(outDir)));

            Assert.Contains("assets/uploads/photo-480w.jpg", failure.Message, StringComparison.Ordinal);
            Assert.Contains("photo-480w.jpg.bukit-freshness.json", failure.Message, StringComparison.Ordinal);
            Assert.Equal("user", File.ReadAllText(sidecar));
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_UnownedExistingVariantIsKeptWithoutFailure()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source", width: 700);
            var variant = Path.Combine(assetsDir, "photo-480w.jpg");
            WriteValidImage(variant, "user-variant", width: 480);
            var before = File.ReadAllBytes(variant);

            await new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Formats = [], Sizes = [480] })).AfterBuildAsync(CreateContext(outDir));

            Assert.Equal(before, File.ReadAllBytes(variant));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_WebpKeepsUnownedCandidateAndDoesNotProjectIt()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source", width: 700);
            File.WriteAllText(Path.Combine(assetsDir, "broken.jpg"), "not an image");
            var unowned = Path.Combine(assetsDir, "photo-480w.webp");
            using (var image = new SKBitmap(2, 2))
            {
                using var encoded = image.Encode(SKEncodedImageFormat.Webp, 80);
                File.WriteAllBytes(unowned, encoded.ToArray());
            }
            var unownedHash = SHA256.HashData(File.ReadAllBytes(unowned));
            var htmlFile = Path.Combine(outDir, "index.html");
            File.WriteAllText(htmlFile, "<img src=\"/assets/photo.jpg\" alt=\"fallback\">");
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });

            await new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = new[] { "webp" },
                Sizes = new[] { 480 }
            })).AfterBuildAsync(context);

            Assert.Equal(unownedHash, SHA256.HashData(File.ReadAllBytes(unowned)));
            Assert.False(File.Exists(unowned + ".bukit-freshness.json"));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-700w.webp")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "broken.webp")));
            var html = File.ReadAllText(htmlFile);
            Assert.DoesNotContain("photo-480w.webp", html, StringComparison.Ordinal);
            Assert.Contains("photo-700w.webp 700w", html, StringComparison.Ordinal);
            AssertNotTracked(context, outDir, unowned, unowned + ".bukit-freshness.json");
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Theory]
    [InlineData("<img src=\"/assets/uploads/photo.jpg\" sizes=\"50vw\" alt=\"A > B\">")]
    [InlineData("<img data-src=\"/assets/uploads/other.jpg\" src=\"/assets/uploads/photo.jpg\" sizes=\"50vw\" alt=\"A > B\">")]
    [InlineData("<img title=\"src='/assets/uploads/other.jpg' srcset='fake' decoding='sync'\" src='/assets/uploads/photo.jpg' sizes=\"50vw\" alt=\"A > B\"/>")]
    [InlineData("<img src=/assets/uploads/photo.jpg sizes=\"50vw\" alt=\"A > B\">")]
    public void HtmlTransform_AddsFileBasedSrcsetWithoutOverwritingTemplateHints(string input)
    {
        var outDir = GetTempDir();
        try
        {
            var mediaDir = Path.Combine(outDir, "media");
            Directory.CreateDirectory(mediaDir);
            WriteValidImage(Path.Combine(mediaDir, "photo.jpg"), "source", width: 1000);
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.MediaDownloadDir, mediaDir);
            var plugin = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 768, 1200 } }));
            var transform = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(
                context,
                BuildExecutionMode.Production));

            var html = transform.Transform(
                new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
                input);

            Assert.Contains("alt=\"A > B\"", html, StringComparison.Ordinal);
            Assert.Contains("sizes=\"50vw\"", html, StringComparison.Ordinal);
            Assert.Contains("srcset=\"/assets/uploads/photo-480w.jpg 480w, /assets/uploads/photo-768w.jpg 768w, /assets/uploads/photo.jpg 1000w\"", html, StringComparison.Ordinal);
            Assert.Contains("decoding=\"async\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("1200w", html, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Theory]
    [InlineData("<img src=\"/assets/uploads/photo.jpg\">", "sizes=\"auto, (max-width: 768px) 100vw, 768px\"")]
    [InlineData("<img src=\"/assets/uploads/photo.jpg\" loading=\"eager\">", "sizes=\"(max-width: 768px) 100vw, 768px\"")]
    [InlineData("<img src=\"/assets/uploads/photo.jpg\" sizes=\"50vw\">", "sizes=\"50vw\"")]
    public void HtmlTransform_SizesAttribute_AddsAutoOnlyToLazyImagesAndKeepsAuthorSizes(string second, string expected)
    {
        var outDir = GetTempDir();
        try
        {
            var mediaDir = Path.Combine(outDir, "media");
            Directory.CreateDirectory(mediaDir);
            WriteValidImage(Path.Combine(mediaDir, "photo.jpg"), "source", width: 1000);
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.MediaDownloadDir, mediaDir);
            var plugin = new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Sizes = new[] { 480 },
                SizesAttribute = "(max-width: 768px) 100vw, 768px"
            }));
            var transform = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(
                context,
                BuildExecutionMode.Production));

            var html = transform.Transform(
                new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
                "<img src=\"/assets/uploads/photo.jpg\">" + second);

            var firstEnd = html.IndexOf('>', StringComparison.Ordinal) + 1;
            Assert.Contains("sizes=\"(max-width: 768px) 100vw, 768px\"", html[..firstEnd], StringComparison.Ordinal);
            Assert.Contains(expected, html[firstEnd..], StringComparison.Ordinal);
            Assert.Equal(2, Regex.Matches(html, @"\ssizes=", RegexOptions.IgnoreCase).Count);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void HtmlTransform_SizesAttributeUnset_AddsNoSizes()
    {
        var outDir = GetTempDir();
        try
        {
            var mediaDir = Path.Combine(outDir, "media");
            Directory.CreateDirectory(mediaDir);
            WriteValidImage(Path.Combine(mediaDir, "photo.jpg"), "source", width: 1000);
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.MediaDownloadDir, mediaDir);
            var plugin = new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            var transform = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(context, BuildExecutionMode.Production));

            var html = transform.Transform(
                new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
                "<img src=\"/assets/uploads/photo.jpg\"><img src=\"/assets/uploads/photo.jpg\">");

            Assert.Contains("srcset=", html, StringComparison.Ordinal);
            Assert.DoesNotContain("sizes=", html, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Theory]
    [InlineData("<img src=\"/assets/uploads/photo.jpg\" alt=\"a\">", "width=\"1000\" height=\"8\"", "loading=\"lazy\"", 2)]
    [InlineData("<img src=\"/assets/uploads/photo.jpg\" width=\"50\" loading=\"eager\">", "width=\"50\"", "loading=\"eager\"", 1)]
    [InlineData("<img src=\"/assets/uploads/photo.jpg\" HEIGHT=\"4\" Loading=\"auto\">", "HEIGHT=\"4\"", "Loading=\"auto\"", 1)]
    public void HtmlTransform_AddsDimensionsAndLazyLoadingOnlyWhenTheAuthorDidNot_ExceptOnTheFirstImage(string input, string dimensions, string loading, int dimensionAttributes)
    {
        var outDir = GetTempDir();
        try
        {
            var mediaDir = Path.Combine(outDir, "media");
            Directory.CreateDirectory(mediaDir);
            WriteValidImage(Path.Combine(mediaDir, "photo.jpg"), "source", width: 1000);
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.MediaDownloadDir, mediaDir);
            var plugin = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }));
            var transform = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(
                context,
                BuildExecutionMode.Production));

            // The page's first media image stays eager, so the case under test comes second.
            const string first = "<img src=\"/assets/uploads/photo.jpg\" alt=\"first\">";
            var html = transform.Transform(
                new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
                first + input);

            var firstEnd = html.IndexOf('>', StringComparison.Ordinal) + 1;
            Assert.Contains("width=\"1000\" height=\"8\"", html[..firstEnd], StringComparison.Ordinal);
            Assert.DoesNotContain("loading=", html[..firstEnd], StringComparison.OrdinalIgnoreCase);
            Assert.Contains(dimensions, html[firstEnd..], StringComparison.Ordinal);
            Assert.Contains(loading, html[firstEnd..], StringComparison.Ordinal);
            Assert.Equal(dimensionAttributes + 2, Regex.Matches(html, @"\s(?:width|height)=", RegexOptions.IgnoreCase).Count);
            Assert.Single(Regex.Matches(html, @"\sloading=", RegexOptions.IgnoreCase));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuild_AnimatedPng_IsLeftAsUploadedWithoutVariantsOrFailure()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "animated.png");
            File.WriteAllBytes(source, ImageOptimizerTests.AnimatedPng);
            var logger = new ResizeWarningLogger(new ConsoleLogger(LogLevel.Error));
            var context = new BuildContext
            {
                RootDir = outDir,
                OutputDir = outDir,
                LayoutsDir = outDir,
                BaseUrl = "/",
                RoutedDocuments = [],
                Logger = logger
            };

            await new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = ["webp"],
                Sizes = [1]
            })).AfterBuildAsync(context);

            Assert.Equal(ImageOptimizerTests.AnimatedPng, File.ReadAllBytes(source));
            Assert.Equal(["animated.png"], Directory.GetFiles(assetsDir).Select(Path.GetFileName));
            // Planning skips it, so no encoder ever rejects the animation.
            Assert.Contains(logger.Warnings, warning => warning.Contains("file=animated.png", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Warnings, warning => warning.Contains(".error", StringComparison.Ordinal));
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
    public async Task AfterBuildAsync_CancellationStopsResizingWithoutTrackingOrTemporaryOutput()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "test.jpg"), "fake-jpeg");
            using var cancellation = new CancellationTokenSource();
            var context = new BuildContext
            {
                RootDir = outDir,
                OutputDir = outDir,
                LayoutsDir = outDir,
                BaseUrl = "/",
                RoutedDocuments = [],
                Logger = new CancelOnFirstResizeLogger(cancellation)
            };
            var plugin = new ImageProcessingPlugin(CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Formats = [], Sizes = [480, 768] }));

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                plugin.AfterBuildAsync(context, cancellation.Token));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.False(File.Exists(Path.Combine(assetsDir, "test-768w.jpg")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "test-768w.jpg.bukit-freshness.json")));
            Assert.False(context.TryGet(BuildDataKeys.PluginOutputs, out _));
            Assert.Empty(Directory.GetFiles(assetsDir, ".test-*.bukit-*.jpg"));
            Assert.Empty(Directory.GetFiles(assetsDir, "*.bukit-freshness.json.bukit-*.tmp"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    private sealed class CancelOnFirstResizeLogger(CancellationTokenSource cancellation) : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message)
        {
            if (message.StartsWith("event=image_resize.ok", StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }
        }
        public void Warn(string message) { }
        public void Error(string message) => Assert.Fail("Unexpected Error: " + message);
    }

    [Fact]
    public async Task AfterBuildAsync_WarnModeAttemptsAllVariantsAfterFailures()
    {
        var outDir = GetTempDir();
        try
        {
            var assets = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assets);
            for (var i = 0; i < 8; i++) WriteUndecodableImage(Path.Combine(assets, $"photo{i}.jpg"), $"failure-{i}");
            var logger = new ResizeWarningLogger(new ConsoleLogger(LogLevel.Error));
            var context = new BuildContext
            {
                RootDir = outDir,
                OutputDir = outDir,
                LayoutsDir = outDir,
                BaseUrl = "/",
                RoutedDocuments = [],
                Logger = logger
            };
            var config = CreateConfig(new ImageOptimizationConfig { Enabled = true, Formats = [], Sizes = [480] });
            config = config with { Site = config.Site with { PluginFailMode = "warn" } };

            await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(config).AfterBuildAsync(context));
            Assert.Equal(8, logger.Warnings.Count(warning => warning.StartsWith("event=image_resize.error", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_StopsAfterVariantFailureBudget()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            for (var i = 0; i < 8; i++)
            {
                WriteUndecodableImage(Path.Combine(assetsDir, $"photo{i}.jpg"), $"failure-{i}");
            }

            var logger = new ResizeWarningLogger(new ConsoleLogger(LogLevel.Error));
            var context = new BuildContext
            {
                RootDir = outDir,
                OutputDir = outDir,
                LayoutsDir = outDir,
                BaseUrl = "/",
                RoutedDocuments = [],
                Logger = logger
            };

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Formats = [], Sizes = [480] }))
                .AfterBuildAsync(context));

            Assert.Equal(5, logger.Warnings.Count(warning => warning.StartsWith("event=image_resize.error", StringComparison.Ordinal)));
            Assert.Contains("at least 5", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_DoesNotProcessGeneratedSizedImageAsSource()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "original");
            File.WriteAllText(Path.Combine(assetsDir, "photo-480w.jpg"), "existing-sized");

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-480w-480w.jpg")));
            Assert.Equal("existing-sized", File.ReadAllText(Path.Combine(assetsDir, "photo-480w.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_WhenOnlyOneResizeSucceeds_TracksOnlyExistingVariant()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "original");
            // An unowned sidecar blocks the 768w variant, so only 480w can be produced.
            File.WriteAllText(Path.Combine(assetsDir, "photo-768w.jpg.bukit-freshness.json"), "{}");
            var context = CreateContext(outDir);

            await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480, 768 } }))
                .AfterBuildAsync(context));

            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.Contains(outputs, output => output.Path == "assets/uploads/photo-480w.jpg");
            Assert.DoesNotContain(outputs, output => output.Path.StartsWith("assets/uploads/photo-768w.jpg", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "photo-768w.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_WhenNoResizeSucceeds_TracksNoOutputs()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            WriteUndecodableImage(Path.Combine(assetsDir, "photo.jpg"), "original");
            var context = CreateContext(outDir);

            await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(context));

            Assert.False(context.TryGet(BuildDataKeys.PluginOutputs, out _));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceImageChanged_RegeneratesVariant()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);

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
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_StaleVariantRebuildFails_RemovesVariant()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            var sourceFile = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(sourceFile, "original");
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));
            var variantFile = Path.Combine(assetsDir, "photo-480w.jpg");
            var freshnessFile = variantFile + ".bukit-freshness.json";
            Assert.True(File.Exists(variantFile));
            Assert.True(File.Exists(freshnessFile));

            WriteUndecodableImage(sourceFile, "changed");
            var secondContext = CreateContextWithPriorImageOwnership(outDir, variantFile);

            await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(config).AfterBuildAsync(secondContext));

            Assert.False(File.Exists(variantFile));
            Assert.False(File.Exists(freshnessFile));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceHashChangedWithSameSizeAndMtime_RegeneratesVariant()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            var sourceFile = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(sourceFile, "same-metadata");
            await File.AppendAllTextAsync(sourceFile, "A");
            var sourceTimestamp = File.GetLastWriteTimeUtc(sourceFile);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            var variantFile = Path.Combine(assetsDir, "photo-480w.jpg");
            var sidecar = variantFile + ".bukit-freshness.json";
            var firstSourceHash = JsonNode.Parse(await File.ReadAllTextAsync(sidecar))!["sourceSha256"]!.GetValue<string>();
            var sourceBytes = await File.ReadAllBytesAsync(sourceFile);
            sourceBytes[^1] = (byte)'B';
            await File.WriteAllBytesAsync(sourceFile, sourceBytes);
            File.SetLastWriteTimeUtc(sourceFile, sourceTimestamp);
            var oldTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(variantFile, oldTimestamp);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContextWithPriorImageOwnership(outDir, variantFile));

            // The trailing byte does not change decoded pixels, so prove the
            // rebuild through the rewritten variant and its freshness record.
            var secondSourceHash = JsonNode.Parse(await File.ReadAllTextAsync(sidecar))!["sourceSha256"]!.GetValue<string>();
            Assert.NotEqual(firstSourceHash, secondSourceHash);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(sourceBytes)), secondSourceHash);
            Assert.True(File.GetLastWriteTimeUtc(variantFile) > oldTimestamp);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_TracksVariantAndFreshnessSidecarAsOwnedOutputs()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(context);

            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.Contains(outputs, output => output.Path == "assets/uploads/photo-480w.jpg");
            Assert.Contains(outputs, output => output.Path == "assets/uploads/photo-480w.jpg.bukit-freshness.json");
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SizeConfigReduced_DeletesStaleVariants()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);

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
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_ThemeImage_GetsWebpPictureButNoResizedVariants()
    {
        var outDir = GetTempDir();
        try
        {
            var mediaDir = Path.Combine(outDir, "assets", "uploads");
            var themeDir = Path.Combine(outDir, "assets", "theme");
            Directory.CreateDirectory(mediaDir);
            Directory.CreateDirectory(themeDir);
            WriteValidImage(Path.Combine(mediaDir, "photo.jpg"), "media");
            WriteValidImage(Path.Combine(themeDir, "logo.jpg"), "theme");
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.MediaDownloadDir, mediaDir);
            context.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
            var plugin = new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = ["webp"],
                Sizes = [480, 1200]
            }));
            var transformed = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(context, BuildExecutionMode.Production)).Transform(
                new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
                "<img src=\"/assets/uploads/photo.jpg\" alt=\"\"><img src=\"/assets/theme/logo.jpg\" alt=\"\">");
            File.WriteAllText(Path.Combine(outDir, "index.html"), $"<html><body>{transformed}</body></html>");

            await plugin.AfterBuildAsync(context);

            Assert.True(File.Exists(Path.Combine(mediaDir, "photo-480w.jpg")));
            Assert.True(File.Exists(Path.Combine(mediaDir, "photo-1200w.jpg")));
            Assert.False(File.Exists(Path.Combine(themeDir, "logo-480w.jpg")));
            Assert.False(File.Exists(Path.Combine(themeDir, "logo-1200w.jpg")));
            foreach (var width in new[] { 480, 1200, 1600 })
            {
                Assert.True(File.Exists(Path.Combine(themeDir, $"logo-{width}w.webp")));
            }

            var html = File.ReadAllText(Path.Combine(outDir, "index.html"));
            Assert.Contains("/assets/uploads/photo-480w.jpg 480w, /assets/uploads/photo-1200w.jpg 1200w", html, StringComparison.Ordinal);
            Assert.Contains(
                "<picture><source type=\"image/webp\" srcset=\"/assets/theme/logo-480w.webp 480w, /assets/theme/logo-1200w.webp 1200w, /assets/theme/logo-1600w.webp 1600w\"><img src=\"/assets/theme/logo.jpg\" alt=\"\"",
                html,
                StringComparison.Ordinal);
            Assert.Equal(2, CountOccurrences(html, "<picture>"));
            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.DoesNotContain(outputs, output => output.Path.StartsWith("assets/theme/logo-", StringComparison.Ordinal) &&
                !output.Path.Contains(".webp", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CustomMediaUrlBaseOutsideAssets_GetsResizedVariantsAndSrcset()
    {
        var outDir = GetTempDir();
        try
        {
            var mediaDir = Path.Combine(outDir, "media");
            Directory.CreateDirectory(mediaDir);
            WriteValidImage(Path.Combine(mediaDir, "photo.jpg"), "media");
            var context = CreateContext(outDir);
            context.Set(BuildDataKeys.MediaDownloadDir, mediaDir);
            var plugin = new ImageProcessingPlugin(CreateConfig(new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = [],
                Sizes = [480, 1200]
            }) with
            {
                Content = TestContent.Markdown() with { Media = new MediaConfig { UrlBase = "/media" } }
            });
            var transformed = plugin.CreateHtmlTransform(new HtmlTransformPluginContext(context, BuildExecutionMode.Production)).Transform(
                new HtmlTransformContext("/", "index.html", HtmlDocumentKind.Content, BuildExecutionMode.Production, context.Logger),
                "<img src=\"/media/photo.jpg\" alt=\"\">");

            await plugin.AfterBuildAsync(context);

            Assert.True(File.Exists(Path.Combine(mediaDir, "photo-480w.jpg")));
            Assert.True(File.Exists(Path.Combine(mediaDir, "photo-1200w.jpg")));
            Assert.Contains("/media/photo-480w.jpg 480w, /media/photo-1200w.jpg 1200w", transformed, StringComparison.Ordinal);
            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.Contains(outputs, output => output.Path == "media/photo-480w.jpg");
            Assert.Contains(outputs, output => output.Path == "media/photo-1200w.jpg");
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_ThemeImageResizedVariantFromEarlierBuild_IsDeleted()
    {
        var outDir = GetTempDir();
        try
        {
            var themeDir = Path.Combine(outDir, "assets", "theme");
            Directory.CreateDirectory(themeDir);
            var source = Path.Combine(themeDir, "logo.jpg");
            WriteValidImage(source, "theme");
            var images = new ImageOptimizationConfig { Enabled = true, Formats = [], Sizes = [480] };

            // An earlier build planned resized variants for every scanned image. Treating
            // the assets dir as the media dir reproduces the owned variant it left behind.
            await new ImageProcessingPlugin(CreateConfig(images) with
            {
                Content = TestContent.Markdown() with { Media = new MediaConfig { UrlBase = "/assets" } }
            }).AfterBuildAsync(CreateContext(outDir));
            var variant = Path.Combine(themeDir, "logo-480w.jpg");
            Assert.True(File.Exists(variant));
            Assert.True(File.Exists(variant + ".bukit-freshness.json"));

            var context = CreateContextWithPriorImageOwnership(outDir, variant);
            await new ImageProcessingPlugin(CreateConfig(images)).AfterBuildAsync(context);

            Assert.False(File.Exists(variant));
            Assert.False(File.Exists(variant + ".bukit-freshness.json"));
            Assert.True(File.Exists(source));
            Assert.False(context.TryGet(BuildDataKeys.PluginOutputs, out _));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CurrentSizeUserLookalikeWithoutOwnership_IsNeverOverwritten()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            var userOwnedVariant = Path.Combine(assetsDir, "photo-300w.jpg");
            WriteValidImage(userOwnedVariant, "user-owned");
            var userOwnedBytes = await File.ReadAllBytesAsync(userOwnedVariant);
            var userOwnedSidecar = userOwnedVariant + ".bukit-freshness.json";
            const string userOwnedSidecarContent = """
                { "owner": "user", "size": 300, "format": ".jpg" }
                """;
            await File.WriteAllTextAsync(userOwnedSidecar, userOwnedSidecarContent);
            var context = CreateContext(outDir);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 300 } }))
                .AfterBuildAsync(context);

            Assert.Equal(userOwnedBytes, await File.ReadAllBytesAsync(userOwnedVariant));
            Assert.Equal(userOwnedSidecarContent, await File.ReadAllTextAsync(userOwnedSidecar));
            // The user-owned lookalike is now processed as an ordinary source (M-05),
            // but it must never be claimed as a managed plugin output itself.
            AssertNotTracked(context, outDir, userOwnedVariant, userOwnedSidecar);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_HistoricalSizeUserLookalikeWithoutOwnership_IsNeverDeleted()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
            var userOwnedVariant = Path.Combine(assetsDir, "photo-300w.jpg");
            WriteValidImage(userOwnedVariant, "user-owned");
            var userOwnedBytes = await File.ReadAllBytesAsync(userOwnedVariant);

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            Assert.Equal(userOwnedBytes, await File.ReadAllBytesAsync(userOwnedVariant));
            Assert.True(File.Exists(Path.Combine(assetsDir, "photo-480w.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CopiedValidSidecarAcrossDirectories_DoesNotAuthorizeTracking()
    {
        var outDir = GetTempDir();
        try
        {
            var ownedDir = Path.Combine(outDir, "assets", "uploads", "owned");
            var copiedDir = Path.Combine(outDir, "assets", "uploads", "copied");
            Directory.CreateDirectory(ownedDir);
            Directory.CreateDirectory(copiedDir);
            var ownedSource = Path.Combine(ownedDir, "photo.jpg");
            WriteValidImage(ownedSource, "same-source");
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
            var outputs = Assert.IsType<HashSet<PluginOutputTrackingInfo>>(context.Get(BuildDataKeys.PluginOutputs));
            Assert.DoesNotContain(outputs, output => output.Path == "assets/uploads/copied/photo-300w.jpg");
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CurrentManagedPathWithUserReplacedBytes_IsNotTrackedOrOverwritten()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
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
            // User-replaced bytes remain untouched; the file may be resized as a user
            // source but is never tracked as a managed output of this plugin.
            AssertNotTracked(context, outDir, variant, sidecar);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_HistoricalManagedPathWithUserReplacedBytes_IsNotDeleted()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");

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
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceRenamed_ReplacesOwnedOrphan()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(source, "source");
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-480w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            File.Move(source, Path.Combine(assetsDir, "renamed.jpg"));
            var context = CreateContextWithPriorImageOwnership(outDir, variant);

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            Assert.False(File.Exists(variant));
            Assert.False(File.Exists(sidecar));
            Assert.True(File.Exists(Path.Combine(assetsDir, "renamed-480w.jpg")));
            Assert.True(context.TryGet(BuildDataKeys.PluginOutputs, out _));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_SourceRemoved_RemovesOwnedOrphan()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(source, "source");
            var config = CreateConfig(
                new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } });

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            var variant = Path.Combine(assetsDir, "photo-480w.jpg");
            var sidecar = variant + ".bukit-freshness.json";
            Assert.True(File.Exists(variant));
            Assert.True(File.Exists(sidecar));
            File.Delete(source);
            var context = CreateContextWithPriorImageOwnership(outDir, variant);

            await new ImageProcessingPlugin(config).AfterBuildAsync(context);

            Assert.False(File.Exists(variant));
            Assert.False(File.Exists(sidecar));
            Assert.False(context.TryGet(BuildDataKeys.PluginOutputs, out _));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_CurrentFullyRecomputedSpoofWithoutPriorManifest_IsNotTracked()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");
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
            // Without prior manifest ownership the spoofed file stays user data: it is
            // never overwritten and never claimed as a managed plugin output.
            AssertNotTracked(context, outDir, variant, sidecar);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_HistoricalFullyRecomputedSpoofWithoutPriorManifest_IsNotDeleted()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            WriteValidImage(Path.Combine(assetsDir, "photo.jpg"), "source");

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
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task AfterBuildAsync_OrphanFullyRecomputedSpoofWithoutPriorManifest_IsNotDeleted()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            var source = Path.Combine(assetsDir, "photo.jpg");
            WriteValidImage(source, "source");
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

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(outDir));

            Assert.Equal(spoofBytes, await File.ReadAllBytesAsync(variant));
            Assert.Equal(spoofSidecar, await File.ReadAllBytesAsync(sidecar));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task Process_UserOwnedWidthSuffixSource_IsNotSkipped()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets", "uploads");
            Directory.CreateDirectory(assetsDir);
            // User source whose filename happens to look like a generated variant.
            // Without ownership proof it must be treated as an ordinary input.
            WriteValidImage(Path.Combine(assetsDir, "banner-480w.jpg"), "user-owned");

            await new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(CreateContext(outDir));

            Assert.True(File.Exists(Path.Combine(assetsDir, "banner-480w-480w.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public async Task Resize_UndecodableSource_IsNotTrackedOrPublished()
    {
        var outDir = GetTempDir();
        try
        {
            var assetsDir = Path.Combine(outDir, "assets");
            Directory.CreateDirectory(assetsDir);
            WriteUndecodableImage(Path.Combine(assetsDir, "hero.jpg"), "hero");
            var context = CreateContext(outDir);

            await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageProcessingPlugin(CreateConfig(
                    new ImageOptimizationConfig { Enabled = true, Sizes = new[] { 480 } }))
                .AfterBuildAsync(context));

            Assert.False(File.Exists(Path.Combine(assetsDir, "hero-480w.jpg")));
            Assert.False(File.Exists(Path.Combine(assetsDir, "hero-480w.jpg.bukit-freshness.json")));
            Assert.False(context.TryGet(BuildDataKeys.PluginOutputs, out _));
            Assert.Empty(Directory.GetFiles(assetsDir, ".hero-480w.bukit-*.jpg"));
            Assert.Empty(Directory.GetFiles(assetsDir, "hero-480w.jpg.bukit-freshness.json.bukit-*.tmp"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    private static void AssertNotTracked(BuildContext context, string outDir, params string[] paths)
    {
        if (!context.TryGet(BuildDataKeys.PluginOutputs, out var value) ||
            value is not HashSet<PluginOutputTrackingInfo> outputs)
        {
            return;
        }

        foreach (var path in paths)
        {
            var relative = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(outDir, path));
            Assert.DoesNotContain(
                outputs,
                output => string.Equals(output.Path, relative, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task AfterBuild_SharedImage_ReusesDefaultWebpVariantsWhenLanguageHasNoImages()
    {
        var root = GetTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            var source = Path.Combine(root, "assets", "hero.jpg");
            WriteValidImage(source, "shared", width: 1000);
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    BaseUrl = "/",
                    Languages = ["en", "zh-CN"],
                    SharedAssets = new SharedAssetsConfig { Enabled = true }
                },
                Content = TestContent.Markdown(),
                Theme = new ThemeConfig
                {
                    Images = new ImageOptimizationConfig
                    {
                        Enabled = true,
                        Formats = ["webp"],
                        Sizes = [480],
                        Quality = 80
                    }
                }
            };

            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(root));
            var sharedBefore = SnapshotFiles(Path.Combine(root, "assets"));

            var scope = SharedAssetScope.TryCreate(config, ["en", "zh-CN"], root, null);
            Assert.NotNull(scope);
            var languageOutput = Path.Combine(root, "zh-CN");
            Directory.CreateDirectory(languageOutput);
            var page = Path.Combine(languageOutput, "index.html");
            await File.WriteAllTextAsync(page, "<img src=\"/assets/hero.jpg\" alt=\"\" />");
            var languageContext = new BuildContext
            {
                RootDir = root,
                OutputDir = languageOutput,
                BaseUrl = "/zh-CN",
                LayoutsDir = root,
                RoutedDocuments = [],
                Logger = new ConsoleLogger(LogLevel.Error)
            };
            languageContext.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
            languageContext.Set(
                BuildDataKeys.SharedAssets,
                new SharedAssetBuildData(scope, ["assets/hero.jpg"]));

            await new ImageProcessingPlugin(config).AfterBuildAsync(languageContext);
            Assert.Equal(sharedBefore, SnapshotFiles(Path.Combine(root, "assets")));

            var html = await File.ReadAllTextAsync(page);
            Assert.Contains("<picture>", html, StringComparison.Ordinal);
            Assert.Contains("/assets/hero-480w.webp", html, StringComparison.Ordinal);
            Assert.DoesNotContain("/zh-CN/assets/hero", html, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task AfterBuild_SharedImage_MergesLanguageAndSharedWebpVariants()
    {
        var root = GetTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            WriteValidImage(Path.Combine(root, "assets", "hero.jpg"), "shared", width: 1000);
            var config = SharedWebpConfig();
            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(root));

            var ownSource = Path.Combine(root, "zh-CN", "assets", "own.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(ownSource)!);
            WriteValidImage(ownSource, "own", width: 1000);

            var (languageContext, page) = SharedLanguageContext(
                root,
                config,
                "/zh-CN",
                ["assets/hero.jpg"],
                "<img src=\"/assets/hero.jpg\" alt=\"\" />" +
                "<img src=\"/zh-CN/assets/own.jpg\" alt=\"\" />");

            await new ImageProcessingPlugin(config).AfterBuildAsync(languageContext);

            var html = await File.ReadAllTextAsync(page);
            Assert.Equal(2, CountOccurrences(html, "<picture>"));
            Assert.Contains("/assets/hero-480w.webp", html, StringComparison.Ordinal);
            Assert.Contains("/zh-CN/assets/own-480w.webp", html, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, "zh-CN", "assets", "own-480w.webp")));
            Assert.DoesNotContain("/zh-CN/assets/hero", html, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task AfterBuild_SharedImage_StaleSourceIsNotReferenced()
    {
        var root = GetTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            var source = Path.Combine(root, "assets", "hero.jpg");
            WriteValidImage(source, "original", width: 1000);
            var config = SharedWebpConfig();
            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(root));

            WriteValidImage(source, "changed", width: 1000);
            File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(1));
            var (languageContext, page) = SharedLanguageContext(
                root, config, "/zh-CN", ["assets/hero.jpg"], "<img src=\"/assets/hero.jpg\" alt=\"\" />");

            await new ImageProcessingPlugin(config).AfterBuildAsync(languageContext);

            Assert.DoesNotContain("<picture>", await File.ReadAllTextAsync(page), StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task AfterBuild_SharedImage_AllVariantsMissingLeavesImageInPlace()
    {
        var root = GetTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            var source = Path.Combine(root, "assets", "hero.jpg");
            WriteValidImage(source, "missing", width: 1000);
            var config = SharedWebpConfig(sizes: [480]);
            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(root));
            foreach (var variant in Directory.GetFiles(Path.Combine(root, "assets"), "hero-*.webp"))
            {
                File.Delete(variant);
                File.Delete(variant + ".bukit-freshness.json");
            }
            var (languageContext, page) = SharedLanguageContext(
                root,
                config,
                "/zh-CN",
                ["assets/hero.jpg"],
                "<img src=\"/assets/hero.jpg\" alt=\"\" />");

            await new ImageProcessingPlugin(config).AfterBuildAsync(languageContext);

            var html = await File.ReadAllTextAsync(page);
            Assert.DoesNotContain("<picture>", html, StringComparison.Ordinal);
            Assert.Contains("src=\"/assets/hero.jpg\"", html, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task AfterBuild_SharedImage_RepoBaseKeepsSingleRootAndSkipsMissingCandidates()
    {
        var root = GetTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            var source = Path.Combine(root, "assets", "hero.jpg");
            WriteValidImage(source, "repo", width: 1000);
            var config = SharedWebpConfig(sizes: [480, 768], baseUrl: "/repo");
            await new ImageProcessingPlugin(config).AfterBuildAsync(CreateContext(root));
            File.Delete(Path.Combine(root, "assets", "hero-768w.webp"));
            File.Delete(Path.Combine(root, "assets", "hero-768w.webp.bukit-freshness.json"));

            var (languageContext, page) = SharedLanguageContext(
                root,
                config,
                "/repo/zh-CN",
                ["assets/hero.jpg"],
                "<img src=\"/repo/assets/hero.jpg\" alt=\"\" />" +
                "<img src=\"/assets/uploads/x.jpg\" alt=\"\" />" +
                "<img src=\"/images/a.jpg\" alt=\"\" />");

            await new ImageProcessingPlugin(config).AfterBuildAsync(languageContext);

            var html = await File.ReadAllTextAsync(page);
            Assert.Equal(1, CountOccurrences(html, "<picture>"));
            Assert.Contains("/repo/assets/hero-480w.webp 480w", html, StringComparison.Ordinal);
            Assert.Contains("/repo/assets/hero-1000w.webp 1000w", html, StringComparison.Ordinal);
            Assert.DoesNotContain("768w", html, StringComparison.Ordinal);
            Assert.DoesNotContain("//assets", html, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task AfterBuild_SharedImage_CorruptSourceLogsSkipAndLeavesImage()
    {
        var root = GetTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            File.WriteAllText(Path.Combine(root, "assets", "broken.jpg"), "not an image");
            var config = SharedWebpConfig();
            var (languageContext, page) = SharedLanguageContext(
                root,
                config,
                "/zh-CN",
                ["assets/broken.jpg"],
                "<img src=\"/assets/broken.jpg\" alt=\"\" />",
                new ResizeWarningLogger(new ConsoleLogger(LogLevel.Error)));

            await new ImageProcessingPlugin(config).AfterBuildAsync(languageContext);

            Assert.Contains(
                ((ResizeWarningLogger)languageContext.Logger).Warnings,
                message => message.Contains("event=image_processing.skip", StringComparison.Ordinal));
            var html = await File.ReadAllTextAsync(page);
            Assert.DoesNotContain("<picture>", html, StringComparison.Ordinal);
            Assert.Contains("src=\"/assets/broken.jpg\"", html, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    private static AppConfig SharedWebpConfig(int[]? sizes = null, string baseUrl = "/") => new()
    {
        Site = new SiteConfig
        {
            Name = "t",
            Title = "t",
            BaseUrl = baseUrl,
            Languages = ["en", "zh-CN"],
            SharedAssets = new SharedAssetsConfig { Enabled = true }
        },
        Content = TestContent.Markdown(),
        Theme = new ThemeConfig
        {
            Images = new ImageOptimizationConfig
            {
                Enabled = true,
                Formats = ["webp"],
                Sizes = sizes ?? [480],
                Quality = 80
            }
        }
    };

    private static (BuildContext Context, string Page) SharedLanguageContext(
        string root,
        AppConfig config,
        string baseUrl,
        string[] destinations,
        string html,
        ILogger? logger = null)
    {
        var scope = SharedAssetScope.TryCreate(config, ["en", "zh-CN"], root, null);
        Assert.NotNull(scope);
        var languageOutput = Path.Combine(root, "zh-CN");
        Directory.CreateDirectory(languageOutput);
        var page = Path.Combine(languageOutput, "index.html");
        File.WriteAllText(page, html);
        var context = new BuildContext
        {
            RootDir = root,
            OutputDir = languageOutput,
            BaseUrl = baseUrl,
            LayoutsDir = root,
            RoutedDocuments = [],
            Logger = logger ?? new ConsoleLogger(LogLevel.Error)
        };
        context.Set(BuildDataKeys.CurrentHtmlOutputs, new[] { "index.html" });
        context.Set(BuildDataKeys.SharedAssets, new SharedAssetBuildData(scope, destinations));
        return (context, page);
    }

    private static Dictionary<string, (long Length, DateTime LastWrite)> SnapshotFiles(string directory)
        => Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
                    path => (new FileInfo(path).Length, File.GetLastWriteTimeUtc(path)),
                    StringComparer.Ordinal)
            : new Dictionary<string, (long, DateTime)>(StringComparer.Ordinal);

    private static void WriteValidImage(string path, string seed, int width = 1600)
    {
        var hash = 0;
        foreach (var character in seed)
        {
            hash = unchecked(hash * 31 + character);
        }

        var color = new SKColor(
            (byte)(hash & 0xFF), (byte)((hash >> 8) & 0xFF), (byte)((hash >> 16) & 0xFF));
        using var image = new SKBitmap(width, 8);
        image.Erase(color);
        image.SetPixel(width - 1, 7, SKColors.White);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        File.WriteAllBytes(path, encoded.ToArray());
    }

    // Keeps every header segment (so planning reads the width) but cuts the
    // scan data in half, so SkiaSharp cannot fully decode the pixels.
    private static void WriteUndecodableImage(string path, string seed, int width = 1600)
    {
        WriteValidImage(path, seed, width);
        var bytes = File.ReadAllBytes(path);
        var startOfScan = bytes.AsSpan().IndexOf((ReadOnlySpan<byte>)[0xFF, 0xDA]);
        Assert.True(startOfScan > 0);
        var scanData = startOfScan + 2 + (bytes[startOfScan + 2] << 8 | bytes[startOfScan + 3]);
        File.WriteAllBytes(path, bytes[..(scanData + (bytes.Length - scanData) / 2)]);
    }

    private static string EscapeSingleQuoted(string value) => value.Replace("'", "'\\''", StringComparison.Ordinal);

    private static int CountOccurrences(string value, string needle) =>
        value.Split(needle, StringSplitOptions.None).Length - 1;

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

        context.Set(BuildDataKeys.PriorPluginOutputs, outputs);
        return context;
    }

    private static void WriteTool(string directory, string name, string body)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/bash\n" + body + "\n");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void RequireUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("This PATH probe uses temporary Unix executables.");
        }
    }

    private static string PrependPath(string directory, string? originalPath) =>
        string.IsNullOrEmpty(originalPath) ? directory : directory + Path.PathSeparator + originalPath;

    private static AppConfig CreateConfig(ImageOptimizationConfig images) => new()
    {
        Site = new SiteConfig { Name = "t", Title = "t" },
        Content = TestContent.Markdown(),
        Theme = new ThemeConfig { Images = images }
    };
}
