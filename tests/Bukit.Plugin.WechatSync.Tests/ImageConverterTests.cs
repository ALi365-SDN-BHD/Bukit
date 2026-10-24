using System.Buffers.Binary;
using Bukit.Shared;
using Bukit.WechatSyncing;
using SkiaSharp;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class ImageConverterTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg", ".jpg")]
    [InlineData(SKEncodedImageFormat.Png, "image/png", ".png")]
    public void NormalizeForUpload_SupportedAtInclusiveLimit_PreservesOriginalBytes(SKEncodedImageFormat format, string mime, string extension)
    {
        using var image = new SKBitmap(8, 4);
        image.Erase(SKColors.CornflowerBlue);
        using var encoded = image.Encode(format, 85);
        var bytes = encoded.ToArray();
        var result = ImageConverter.NormalizeForUpload(bytes, bytes.Length);

        Assert.NotNull(result);
        Assert.Same(bytes, result.Value.Bytes);
        Assert.Equal(mime, result.Value.ContentType);
        Assert.Equal(extension, result.Value.Extension);
    }

    [Fact]
    public void NormalizeForUpload_Apng_PreservesPassthroughButRejectsFlattening()
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAACAAAAAQAAAAAAAAAAAAEACgAA+Sm2eQAAAA5JREFUeJxj+M/A8B+EARH3A/3jxfXvAAAAGmZjVEwAAAABAAAAAgAAAAEAAAAAAAAAAAACAAoAACX6Jn0AAAASZmRBVAAAAAJ4nGNgYPj/H4QBDfsD/a6UELEAAAAASUVORK5CYII=");
        var passthrough = ImageConverter.NormalizeForUpload(bytes, bytes.Length);
        Assert.NotNull(passthrough);
        Assert.Same(bytes, passthrough.Value.Bytes);
        Assert.Null(ImageConverter.NormalizeForUpload(bytes, bytes.Length - 1));
    }

    [Fact]
    public void NormalizeForUpload_Webp_PrefersPngAndPreservesAlpha()
    {
        using var image = new SKBitmap(8, 4);
        image.Erase(new SKColor(80, 40, 20, 64));
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 100);

        var result = ImageConverter.NormalizeForUpload(encoded.ToArray(), ImageConverter.ContentImageMaxBytes);

        Assert.NotNull(result);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.Equal(".png", result.Value.Extension);
        using var decoded = DecodeComplete(result.Value.Bytes);
        Assert.Equal((8, 4), (decoded.Width, decoded.Height));
        Assert.Equal(64, decoded.GetPixel(0, 0).Alpha);
    }

    [Theory]
    [InlineData("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==")]
    [InlineData("Qk06AAAAAAAAADYAAAAoAAAAAQAAAAEAAAABABgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP8A")]
    public void NormalizeForUpload_GifAndBmp_ProduceCompletePng(string fixture)
    {
        var result = ImageConverter.NormalizeForUpload(Convert.FromBase64String(fixture), 10000);
        Assert.NotNull(result);
        Assert.Equal("image/png", result.Value.ContentType);
        using var decoded = DecodeComplete(result.Value.Bytes);
        Assert.Equal((1, 1), (decoded.Width, decoded.Height));
    }

    [Theory]
    [InlineData(85)]
    [InlineData(75)]
    [InlineData(65)]
    [InlineData(55)]
    [InlineData(30)]
    public void NormalizeForUpload_PngTooLarge_FallsBackThroughJpegQualityLimits(int quality)
    {
        using var image = CreateNoise();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        var maxBytes = (int)jpeg.Size;
        var logger = new RecordingLogger();

        var result = ImageConverter.NormalizeForUpload(png.ToArray(), maxBytes, logger);

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.Value.ContentType);
        Assert.Equal(".jpg", result.Value.Extension);
        Assert.InRange(result.Value.Bytes.Length, 1, maxBytes);
        using var decoded = DecodeComplete(result.Value.Bytes);
        Assert.Equal((128, 128), (decoded.Width, decoded.Height));
        if (quality < 85) Assert.Contains(logger.Messages, message => message.Contains($"quality={quality}", StringComparison.Ordinal));
        Assert.Null(ImageConverter.NormalizeForUpload(png.ToArray(), 1));
    }

    [Fact]
    public void NormalizeForUpload_StrictLimit_NeverReturnsBoundarySizedPayload()
    {
        using var image = CreateNoise();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = png.ToArray();

        var result = ImageConverter.NormalizeForUpload(bytes, bytes.Length, requireStrictlyBelowMaxBytes: true);

        Assert.NotNull(result);
        Assert.True(result.Value.Bytes.Length < bytes.Length);
        using var decoded = DecodeComplete(result.Value.Bytes);
        Assert.Equal(128, decoded.Width);
    }

    [Fact]
    public void NormalizeForUpload_TruncatedScanAndMalformedSource_ReturnNullAndLog()
    {
        using var image = CreateNoise();
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var bytes = encoded.ToArray();
        var logger = new RecordingLogger();
        Assert.Null(ImageConverter.NormalizeForUpload(bytes[..(bytes.Length / 2)], 1, logger));
        Assert.Contains(logger.Messages, message => message.Contains("conversion failed", StringComparison.Ordinal));
        Assert.Null(ImageConverter.NormalizeForUpload("not an image"u8.ToArray(), 1000));
        Assert.Null(ImageConverter.NormalizeForUpload([], 1000));
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
    public void NormalizeForUpload_PreservesDisplayedJpegOrientation(SKEncodedOrigin origin)
    {
        using var srgb = SKColorSpace.CreateSrgb();
        using var image = new SKBitmap(new SKImageInfo(80, 40, SKColorType.Rgba8888, SKAlphaType.Premul, srgb));
        using (var canvas = new SKCanvas(image))
        using (var paint = new SKPaint())
        {
            foreach (var (rect, color) in new[]
            {
                (new SKRect(0, 0, 40, 20), SKColors.Red), (new SKRect(40, 0, 80, 20), SKColors.Lime),
                (new SKRect(0, 20, 40, 40), SKColors.Blue), (new SKRect(40, 20, 80, 40), SKColors.Yellow)
            })
            {
                paint.Color = color;
                canvas.DrawRect(rect, paint);
            }
        }
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        var bytes = WithJpegOrientation(encoded.ToArray(), origin);
        using var sourceStream = new MemoryStream(bytes);
        using var source = SKCodec.Create(sourceStream);
        Assert.Equal(origin, source.EncodedOrigin);

        var result = ImageConverter.NormalizeForUpload(bytes, bytes.Length - 1);

        Assert.NotNull(result);
        using var decoded = DecodeComplete(result.Value.Bytes);
        Assert.Equal(origin < SKEncodedOrigin.LeftTop ? (80, 40) : (40, 80), (decoded.Width, decoded.Height));
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
        var positions = new[] { (decoded.Width / 4, decoded.Height / 4), (decoded.Width * 3 / 4, decoded.Height / 4),
            (decoded.Width / 4, decoded.Height * 3 / 4), (decoded.Width * 3 / 4, decoded.Height * 3 / 4) };
        for (var i = 0; i < 4; i++)
        {
            var actual = decoded.GetPixel(positions[i].Item1, positions[i].Item2);
            Assert.InRange(Math.Abs(actual.Red - expected[i].Red), 0, 20);
            Assert.InRange(Math.Abs(actual.Green - expected[i].Green), 0, 20);
            Assert.InRange(Math.Abs(actual.Blue - expected[i].Blue), 0, 20);
        }
    }

    [Theory]
    [InlineData(SKEncodedOrigin.TopLeft)]
    [InlineData(SKEncodedOrigin.RightTop)]
    public void NormalizeForUpload_ConvertsLinearRgbToSrgb(SKEncodedOrigin origin)
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
        var bytes = WithJpegOrientation(encoded.ToArray(), origin);
        using var sourceStream = new MemoryStream(bytes);
        using var source = SKCodec.Create(sourceStream);
        Assert.True(source.Info.ColorSpace.GammaIsLinear);

        var result = ImageConverter.NormalizeForUpload(bytes, bytes.Length - 1);

        Assert.NotNull(result);
        using var stream = new MemoryStream(result.Value.Bytes);
        using var codec = SKCodec.Create(stream);
        using var srgb = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(32, 16, SKColorType.Rgba8888, SKAlphaType.Unpremul, srgb);
        using var decoded = new SKBitmap(origin == SKEncodedOrigin.TopLeft ? info : info.WithSize(16, 32));
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(decoded.Info, decoded.GetPixels()));
        var color = decoded.GetPixel(decoded.Width / 2, decoded.Height / 2);
        Assert.InRange(color.Red, 133, 141);
        Assert.InRange(color.Green, 133, 141);
        Assert.InRange(color.Blue, 133, 141);
    }

    [Fact]
    public void NormalizeForUpload_AnimatedGifAndWebp_RejectsFlattening()
    {
        // Two valid 1x1 GIF frames, with different palette indices.
        byte[] gif = [71,73,70,56,57,97,1,0,1,0,128,0,0,255,0,0,0,0,255,
            33,249,4,4,10,0,0,0,44,0,0,0,0,1,0,1,0,0,2,2,68,1,0,
            33,249,4,4,20,0,0,0,44,0,0,0,0,1,0,1,0,0,2,2,76,1,0,59];
        using var first = new SKBitmap(2, 2);
        using var second = new SKBitmap(2, 2);
        first.Erase(SKColors.Red);
        second.Erase(SKColors.Blue);
        using var firstPixels = first.PeekPixels();
        using var secondPixels = second.PeekPixels();
        using var webp = SKWebpEncoder.EncodeAnimated(
            [new SKWebpEncoderFrame(firstPixels, TimeSpan.FromMilliseconds(100)), new SKWebpEncoderFrame(secondPixels, TimeSpan.FromMilliseconds(200))],
            new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100));
        foreach (var bytes in new[] { gif, webp!.ToArray() })
        {
            using var stream = new MemoryStream(bytes);
            using var codec = SKCodec.Create(stream);
            Assert.Equal(2, codec.FrameCount);
            using var image = new SKBitmap(codec.Info);
            for (var frame = 0; frame < 2; frame++)
                Assert.Equal(SKCodecResult.Success, codec.GetPixels(image.Info, image.GetPixels(), new SKCodecOptions(frame)));
            Assert.Null(ImageConverter.NormalizeForUpload(bytes, 100000));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalizeForUpload_TranslucentSourceJpegFallback_KeepsUnassociatedColor(bool oriented)
    {
        using var image = CreateNoise();
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var color = x < 64 && y < 64 ? SKColors.Red : image.GetPixel(x, y);
            image.SetPixel(x, y, color.WithAlpha(128));
        }
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = png.ToArray();
        if (oriented)
        {
            // The pinned PNG codec ignores eXIf; WebP exposes EXIF together with alpha.
            using var pixels = image.PeekPixels();
            using var webp = pixels.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100));
            Assert.NotNull(webp);
            bytes = webp.ToArray();
            Assert.Equal("VP8X"u8.ToArray(), bytes[12..16]);
            bytes[20] |= 8;
            byte[] exif = [73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0];
            var chunk = new byte[exif.Length + 8];
            "EXIF"u8.CopyTo(chunk);
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), (uint)exif.Length);
            exif.CopyTo(chunk, 8);
            bytes = [.. bytes, .. chunk];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)bytes.Length - 8);
            using var stream = new MemoryStream(bytes);
            using var codec = SKCodec.Create(stream);
            Assert.Equal(SKEncodedOrigin.RightTop, codec.EncodedOrigin);
        }
        var result = ImageConverter.NormalizeForUpload(bytes, 12000);
        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.Value.ContentType);
        Assert.InRange(result.Value.Bytes.Length, 1, 12000);
        using var decoded = DecodeComplete(result.Value.Bytes);
        var patch = decoded.GetPixel(oriented ? image.Height - 25 : 24, 24);
        Assert.InRange(patch.Red, 240, 255);
        Assert.InRange(patch.Green, 0, 15);
        Assert.InRange(patch.Blue, 0, 15);
    }

    [Theory]
    [InlineData(false, SKEncodedOrigin.RightTop)]
    [InlineData(true, SKEncodedOrigin.RightTop)]
    [InlineData(false, SKEncodedOrigin.TopRight)]
    [InlineData(true, SKEncodedOrigin.RightBottom)]
    public void NormalizeForUpload_PngExifPreservesDisplayedPixelsAndAlpha(bool bigEndian, SKEncodedOrigin origin)
    {
        var bytes = CreateOrientedPng(origin, bigEndian);
        var passthrough = ImageConverter.NormalizeForUpload(bytes, bytes.Length);
        Assert.NotNull(passthrough);
        Assert.Same(bytes, passthrough.Value.Bytes);
        var result = ImageConverter.NormalizeForUpload(bytes, bytes.Length - 1);

        Assert.NotNull(result);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.True(result.Value.Bytes.Length < bytes.Length);
        using var decoded = DecodeComplete(result.Value.Bytes);
        Assert.Equal(origin == SKEncodedOrigin.TopRight ? (40, 80) : (80, 40), (decoded.Width, decoded.Height));
        var expected = origin switch
        {
            SKEncodedOrigin.RightTop => new[] { SKColors.Blue, SKColors.Red, SKColors.Yellow, SKColors.Lime },
            SKEncodedOrigin.TopRight => new[] { SKColors.Lime, SKColors.Red, SKColors.Yellow, SKColors.Blue },
            _ => new[] { SKColors.Yellow, SKColors.Lime, SKColors.Blue, SKColors.Red }
        };
        for (var i = 0; i < 4; i++)
        {
            var color = decoded.GetPixel(decoded.Width * (i % 2 == 0 ? 1 : 3) / 4, decoded.Height * (i < 2 ? 1 : 3) / 4);
            Assert.InRange(Math.Abs(color.Red - expected[i].Red), 0, 5);
            Assert.InRange(Math.Abs(color.Green - expected[i].Green), 0, 5);
            Assert.InRange(Math.Abs(color.Blue - expected[i].Blue), 0, 5);
            Assert.Equal(128, color.Alpha);
        }
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
    public void NormalizeForUpload_MalformedPngExifFailsClosed(string malformed)
    {
        var bytes = CreateOrientedPng(SKEncodedOrigin.RightTop, false, malformed);
        Assert.Null(ImageConverter.NormalizeForUpload(bytes, bytes.Length - 1));
    }

    private static byte[] CreateOrientedPng(SKEncodedOrigin origin, bool bigEndian, string? malformed = null)
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

    private static byte[] WithJpegOrientation(byte[] jpeg, SKEncodedOrigin origin)
    {
        byte[] exif = [255, 225, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0,
            1, 0, 18, 1, 3, 0, 1, 0, 0, 0, (byte)origin, 0, 0, 0, 0, 0, 0, 0];
        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    private static SKBitmap CreateNoise()
    {
        using var srgb = SKColorSpace.CreateSrgb();
        var image = new SKBitmap(new SKImageInfo(128, 128, SKColorType.Rgba8888, SKAlphaType.Unpremul, srgb));
        var random = new Random(9);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
            image.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        return image;
    }

    private static SKBitmap DecodeComplete(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        var image = new SKBitmap(codec.Info);
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(image.Info, image.GetPixels()));
        return image;
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public void Debug(string message) => Messages.Add(message);
        public void Info(string message) => Messages.Add(message);
        public void Warn(string message) => Messages.Add(message);
        public void Error(string message) => Messages.Add(message);
    }
}
