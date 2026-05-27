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
        CloneScreenshotDiffTests.WritePngForTest(path, 2, 1, [255, 0, 0, 255, 0, 255, 0, 255]);

        var image = PngImage.Read(path);

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.NotNull(image.Pixels);
        Assert.Equal(8, image.Pixels.Length);
    }

    [Fact]
    public void Read_NonPngFile_Throws()
    {
        var path = Path.Combine(_tempDir, "not-a-png.png");
        File.WriteAllBytes(path, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

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
