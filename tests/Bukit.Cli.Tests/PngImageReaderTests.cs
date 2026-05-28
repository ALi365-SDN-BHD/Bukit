using System.IO.Compression;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class PngImageReaderTests : IDisposable
{
    private readonly string _tempDir;

    public PngImageReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-png-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Read_ValidPng_ReturnsCorrectDimensions()
    {
        var path = Path.Combine(_tempDir, "test.png");
        WriteMinimalPng(path, 2, 1);
        var image = PngImage.Read(path);
        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.NotNull(image.Pixels);
    }

    private static void WriteMinimalPng(string path, int width, int height)
    {
        using var fs = File.Create(path);
        fs.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8; ihdr[9] = 6;
        WriteChunk(fs, "IHDR", ihdr);
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++)
                raw.Write([255, 0, 0, 255]);
        }
        var rawBytes = raw.ToArray();
        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
            zlib.Write(rawBytes);
        WriteChunk(fs, "IDAT", compressed.ToArray());
        WriteChunk(fs, "IEND", []);
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len); s.Write(System.Text.Encoding.ASCII.GetBytes(type)); s.Write(data);
        s.Write([0, 0, 0, 0]);
    }

    [Fact]
    public void Read_NonPngFile_Throws()
    {
        var path = Path.Combine(_tempDir, "not-a-png.png");
        File.WriteAllBytes(path, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

        var ex = Assert.Throws<InvalidOperationException>(() => PngImage.Read(path));
        Assert.Contains("not a PNG file", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingFile_Throws()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.png");

        Assert.Throws<FileNotFoundException>(() => PngImage.Read(path));
    }
}
