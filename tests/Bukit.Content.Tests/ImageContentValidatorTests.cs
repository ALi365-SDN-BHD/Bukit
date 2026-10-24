using System.Buffers.Binary;
using Bukit.Content.Media;
using SkiaSharp;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ImageContentValidatorTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpg")]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task ValidateAsync_CommonEncodedFormats_AcceptsCompletePixels(SKEncodedImageFormat format, string mime)
    {
        using var image = new SKBitmap(8, 3);
        image.Erase(new SKColor(20, 40, 60, 128));
        using var encoded = image.Encode(format, 80);
        Assert.True(await ValidateAsync(encoded.ToArray(), mime));
        Assert.False(await ValidateAsync(encoded.ToArray(), "image/gif"));
    }

    [Fact]
    public async Task ValidateAsync_Apng_RejectsIncompleteAnimationSupport()
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAACAAAAAQAAAAAAAAAAAAEACgAA+Sm2eQAAAA5JREFUeJxj+M/A8B+EARH3A/3jxfXvAAAAGmZjVEwAAAABAAAAAgAAAAEAAAAAAAAAAAACAAoAACX6Jn0AAAASZmRBVAAAAAJ4nGNgYPj/H4QBDfsD/a6UELEAAAAASUVORK5CYII=");
        Assert.False(await ValidateAsync(bytes, "image/png"));
    }

    [Fact]
    public async Task ValidateAsync_Bmp_AcceptsCompletePixelsAndRejectsTruncation()
    {
        var bytes = CreateBmp(12345);
        Assert.True(await ValidateAsync(bytes, "image/bmp"));
        Assert.False(await ValidateAsync(bytes[..^3], "image/bmp"));
    }

    [Fact]
    public async Task ValidateAsync_Gif_DecodesEveryFrameIncludingRestorePrevious()
    {
        var bytes = CreateGif(3);
        Assert.True(await ValidateAsync(bytes, "image/gif"));
        var truncated = bytes[..^5];
        using var stream = new MemoryStream(truncated);
        using var codec = SKCodec.Create(stream);
        Assert.Equal(3, codec.FrameCount);
        using var bitmap = new SKBitmap(codec.Info);
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(bitmap.Info, bitmap.GetPixels(), new SKCodecOptions(0)));
        Assert.NotEqual(SKCodecResult.Success, codec.GetPixels(bitmap.Info, bitmap.GetPixels(), new SKCodecOptions(2)));
        Assert.False(await ValidateAsync(truncated, "image/gif"));
    }

    [Fact]
    public async Task ValidateAsync_GifFrameLimit_UsesRealDistinctFrames()
    {
        var bytes = CreateGif(257);
        using var stream = new MemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        Assert.Equal(257, codec.FrameCount);
        using var bitmap = new SKBitmap(codec.Info);
        var frames = new HashSet<string>();
        for (var frame = 0; frame < codec.FrameCount; frame++)
        {
            Assert.Equal(SKCodecResult.Success, codec.GetPixels(bitmap.Info, bitmap.GetPixels(), new SKCodecOptions(frame)));
            frames.Add(Convert.ToHexString(bitmap.Bytes));
        }
        Assert.Equal(257, frames.Count);
        Assert.True(await ValidateAsync(CreateGif(256), "image/gif"));
        Assert.False(await ValidateAsync(bytes, "image/gif"));
    }

    [Fact]
    public async Task ValidateAsync_JpegWithIntactHeaderAndTruncatedScan_RejectsIncompleteInput()
    {
        using var bitmap = new SKBitmap(128, 128);
        var random = new Random(7);
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
            bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        var truncated = encoded.ToArray()[..((int)encoded.Size / 2)];
        using var stream = new MemoryStream(truncated);
        using var codec = SKCodec.Create(stream);
        Assert.Equal(128, codec.Info.Width);
        Assert.Equal(SKCodecResult.IncompleteInput, codec.GetPixels(bitmap.Info, bitmap.GetPixels()));
        Assert.False(await ValidateAsync(truncated, "image/jpeg"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateAsync_StandardTiff_AcceptsSingleAndDifferentSizedPages(bool bigEndian)
    {
        Assert.True(await ValidateAsync(CreateTiff(bigEndian, (2, 3)), "image/tiff"));
        Assert.True(await ValidateAsync(CreateTiff(bigEndian, (1, 1), (3, 5)), "image/tiff"));
        Assert.False(await ValidateAsync(CreateTiff(bigEndian, (1, 1), (3, 5))[..^1], "image/tiff"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(20000, 20000)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public async Task ValidateAsync_TiffInvalidDimensions_RejectsBeforePixels(int width, int height)
        => Assert.False(await ValidateAsync(CreateTiff(false, (width, height)), "image/tiff"));

    [Fact]
    public async Task ValidateAsync_Tiff_TotalBudgetCountsDifferentPageSizes()
        => Assert.False(await ValidateAsync(CreateTiff(false, (1, 1), (10000, 10000)), "image/tiff"));

    [Fact]
    public async Task ValidateAsync_Tiff_PageLimitAndCycleFailClosed()
    {
        Assert.True(await ValidateAsync(CreateTiff(false, Enumerable.Repeat((1, 1), 256).ToArray()), "image/tiff"));
        Assert.False(await ValidateAsync(CreateTiff(false, Enumerable.Repeat((1, 1), 257).ToArray()), "image/tiff"));
        var cycle = CreateTiff(false, (1, 1));
        BinaryPrimitives.WriteUInt32LittleEndian(cycle.AsSpan(118), 8);
        Assert.False(await ValidateAsync(cycle, "image/tiff"));
    }

    [Fact]
    public async Task ValidateAsync_Tiff_MismatchedStripArraysFailClosed()
    {
        var bytes = CreateTiff(false, (1, 1));
        // Only two declared values: safe on the old decoder, which incorrectly infers the missing count.
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(110), 2);
        Assert.False(await ValidateAsync(bytes, "image/tiff"));
    }

    [Theory]
    [InlineData(78, uint.MaxValue)]
    [InlineData(114, 1_073_741_824u)]
    [InlineData(114, uint.MaxValue)]
    [InlineData(74, uint.MaxValue)]
    [InlineData(110, uint.MaxValue)]
    [InlineData(38, uint.MaxValue)]
    public async Task ValidateAsync_Tiff_OutOfFileRangesFailBeforeAllocation(int offset, uint value)
    {
        var bytes = CreateTiff(false, (1, 1));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        Assert.False(await ValidateAsync(bytes, "image/tiff"));
    }

    [Fact]
    public async Task ValidateAsync_Tiff_OverflowingRawCountAndTruncatedDirectoryFailClosed()
    {
        var bytes = CreateTiff(false, (1, 1));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(108), 16); // LONG8 byte count, stored out of line.
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(114), (uint)bytes.Length);
        Assert.False(await ValidateAsync([.. bytes, .. Enumerable.Repeat((byte)255, 8)], "image/tiff"));
        bytes = CreateTiff(false, (1, 1));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), ushort.MaxValue);
        Assert.False(await ValidateAsync(bytes, "image/tiff"));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 257)]
    [InlineData(true, 257)]
    public async Task ValidateAsync_Tiff_ValidPaddedTilesAndChunkedRanges(bool bigEndian, int tileCount)
    {
        var bytes = CreateTiledTiff(bigEndian, tileCount);
        Assert.True(await ValidateAsync(bytes, "image/tiff"));
        Assert.False(await ValidateAsync(bytes[..^1], "image/tiff"));
    }

    [Theory]
    [InlineData(90, 1_073_741_824u)]
    [InlineData(102, 1_073_741_824u)]
    [InlineData(114, uint.MaxValue)]
    [InlineData(126, 1_073_741_824u)]
    [InlineData(122, uint.MaxValue)]
    public async Task ValidateAsync_Tiff_TileRangesAndPaddingAreBounded(int offset, uint value)
    {
        var bytes = CreateTiledTiff(false, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        Assert.False(await ValidateAsync(bytes, "image/tiff"));
    }

    [Fact]
    public async Task ValidateAsync_Tiff_UnsupportedCompressionAndBrokenDirectoryFailClosed()
    {
        var bytes = CreateTiff(false, (1, 1));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), ushort.MaxValue);
        Assert.False(await ValidateAsync(bytes, "image/tiff"));
        Assert.False(await ValidateAsync(bytes[..20], "image/tiff"));
        Assert.False(await ValidateAsync([0x49, 0x49, 42, 0, 0, 0, 0, 0], "image/tiff"));
    }

    [Fact]
    public async Task ValidateAsync_BigTiffAvifAndIco_RemainUnsupported()
    {
        Assert.False(await ValidateAsync([0x49, 0x49, 43, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], "image/tiff"));
        Assert.False(await ValidateAsync([0, 0, 0, 16, 102, 116, 121, 112, 97, 118, 105, 102, 0, 0, 0, 0], "image/avif"));
        Assert.False(await ValidateAsync([0, 0, 1, 0], "image/x-icon"));
    }

    [Fact]
    public async Task ValidateAsync_CancellationAndMissingFile_Propagate()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var validator = new ImageContentValidator();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validator.ValidateAsync(path, "image/png", cancellation.Token));
        await Assert.ThrowsAsync<FileNotFoundException>(() => validator.ValidateAsync(path, "image/tiff", CancellationToken.None));
    }

    private static async Task<bool> ValidateAsync(byte[] bytes, string mime)
    {
        var path = Path.Combine(Path.GetTempPath(), "bukit-image-validator-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            return await new ImageContentValidator().ValidateAsync(path, mime, CancellationToken.None);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static byte[] CreateBmp(int seed)
    {
        var bytes = new byte[58];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 24);
        bytes[54] = (byte)(seed >> 16);
        bytes[55] = (byte)(seed >> 8);
        bytes[56] = (byte)seed;
        return bytes;
    }

    internal static byte[] CreateGif(int frameCount, int seed = 0)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("GIF89a"u8);
        writer.Write((ushort)9);
        writer.Write((ushort)1);
        writer.Write(new byte[] { 0x80, 0, 0, (byte)seed, (byte)(seed >> 8), (byte)(seed >> 16), 255, 255, 255 });
        for (var frame = 0; frame < frameCount; frame++)
        {
            writer.Write(new byte[] { 0x21, 0xf9, 4, (byte)(frame % 2 == 0 ? 4 : 12), 1, 0, 0, 0, 0x2c });
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)9);
            writer.Write((ushort)1);
            writer.Write((byte)0);
            writer.Write((byte)2);
            // A clear code before each pixel keeps all LZW codes at three bits.
            ulong codes = 0;
            var bit = 0;
            for (var pixel = 0; pixel < 9; pixel++)
            {
                codes |= 4UL << bit;
                bit += 3;
                codes |= (ulong)((frame >> pixel) & 1) << bit;
                bit += 3;
            }
            codes |= 5UL << bit;
            writer.Write((byte)8);
            writer.Write(codes);
            writer.Write((byte)0);
        }
        writer.Write((byte)0x3b);
        return stream.ToArray();
    }

    private static byte[] CreateTiledTiff(bool bigEndian, int tileCount)
    {
        const int tableEnd = 134;
        var pixels = tableEnd + (tileCount == 1 ? 0 : tileCount * 8);
        var bytes = new byte[pixels + tileCount * 256];
        bytes[0] = bytes[1] = (byte)(bigEndian ? 'M' : 'I');
        Write16(2, 42);
        Write32(4, 8);
        Write16(8, 10);
        var entry = 10;
        Tag(256, 4, 1, 1);
        Tag(257, 4, 1, (uint)((tileCount - 1) * 16 + 1));
        Tag(258, 3, 1, 8);
        Tag(259, 3, 1, 1);
        Tag(262, 3, 1, 1);
        Tag(277, 3, 1, 1);
        Tag(322, 4, 1, 16);
        Tag(323, 4, 1, 16);
        Tag(324, 4, (uint)tileCount, (uint)(tileCount == 1 ? pixels : tableEnd));
        Tag(325, 4, (uint)tileCount, (uint)(tileCount == 1 ? 256 : tableEnd + tileCount * 4));
        if (tileCount > 1)
        {
            for (var tile = 0; tile < tileCount; tile++)
            {
                Write32(tableEnd + tile * 4, (uint)(pixels + tile * 256));
                Write32(tableEnd + (tileCount + tile) * 4, 256);
            }
        }
        bytes.AsSpan(pixels).Fill(80);
        return bytes;

        void Tag(ushort tag, ushort type, uint count, uint value)
        {
            Write16(entry, tag);
            Write16(entry + 2, type);
            Write32(entry + 4, count);
            if (type == 3) Write16(entry + 8, (ushort)value);
            else Write32(entry + 8, value);
            entry += 12;
        }
        void Write16(int offset, ushort value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        }
        void Write32(int offset, uint value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }

    private static byte[] CreateTiff(bool bigEndian, params (int Width, int Height)[] pages)
    {
        const int directorySize = 114;
        var pixelOffset = 8 + directorySize * pages.Length;
        var bytes = new byte[pixelOffset + pages.Sum(page => (int)Math.Min((long)page.Width * page.Height, 16))];
        bytes[0] = bytes[1] = (byte)(bigEndian ? 'M' : 'I');
        Write16(2, 42);
        Write32(4, 8);
        for (var page = 0; page < pages.Length; page++)
        {
            var (width, height) = pages[page];
            var directory = 8 + page * directorySize;
            Write16(directory, 9);
            var entry = directory + 2;
            Tag(256, 4, (uint)width);
            Tag(257, 4, (uint)height);
            Tag(258, 3, 8);
            Tag(259, 3, 1);
            Tag(262, 3, 1);
            Tag(273, 4, (uint)pixelOffset);
            Tag(277, 3, 1);
            Tag(278, 4, (uint)height);
            Tag(279, 4, (uint)Math.Min((long)width * height, uint.MaxValue));
            Write32(entry, page + 1 == pages.Length ? 0 : (uint)(directory + directorySize));
            var pixelCount = (int)Math.Min((long)width * height, 16);
            bytes.AsSpan(pixelOffset, pixelCount).Fill((byte)(page + 20));
            pixelOffset += pixelCount;

            void Tag(ushort tag, ushort type, uint value)
            {
                Write16(entry, tag);
                Write16(entry + 2, type);
                Write32(entry + 4, 1);
                if (type == 3) Write16(entry + 8, (ushort)value);
                else Write32(entry + 8, value);
                entry += 12;
            }
        }
        return bytes;

        void Write16(int offset, ushort value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        }
        void Write32(int offset, uint value)
        {
            if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
