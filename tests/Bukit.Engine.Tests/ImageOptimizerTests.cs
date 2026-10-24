using System.Buffers.Binary;
using Bukit.Config;
using Bukit.Shared;
using SkiaSharp;
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
            using (var image = new SKBitmap(12, 8))
            {
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                File.WriteAllBytes(input, encoded.ToArray());
            }
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
            using var decoded = SKBitmap.Decode(output);
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
            using (var image = new SKBitmap(4, 4))
            {
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(input, encoded.ToArray());
            }

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
            using (var image = new SKBitmap(20, 10))
            {
                image.Erase(SKColors.Transparent);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(input, encoded.ToArray());
            }

            await ImageOptimizer.EncodeWebpAsync(input, resized, 10, 80, CancellationToken.None);
            await ImageOptimizer.EncodeWebpAsync(input, notUpscaled, 40, 80, CancellationToken.None);

            using var resizedImage = SKBitmap.Decode(resized);
            using var originalSizeImage = SKBitmap.Decode(notUpscaled);
            Assert.Equal((10, 5), (resizedImage.Width, resizedImage.Height));
            Assert.Equal((20, 10), (originalSizeImage.Width, originalSizeImage.Height));
            Assert.Equal(0, resizedImage.GetPixel(0, 0).Alpha);
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
            using (var image = new SKBitmap(4, 4))
            {
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(input, encoded.ToArray());
            }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncodeWebpAsync_RejectsTruncatedPixelsAndNonJpegPngInputs(bool nonJpegPng)
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            using var image = new SKBitmap(128, 128);
            var random = new Random(7);
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                image.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
            using var encoded = image.Encode(nonJpegPng ? SKEncodedImageFormat.Webp : SKEncodedImageFormat.Jpeg, 90);
            var bytes = encoded.ToArray();
            var input = Path.Combine(root, "source.jpg");
            var output = Path.Combine(root, "output.webp");
            File.WriteAllBytes(input, nonJpegPng ? bytes : bytes[..(bytes.Length / 2)]);

            await Assert.ThrowsAsync<InvalidDataException>(() => ImageOptimizer.EncodeWebpAsync(input, output, 64, 80, CancellationToken.None));

            Assert.False(File.Exists(output));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Webp)]
    [InlineData(SKEncodedImageFormat.Png)]
    public async Task EncodeAsync_AnimatedPngNeverFlattensFrames(SKEncodedImageFormat format)
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            // Two full 2x1 RGBA frames: red for 100 ms, blue for 200 ms, infinite loop.
            var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAACAAAAAQAAAAAAAAAAAAEACgAA+Sm2eQAAAA5JREFUeJxj+M/A8B+EARH3A/3jxfXvAAAAGmZjVEwAAAABAAAAAgAAAAEAAAAAAAAAAAACAAoAACX6Jn0AAAASZmRBVAAAAAJ4nGNgYPj/H4QBDfsD/a6UELEAAAAASUVORK5CYII=");
            var input = Path.Combine(root, "animated.png");
            var output = Path.Combine(root, "variant." + format.ToString().ToLowerInvariant());
            File.WriteAllBytes(input, bytes);
            await Assert.ThrowsAsync<NotSupportedException>(() => ImageOptimizer.EncodeAsync(input, output, 1, 100, format, CancellationToken.None));
            Assert.False(File.Exists(output));
            Assert.Equal(bytes, File.ReadAllBytes(input));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(SKEncodedOrigin.TopLeft)]
    [InlineData(SKEncodedOrigin.RightTop)]
    [InlineData(SKEncodedOrigin.TopRight)]
    [InlineData(SKEncodedOrigin.RightBottom)]
    [InlineData(SKEncodedOrigin.BottomRight)]
    [InlineData(SKEncodedOrigin.BottomLeft)]
    [InlineData(SKEncodedOrigin.LeftTop)]
    [InlineData(SKEncodedOrigin.LeftBottom)]
    public async Task EncodeWebpAsync_PreservesDisplayedOrientation(SKEncodedOrigin origin)
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "oriented.jpg");
            var output = Path.Combine(root, "oriented.webp");
            File.WriteAllBytes(input, CreateOrientedJpeg(origin));
            using var source = SKCodec.Create(input);
            Assert.Equal(origin, source.EncodedOrigin);

            await ImageOptimizer.EncodeWebpAsync(input, output, 20, 100, CancellationToken.None);

            using var result = SKBitmap.Decode(output);
            Assert.Equal(origin < SKEncodedOrigin.LeftTop ? (20, 10) : (20, 40), (result.Width, result.Height));
            var expected = origin switch
            {
                SKEncodedOrigin.RightTop => new[] { SKColors.Blue, SKColors.Red, SKColors.Yellow, SKColors.Lime },
                SKEncodedOrigin.TopRight => new[] { SKColors.Lime, SKColors.Red, SKColors.Yellow, SKColors.Blue },
                SKEncodedOrigin.RightBottom => new[] { SKColors.Yellow, SKColors.Lime, SKColors.Blue, SKColors.Red },
                SKEncodedOrigin.BottomRight => new[] { SKColors.Yellow, SKColors.Blue, SKColors.Lime, SKColors.Red },
                SKEncodedOrigin.BottomLeft => new[] { SKColors.Blue, SKColors.Yellow, SKColors.Red, SKColors.Lime },
                SKEncodedOrigin.LeftTop => new[] { SKColors.Red, SKColors.Blue, SKColors.Lime, SKColors.Yellow },
                SKEncodedOrigin.LeftBottom => new[] { SKColors.Lime, SKColors.Yellow, SKColors.Red, SKColors.Blue },
                _ => new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow }
            };
            AssertCorners(result, expected);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(SKEncodedOrigin.TopLeft)]
    [InlineData(SKEncodedOrigin.RightTop)]
    public async Task EncodeWebpAsync_ConvertsLinearRgbToSrgbThroughResize(SKEncodedOrigin origin)
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            using var linear = SKColorSpace.CreateSrgbLinear();
            using var image = new SKBitmap(new SKImageInfo(32, 16, SKColorType.Rgba8888, SKAlphaType.Unpremul, linear));
            var pixels = image.GetPixelSpan();
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = pixels[i + 1] = pixels[i + 2] = 64;
                pixels[i + 3] = 255;
            }
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            var input = Path.Combine(root, "linear.jpg");
            var output = Path.Combine(root, "srgb.webp");
            File.WriteAllBytes(input, WithJpegOrientation(encoded.ToArray(), origin));
            using var source = SKCodec.Create(input);
            Assert.NotNull(source.Info.ColorSpace);
            Assert.True(source.Info.ColorSpace.GammaIsLinear);

            await ImageOptimizer.EncodeWebpAsync(input, output, 16, 100, CancellationToken.None);

            using var codec = SKCodec.Create(output);
            using var srgb = SKColorSpace.CreateSrgb();
            var height = origin == SKEncodedOrigin.TopLeft ? 8 : 32;
            using var decoded = new SKBitmap(new SKImageInfo(16, height, SKColorType.Rgba8888, SKAlphaType.Unpremul, srgb));
            Assert.Equal(SKCodecResult.Success, codec.GetPixels(decoded.Info, decoded.GetPixels()));
            var color = decoded.GetPixel(8, 4);
            Assert.InRange(color.Red, 133, 141);
            Assert.InRange(color.Green, 133, 141);
            Assert.InRange(color.Blue, 133, 141);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false, SKEncodedOrigin.RightTop, 60, 60, 30)]
    [InlineData(true, SKEncodedOrigin.RightTop, 60, 60, 30)]
    [InlineData(false, SKEncodedOrigin.TopRight, 60, 40, 80)]
    [InlineData(true, SKEncodedOrigin.RightBottom, 120, 80, 40)]
    public async Task EncodeWebpAsync_PngExifPreservesDisplayedPixelsAndDoesNotUpscale(
        bool bigEndian, SKEncodedOrigin origin, int requestedWidth, int width, int height)
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "oriented.png");
            var output = Path.Combine(root, "oriented.webp");
            File.WriteAllBytes(input, CreateOrientedPng(origin, bigEndian));
            using var source = SKCodec.Create(input);
            Assert.Equal(SKEncodedOrigin.TopLeft, source.EncodedOrigin); // The native PNG codec ignores eXIf.

            await ImageOptimizer.EncodeWebpAsync(input, output, requestedWidth, 100, CancellationToken.None);

            using var result = SKBitmap.Decode(output);
            Assert.Equal((width, height), (result.Width, result.Height));
            AssertCorners(result, origin switch
            {
                SKEncodedOrigin.RightTop => [SKColors.Blue, SKColors.Red, SKColors.Yellow, SKColors.Lime],
                SKEncodedOrigin.TopRight => [SKColors.Lime, SKColors.Red, SKColors.Yellow, SKColors.Blue],
                _ => [SKColors.Yellow, SKColors.Lime, SKColors.Blue, SKColors.Red]
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("ifd-offset")]
    [InlineData("ifd-count")]
    [InlineData("field-count")]
    [InlineData("field-type")]
    [InlineData("orientation")]
    [InlineData("duplicate")]
    [InlineData("crc")]
    [InlineData("crc-tail")]
    [InlineData("chunk-length")]
    [InlineData("header")]
    [InlineData("truncated-ifd")]
    public async Task EncodeWebpAsync_MalformedPngExifFailsWithoutOutput(string malformed)
    {
        var root = CreateRoot();
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "malformed.png");
            var output = Path.Combine(root, "malformed.webp");
            File.WriteAllBytes(input, CreateOrientedPng(SKEncodedOrigin.RightTop, false, malformed));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ImageOptimizer.EncodeWebpAsync(input, output, 20, 100, CancellationToken.None));
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(root, true); }
    }

    internal static byte[] CreateOrientedPng(SKEncodedOrigin origin, bool bigEndian = false, string? malformed = null)
    {
        using var srgb = SKColorSpace.CreateSrgb();
        using var image = new SKBitmap(new SKImageInfo(40, 80, SKColorType.Rgba8888, SKAlphaType.Premul, srgb));
        using (var canvas = new SKCanvas(image))
        using (var paint = new SKPaint())
        {
            foreach (var (rect, color) in new[]
            {
                (new SKRect(0, 0, 20, 40), SKColors.Red), (new SKRect(20, 0, 40, 40), SKColors.Lime),
                (new SKRect(0, 40, 20, 80), SKColors.Blue), (new SKRect(20, 40, 40, 80), SKColors.Yellow)
            })
            {
                paint.Color = color.WithAlpha(128);
                canvas.DrawRect(rect, paint);
            }
        }
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();
        var exif = new byte[38 + 2048];
        exif[0] = exif[1] = (byte)(bigEndian ? 'M' : 'I');
        Write16(2, 42);
        Write32(4, malformed == "ifd-offset" ? uint.MaxValue : 8);
        Write16(8, malformed == "ifd-count" ? ushort.MaxValue : (ushort)2);
        Write16(10, 0x10e); // ImageDescription makes the CRC span multiple fixed-size reads.
        Write16(12, 2);
        Write32(14, 2048);
        Write32(18, 38);
        exif.AsSpan(38, 2047).Fill((byte)'A');
        Write16(22, 0x112);
        Write16(24, malformed == "field-type" ? (ushort)4 : (ushort)3);
        Write32(26, malformed == "field-count" ? uint.MaxValue : 1);
        Write16(30, malformed == "orientation" ? (ushort)9 : (ushort)origin);
        if (malformed == "header") exif[2] ^= 1;
        if (malformed == "truncated-ifd") exif = exif[..37];
        var chunk = new byte[exif.Length + 12];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)exif.Length);
        "eXIf"u8.CopyTo(chunk.AsSpan(4));
        exif.CopyTo(chunk, 8);
        uint crc = uint.MaxValue;
        foreach (var value in chunk.AsSpan(4, exif.Length + 4))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(chunk.Length - 4), ~crc);
        if (malformed == "crc") chunk[^1] ^= 1;
        if (malformed == "crc-tail") chunk[^5] ^= 1;
        if (malformed == "chunk-length") BinaryPrimitives.WriteUInt32BigEndian(chunk, uint.MaxValue);
        return malformed == "duplicate"
            ? [.. png[..33], .. chunk, .. chunk, .. png[33..]]
            : [.. png[..33], .. chunk, .. png[33..]];

        void Write16(int offset, ushort value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(exif.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(offset), value);
        }
        void Write32(int offset, uint value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(exif.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt32LittleEndian(exif.AsSpan(offset), value);
        }
    }

    internal static byte[] CreateOrientedJpeg(SKEncodedOrigin origin, int width = 80, int height = 40)
    {
        using var image = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(image))
        using (var paint = new SKPaint())
        {
            foreach (var (rect, color) in new[]
            {
                (new SKRect(0, 0, width / 2, height / 2), SKColors.Red), (new SKRect(width / 2, 0, width, height / 2), SKColors.Lime),
                (new SKRect(0, height / 2, width / 2, height), SKColors.Blue), (new SKRect(width / 2, height / 2, width, height), SKColors.Yellow)
            })
            {
                paint.Color = color;
                canvas.DrawRect(rect, paint);
            }
        }
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return WithJpegOrientation(data.ToArray(), origin);
    }

    private static byte[] WithJpegOrientation(byte[] jpeg, SKEncodedOrigin origin)
    {
        byte[] exif = [255, 225, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0,
            1, 0, 18, 1, 3, 0, 1, 0, 0, 0, (byte)origin, 0, 0, 0, 0, 0, 0, 0];
        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    private static void AssertCorners(SKBitmap image, SKColor[] expected)
    {
        var positions = new[] { (image.Width / 4, image.Height / 4), (image.Width * 3 / 4, image.Height / 4),
            (image.Width / 4, image.Height * 3 / 4), (image.Width * 3 / 4, image.Height * 3 / 4) };
        for (var i = 0; i < positions.Length; i++)
        {
            var actual = image.GetPixel(positions[i].Item1, positions[i].Item2);
            Assert.InRange(Math.Abs(actual.Red - expected[i].Red), 0, 20);
            Assert.InRange(Math.Abs(actual.Green - expected[i].Green), 0, 20);
            Assert.InRange(Math.Abs(actual.Blue - expected[i].Blue), 0, 20);
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
