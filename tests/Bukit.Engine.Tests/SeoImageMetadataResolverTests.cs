using Bukit.Engine.Audit;
using Bukit.Rendering;
using Bukit.Shared;
using SkiaSharp;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoImageMetadataResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bukit-og-image-{Guid.NewGuid():N}");

    public SeoImageMetadataResolverTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("https://example.com/repo/en/assets/uploads/cover.png", "media", "cover.png", "image/png")]
    [InlineData("https://example.com/repo/assets/og.jpg", "theme-assets", "og.jpg", "image/jpeg")]
    [InlineData("/repo/en/share/card.webp", "static", "share/card.webp", "image/webp")]
    public void Apply_SiteHostedImage_AddsWidthHeightAndType(string imageUrl, string dir, string file, string type)
    {
        var format = Path.GetExtension(file) switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg
        };
        WriteImage(Path.Combine(_root, dir, file), 1200, 630, format);

        var model = CreateResolver().Apply(Model(imageUrl));

        Assert.Equal(1200, model.Og.ImageWidth);
        Assert.Equal(630, model.Og.ImageHeight);
        Assert.Equal(type, model.Og.ImageType);
    }

    [Theory]
    [InlineData("https://cdn.example.net/repo/assets/og.jpg")]
    [InlineData("https://example.com/repo/assets/missing.jpg")]
    [InlineData("https://example.com/repo/assets/../../outside.jpg")]
    [InlineData("data:image/png;base64,AAAA")]
    public void Apply_ExternalMissingOrEscapingImage_LeavesTheModelUnchanged(string imageUrl)
    {
        WriteImage(Path.Combine(_root, "outside.jpg"), 1200, 630, SKEncodedImageFormat.Jpeg);
        var model = Model(imageUrl);

        var applied = CreateResolver().Apply(model);

        Assert.Null(applied.Og.ImageWidth);
        Assert.Null(applied.Og.ImageType);
    }

    [Fact]
    public void InjectIntoHead_WritesImageMetadataAfterOgImage()
    {
        var seo = new SeoModel
        {
            Title = "T", Canonical = "https://example.com/post/",
            Og = new SeoOpenGraphModel { Image = "https://example.com/og.png", ImageWidth = 1200, ImageHeight = 630, ImageType = "image/png" }
        };

        var html = SeoHtmlRenderer.InjectIntoHead("<html><head></head><body></body></html>", seo, out _, out _);

        var image = html.IndexOf("property=\"og:image\"", StringComparison.Ordinal);
        Assert.True(image >= 0);
        Assert.True(html.IndexOf("property=\"og:image:width\" content=\"1200\"", StringComparison.Ordinal) > image);
        Assert.Contains("property=\"og:image:height\" content=\"630\"", html, StringComparison.Ordinal);
        Assert.Contains("property=\"og:image:type\" content=\"image/png\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzeLocalImage_WebpShareImage_Warns()
    {
        var path = Path.Combine(_root, "share.webp");
        WriteImage(path, 1200, 630, SKEncodedImageFormat.Webp);
        var issues = new List<SeoAuditIssue>();

        ImageMetadataReader.AnalyzeLocalImage("seo.og_image", "/post/", "/share.webp", path, issues);

        Assert.Contains(issues, issue => issue.Code == "seo.og_image_webp");
        Assert.DoesNotContain(issues, issue => issue.Code == "seo.og_image_too_small");
    }

    private SeoImageMetadataResolver CreateResolver()
        => new(
            "https://example.com",
            ["/repo/en", "/repo", null],
            "/assets/uploads",
            Path.Combine(_root, "media"),
            [Path.Combine(_root, "theme-assets"), null],
            [Path.Combine(_root, "static")],
            new ConsoleLogger(LogLevel.Error));

    private static SeoModel Model(string image)
        => new() { Title = "T", Canonical = "https://example.com/post/", Og = new SeoOpenGraphModel { Image = image } };

    private static void WriteImage(string path, int width, int height, SKEncodedImageFormat format)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SteelBlue);
        using var encoded = bitmap.Encode(format, 80);
        File.WriteAllBytes(path, encoded.ToArray());
    }
}
