using Xunit;
using Bukit.Config;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for ImageMetadataReader image format detection and SEO audit image analysis.
/// </summary>
public sealed class ImageMetadataReaderTests : IDisposable
{
    private readonly string _testDir;

    public ImageMetadataReaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-img-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_testDir, recursive: true);
    }

    private string WriteBytes(byte[] bytes, string name)
    {
        var path = Path.Combine(_testDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // ── PNG ──────────────────────────────────────────────────────────

    [Fact]
    public void TryReadImageMetadata_Png_ReadsDimensions()
    {
        // Valid PNG signature + IHDR: width=100, height=50
        var bytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR length
            0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x64, // width = 100
            0x00, 0x00, 0x00, 0x32, // height = 50
            0x08, 0x06, 0x00, 0x00, 0x00 // rest
        };
        var path = WriteBytes(bytes, "test.png");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/png", metadata!.MimeType);
        Assert.Equal(100, metadata.Width);
        Assert.Equal(50, metadata.Height);
    }

    // ── GIF ──────────────────────────────────────────────────────────

    [Fact]
    public void TryReadImageMetadata_Gif_ReadsDimensions()
    {
        var bytes = new byte[]
        {
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, // GIF89a
            0x64, 0x00, // width = 100 (little endian)
            0x32, 0x00, // height = 50
            0x80, 0x00, 0x00
        };
        var path = WriteBytes(bytes, "test.gif");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/gif", metadata!.MimeType);
        Assert.Equal(100, metadata.Width);
        Assert.Equal(50, metadata.Height);
    }

    // ── WEBP ─────────────────────────────────────────────────────────

    [Fact]
    public void TryReadImageMetadata_WebpVp8x_ReadsDimensions()
    {
        var bytes = new byte[30];
        // RIFF header
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x16; bytes[5] = 0x00; bytes[6] = 0x00; bytes[7] = 0x00; // file size - 8
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x58; // VP8X
        bytes[16] = 10; // chunk size
        // VP8X: width-1 = 99 (0x63), height-1 = 49 (0x31)
        bytes[24] = 99;        // width low byte
        bytes[25] = 0;         // width mid
        bytes[26] = 0;         // width high
        bytes[27] = 49;        // height low
        bytes[28] = 0;         // height mid
        bytes[29] = 0;         // height high
        var path = WriteBytes(bytes, "test.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/webp", metadata!.MimeType);
        Assert.Equal(100, metadata.Width);
        Assert.Equal(50, metadata.Height);
    }

    [Fact]
    public void TryReadImageMetadata_WebpVp8_ReadsDimensions()
    {
        var bytes = new byte[30];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x16; // file size - 8
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x20; // "VP8 "
        bytes[16] = 10; // chunk size
        bytes[23] = 0x9D; bytes[24] = 0x01; bytes[25] = 0x2A; // VP8 key-frame signature
        // VP8 frame: width 14-bit little endian = 100 (0x64), height = 50 (0x32)
        bytes[26] = 100; bytes[27] = 0;
        bytes[28] = 50; bytes[29] = 0;
        var path = WriteBytes(bytes, "test-vp8.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/webp", metadata!.MimeType);
        Assert.Equal(100, metadata.Width);
        Assert.Equal(50, metadata.Height);
    }

    [Fact]
    public void TryReadImageMetadata_WebpVp8l_ReadsDimensions()
    {
        // RIFF/WEBP header requires >= 30 bytes to reach the format check
        var bytes = new byte[30];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x16; // file size - 8
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x4C; // VP8L
        bytes[16] = 10; // chunk size
        bytes[20] = 0x2F; // VP8L signature
        // VP8L lossless: 14-bit (width-1) and (height-1) packed into bytes 21-24
        var widthMinusOne = 99;  // 100-1
        var heightMinusOne = 49; // 50-1
        bytes[21] = (byte)(widthMinusOne & 0xFF);
        bytes[22] = (byte)(((widthMinusOne >> 8) & 0x3F) | ((heightMinusOne & 0x0F) << 6));
        bytes[23] = (byte)((heightMinusOne >> 2) & 0xFF);
        bytes[24] = (byte)((heightMinusOne >> 10) & 0x0F);
        var path = WriteBytes(bytes, "test-vp8l.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/webp", metadata!.MimeType);
        Assert.Equal(100, metadata.Width);
        Assert.Equal(50, metadata.Height);
    }

    // ── WEBP safety boundaries ─────────────────────────────────────

    [Fact]
    public void TryReadImageMetadata_LargeWebp_UsesBoundedMemory()
    {
        const int fileSize = 8 * 1024 * 1024;
        var path = Path.Combine(_testDir, "large.webp");
        var header = new byte[30];
        header[0] = 0x52; header[1] = 0x49; header[2] = 0x46; header[3] = 0x46; // RIFF
        header[4] = 0xF8; header[5] = 0xFF; header[6] = 0x7F; header[7] = 0x00; // file size - 8
        header[8] = 0x57; header[9] = 0x45; header[10] = 0x42; header[11] = 0x50; // WEBP
        header[12] = 0x56; header[13] = 0x50; header[14] = 0x38; header[15] = 0x58; // VP8X
        header[16] = 10; // VP8X chunk size
        header[24] = 99; // width - 1
        header[27] = 49; // height - 1

        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(fileSize);
            stream.Write(header);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.NotNull(metadata);
        Assert.Equal(100, metadata!.Width);
        Assert.Equal(50, metadata.Height);
        Assert.True(allocatedBytes < 1024 * 1024, $"WebP metadata parsing allocated {allocatedBytes} bytes.");
    }

    [Fact]
    public void TryReadImageMetadata_WebpTruncatedChunk_ReturnsNull()
    {
        var bytes = new byte[30];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x16; // file size - 8
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x58; // VP8X
        bytes[16] = 100; // declared chunk extends beyond the file
        bytes[24] = 99;
        bytes[27] = 49;
        var path = WriteBytes(bytes, "truncated.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    [Fact]
    public void TryReadImageMetadata_WebpTruncatedRiffContainer_ReturnsNull()
    {
        var bytes = new byte[30];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x00; bytes[5] = 0x10; // declares a 4096-byte container payload
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x58; // VP8X
        bytes[16] = 10; // chunk size itself fits
        bytes[24] = 99;
        bytes[27] = 49;
        var path = WriteBytes(bytes, "truncated-riff.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    [Fact]
    public void TryReadImageMetadata_WebpChunkOutsideDeclaredRiffContainer_ReturnsNull()
    {
        var bytes = new byte[128];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x16; // declares a 30-byte container
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x58; // VP8X
        bytes[16] = 100; // fits the physical file but not the declared RIFF container
        bytes[24] = 99;
        bytes[27] = 49;
        var path = WriteBytes(bytes, "chunk-outside-riff.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    [Fact]
    public void TryReadImageMetadata_WebpVp8lWithoutSignature_ReturnsNull()
    {
        var bytes = new byte[30];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x16; // file size - 8
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x4C; // VP8L
        bytes[16] = 10; // chunk size
        bytes[20] = 0x00; // invalid: VP8L requires 0x2F
        bytes[21] = 99;
        bytes[22] = 0x40;
        bytes[23] = 12;
        var path = WriteBytes(bytes, "invalid-vp8l.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    [Fact]
    public void TryReadImageMetadata_WebpVp8WithoutFrameSignature_ReturnsNull()
    {
        var bytes = new byte[30];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x16; // file size - 8
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x20; // "VP8 "
        bytes[16] = 10; // chunk size
        bytes[23] = 0x00; bytes[24] = 0x00; bytes[25] = 0x00; // invalid frame signature
        bytes[26] = 100;
        bytes[28] = 50;
        var path = WriteBytes(bytes, "invalid-vp8.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    [Fact]
    public void TryReadImageMetadata_WebpVp8xWithWrongChunkSize_ReturnsNull()
    {
        var bytes = new byte[32];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46; // RIFF
        bytes[4] = 0x18; // file size - 8
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50; // WEBP
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x58; // VP8X
        bytes[16] = 12; // invalid: VP8X payload must be exactly 10 bytes
        bytes[24] = 99;
        bytes[27] = 49;
        var path = WriteBytes(bytes, "invalid-vp8x-size.webp");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    // JPEG

    [Fact]
    public void TryReadImageMetadata_Jpeg_ReadsDimensions()
    {
        // SOI (FF D8) + APP0 (FF E0) + SOF0 (FF C0) with width=100 height=50
        var bytes = new List<byte>
        {
            0xFF, 0xD8, // SOI
            0xFF, 0xE0, // APP0 marker
            0x00, 0x10, // length 16
            0x4A, 0x46, 0x49, 0x46, 0x00, // "JFIF\0"
            0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, // JFIF data
            0xFF, 0xC0, // SOF0
            0x00, 0x11, // length 17
            0x08, // precision
            0x00, 0x32, // height = 50
            0x00, 0x64, // width = 100
            0x03, // components
            0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, // component data
            0xFF, 0xD9 // EOI
        };
        var path = WriteBytes(bytes.ToArray(), "test.jpg");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/jpeg", metadata!.MimeType);
        Assert.Equal(100, metadata.Width);
        Assert.Equal(50, metadata.Height);
    }

    [Fact]
    public void TryReadImageMetadata_Jpeg_MissingSof_ReturnsNull()
    {
        var bytes = new byte[]
        {
            0xFF, 0xD8, // SOI
            0xFF, 0xD9  // EOI without SOF
        };
        var path = WriteBytes(bytes, "no-sof.jpg");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    // ── SVG ──────────────────────────────────────────────────────────

    [Fact]
    public void TryReadImageMetadata_Svg_ReadsDimensionsFromAttributes()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="50"></svg>""";
        var path = WriteBytes(System.Text.Encoding.UTF8.GetBytes(svg), "test.svg");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/svg+xml", metadata!.MimeType);
        Assert.Equal(100, metadata.Width);
        Assert.Equal(50, metadata.Height);
    }

    [Fact]
    public void TryReadImageMetadata_Svg_ReadsDimensionsFromViewBox()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100"></svg>""";
        var path = WriteBytes(System.Text.Encoding.UTF8.GetBytes(svg), "viewbox.svg");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/svg+xml", metadata!.MimeType);
        Assert.Equal(200, metadata.Width);
        Assert.Equal(100, metadata.Height);
    }

    [Fact]
    public void TryReadImageMetadata_Svg_PercentageDimensions_FallbackToViewBox()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 300 150"></svg>""";
        var path = WriteBytes(System.Text.Encoding.UTF8.GetBytes(svg), "percent.svg");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.NotNull(metadata);
        Assert.Equal("image/svg+xml", metadata!.MimeType);
        Assert.Equal(300, metadata.Width);
        Assert.Equal(150, metadata.Height);
    }

    [Fact]
    public void TryReadImageMetadata_Svg_Malformed_ReturnsNull()
    {
        var path = WriteBytes(System.Text.Encoding.UTF8.GetBytes("<svg<broken>"), "broken.svg");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    [Fact]
    public void TryReadImageMetadata_Svg_NonSvgElement_ReturnsNull()
    {
        var path = WriteBytes(System.Text.Encoding.UTF8.GetBytes("<html><body></body></html>"), "notsvg.svg");

        var metadata = ImageMetadataReader.TryReadImageMetadata(path);

        Assert.Null(metadata);
    }

    // ── Unknown formats ──────────────────────────────────────────────

    [Fact]
    public void TryReadImageMetadata_UnknownFormat_ReturnsNull()
    {
        var path = WriteBytes([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], "unknown.bin");
        var metadata = ImageMetadataReader.TryReadImageMetadata(path);
        Assert.Null(metadata);
    }

    [Fact]
    public void TryReadImageMetadata_TooShort_ReturnsNull()
    {
        var path = WriteBytes([0x89, 0x50], "short.png");
        var metadata = ImageMetadataReader.TryReadImageMetadata(path);
        Assert.Null(metadata);
    }

    // ── Raw byte readers ─────────────────────────────────────────────

    [Fact]
    public void ReadBigEndianInt32_CorrectValue()
    {
        var result = ImageMetadataReader.ReadBigEndianInt32([0x00, 0x00, 0x00, 0x64]);
        Assert.Equal(100, result);
    }

    [Fact]
    public void ReadLittleEndianUInt16_CorrectValue()
    {
        var result = ImageMetadataReader.ReadLittleEndianUInt16([0x64, 0x00]);
        Assert.Equal(100, result);
    }

    // ── AnalyzeImage / AnalyzeLocalImage ─────────────────────────────

    [Fact]
    public void AnalyzeImage_EmptyImage_NoIssues()
    {
        var issues = new List<SeoAuditIssue>();
        ImageMetadataReader.AnalyzeImage(CreateConfig(), "seo", "/test/", null, _testDir, issues);
        Assert.Empty(issues);
    }

    [Fact]
    public void AnalyzeImage_RelativeUrl_WarnsNotAbsolute()
    {
        var issues = new List<SeoAuditIssue>();
        ImageMetadataReader.AnalyzeImage(CreateConfig(), "seo", "/test/", "/img/photo.jpg", _testDir, issues);
        Assert.Contains(issues, i => i.Code == "seo_not_absolute");
    }

    [Fact]
    public void AnalyzeImage_MissingLocalFile_WarnsMissingFile()
    {
        var issues = new List<SeoAuditIssue>();
        ImageMetadataReader.AnalyzeImage(CreateConfig(), "seo", "/test/", "/img/missing.jpg", _testDir, issues);
        Assert.Contains(issues, i => i.Code == "seo_missing_file");
    }

    [Fact]
    public void AnalyzeLocalImage_ExistingImage_NoIssues()
    {
        var imagePath = WriteBytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 0x00, 0x00, 0x01, 0x2C, 0x00, 0x00, 0x00, 0x9D], "ok.png");
        var issues = new List<SeoAuditIssue>();
        ImageMetadataReader.AnalyzeLocalImage("seo", "/test/", "/img/ok.png", imagePath, issues);
        Assert.DoesNotContain(issues, i => i.Code.Contains("missing") || i.Code.Contains("mime_unknown"));
    }

    [Fact]
    public void AnalyzeLocalImage_TinyImage_WarnsTooSmall()
    {
        var imagePath = WriteBytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 0x00, 0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x32], "tiny.png");
        var issues = new List<SeoAuditIssue>();
        ImageMetadataReader.AnalyzeLocalImage("seo", "/test/", "/img/tiny.png", imagePath, issues);
        Assert.Contains(issues, i => i.Code == "seo_too_small");
    }

    [Fact]
    public void TryMapSiteUrlToOutputPath_MatchingHost_MapsPath()
    {
        var result = ImageMetadataReader.TryMapSiteUrlToOutputPath(
            "https://example.com", "/", "https://example.com/img/photo.jpg", "/tmp/output", out var fullPath);
        Assert.True(result);
        Assert.Equal(Path.Combine("/tmp/output", "img", "photo.jpg"), fullPath);
    }

    [Fact]
    public void TryMapSiteUrlToOutputPath_DifferentHost_ReturnsFalse()
    {
        var result = ImageMetadataReader.TryMapSiteUrlToOutputPath(
            "https://example.com", "/", "https://other.com/img/photo.jpg", "/tmp/output", out _);
        Assert.False(result);
    }

    [Fact]
    public void TryMapSiteUrlToOutputPath_EmptySiteUrl_ReturnsFalse()
    {
        var result = ImageMetadataReader.TryMapSiteUrlToOutputPath(
            null, "/", "https://example.com/img/photo.jpg", "/tmp/output", out _);
        Assert.False(result);
    }

    private static AppConfig CreateConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.com", BaseUrl = "/" },
            Content = ContentConfigFactory.FromSources([])
        };
    }
}
