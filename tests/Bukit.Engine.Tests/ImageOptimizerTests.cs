using Bukit.Config;
using Bukit.Shared;
using SixLabors.ImageSharp;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class ImageOptimizerTests
{
    [Fact]
    public async Task OptimizeIfEnabled_GeneratesValidatedWebpWithoutExternalTools()
    {
        var root = CreateRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            var toolDir = Path.Combine(root, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            var input = Path.Combine(assetsDir, "photo.jpg");
            using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(12, 8))
                image.SaveAsJpeg(input);
            Environment.SetEnvironmentVariable("PATH", toolDir);
            var logger = new RecordingLogger();

            await ImageOptimizer.OptimizeIfEnabled(
                assetsDir,
                new ImageOptimizationConfig
                {
                    Enabled = true,
                    Formats = new[] { "webp", "WEBP", "WebP" },
                    Quality = 73
                },
                logger);

            var output = Path.ChangeExtension(input, ".webp");
            Assert.True(File.Exists(output));
            using var decoded = await Image.LoadAsync(output);
            Assert.Equal(12, decoded.Width);
            Assert.Equal(8, decoded.Height);
            Assert.Single(logger.Infos, message => message.StartsWith("event=image_optimize.ok", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, new string[0])]
    [InlineData(true, new[] { "avif" })]
    public async Task OptimizeIfEnabled_DoesNotGenerateWebpWhenNotEnabledOrNotRequested(
        bool enabled,
        string[] formats)
    {
        var root = CreateRoot();
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            Directory.CreateDirectory(assetsDir);
            var input = Path.Combine(assetsDir, "photo.png");
            using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 4))
                image.SaveAsPng(input);

            await ImageOptimizer.OptimizeIfEnabled(
                assetsDir,
                new ImageOptimizationConfig { Enabled = enabled, Formats = formats },
                new RecordingLogger());

            Assert.False(File.Exists(Path.ChangeExtension(input, ".webp")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EncodeWebpAsync_ResizesWithoutUpscalingAndPreservesAlpha()
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "transparent.png");
            var resized = Path.Combine(root, "resized.webp");
            var notUpscaled = Path.Combine(root, "not-upscaled.webp");
            using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(20, 10, new(20, 40, 60, 0)))
                image.SaveAsPng(input);

            await ImageOptimizer.EncodeWebpAsync(input, resized, 10, 80, CancellationToken.None);
            await ImageOptimizer.EncodeWebpAsync(input, notUpscaled, 40, 80, CancellationToken.None);

            using var resizedImage = await Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(resized);
            using var originalSizeImage = await Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(notUpscaled);
            Assert.Equal((10, 5), (resizedImage.Width, resizedImage.Height));
            Assert.Equal((20, 10), (originalSizeImage.Width, originalSizeImage.Height));
            Assert.Equal(0, resizedImage[0, 0].A);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EncodeWebpAsync_CancellationDoesNotLeaveOutput()
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "photo.png");
            var output = Path.Combine(root, "photo.webp");
            using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 4))
                image.SaveAsPng(input);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ImageOptimizer.EncodeWebpAsync(input, output, null, 80, cancellation.Token));

            Assert.False(File.Exists(output));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OptimizeIfEnabled_AvifDoesNotProbeOrInvokeCwebp()
    {
        RequireUnix();
        var root = CreateRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalLog = Environment.GetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG");
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            var toolDir = Path.Combine(root, "tools");
            var logPath = Path.Combine(root, "tool.log");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            var input = Path.Combine(assetsDir, "photo.jpg");
            File.WriteAllText(input, "input");
            WriteTool(toolDir, "cwebp", """
                printf '%s\n' "$*" >> "$BUKIT_IMAGE_TOOL_LOG"
                if [ "$1" = "-version" ]; then exit 0; fi
                for last in "$@"; do :; done
                printf converted > "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", toolDir);
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG", logPath);

            await ImageOptimizer.OptimizeIfEnabled(
                assetsDir,
                new ImageOptimizationConfig { Enabled = true, Formats = new[] { "avif" } },
                new ConsoleLogger(LogLevel.Error));

            Assert.False(File.Exists(Path.ChangeExtension(input, ".avif")));
            Assert.False(File.Exists(logPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG", originalLog);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OptimizeIfEnabled_InvalidSourceFailsWithoutPublishingOrTemporaryFiles()
    {
        RequireUnix();
        var root = CreateRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            Directory.CreateDirectory(assetsDir);
            var input = Path.Combine(assetsDir, "photo.jpg");
            File.WriteAllText(input, "input");
            var logger = new RecordingLogger();

            await ImageOptimizer.OptimizeIfEnabled(
                assetsDir,
                new ImageOptimizationConfig
                {
                    Enabled = true,
                    Formats = new[] { "webp" },
                    Quality = 80
                },
                logger);

            Assert.False(File.Exists(Path.ChangeExtension(input, ".webp")));
            Assert.All(
                Directory.EnumerateFiles(assetsDir),
                file => Assert.DoesNotContain(".bukit-", Path.GetFileName(file), StringComparison.Ordinal));
            var warning = Assert.Single(
                logger.Warnings,
                message => message.StartsWith("event=image_optimize.error", StringComparison.Ordinal));
            Assert.Contains("reason=", warning, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), "bukit-image-optimizer-" + Guid.NewGuid().ToString("N"));

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
            throw SkipException.ForSkip("This command-matrix test uses temporary Unix executables.");
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = [];
        public List<string> Warnings { get; } = [];

        public void Debug(string message) { }

        public void Info(string message) => Infos.Add(message);

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message) { }
    }
}
