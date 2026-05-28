using System.Reflection;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneContentWriterTests
{
    [Fact]
    public void GenerateIndexContent_WithBrandAndSummary_IncludesFrontMatter()
    {
        var page = new ClonePageInfo
        {
            Title = "Test Page",
            Url = "https://example.com/page",
            Summary = "A summary of the page"
        };
        var brand = "MyBrand";

        var method = typeof(CloneContentWriter).GetMethod("GenerateIndexContent",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, [page, brand])!;

        Assert.Contains("title: 'Test Page'", result);
        Assert.Contains("source_url: 'https://example.com/page'", result);
        Assert.Contains("summary: 'A summary of the page'", result);
        Assert.Contains("type: page", result);
        Assert.Contains("slug: index", result);
        Assert.Contains("template: pages/index.html", result);
    }

    [Fact]
    public void GenerateIndexContent_WithOgImage_IncludesOgImageField()
    {
        var page = new ClonePageInfo
        {
            Title = "OG Test",
            Seo = new ClonePageSeo { Image = "https://example.com/og.png" }
        };

        var method = typeof(CloneContentWriter).GetMethod("GenerateIndexContent",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, [page, null])!;

        Assert.Contains("og_image: 'https://example.com/og.png'", result);
    }

    [Fact]
    public void GenerateIndexContent_WithoutBrand_UsesPageTitle()
    {
        var page = new ClonePageInfo { Title = "Standalone Page" };

        var method = typeof(CloneContentWriter).GetMethod("GenerateIndexContent",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (string)method.Invoke(null, [page, null])!;

        Assert.Contains("title: 'Standalone Page'", result);
        Assert.Contains("# Standalone Page", result);
    }

    [Fact]
    public void BuildAssetMap_MapsSrcToLocalPath()
    {
        var assets = new List<CloneAsset>
        {
            new() { Type = "image", Src = "https://example.com/photo.jpg", LocalPath = "/assets/images/photo.jpg" },
            new() { Type = "font", Src = "https://example.com/font.woff2", LocalPath = "/assets/fonts/font.woff2" }
        };

        var map = CloneContentWriter.BuildAssetMap(assets);

        Assert.Equal(2, map.Count);
        Assert.Equal("/assets/images/photo.jpg", map["https://example.com/photo.jpg"]);
        Assert.Equal("/assets/fonts/font.woff2", map["https://example.com/font.woff2"]);
    }

    [Fact]
    public void BuildAssetMap_WithoutLocalPath_GeneratesPath()
    {
        var assets = new List<CloneAsset>
        {
            new() { Type = "image", Src = "https://example.com/banner.png" }
        };

        var map = CloneContentWriter.BuildAssetMap(assets);

        Assert.Single(map);
        Assert.StartsWith("/assets/images/", map["https://example.com/banner.png"]);
    }

    [Fact]
    public void BuildAssetMap_EmptySrc_Skipped()
    {
        var assets = new List<CloneAsset>
        {
            new() { Type = "image", Src = "" },
            new() { Type = "image", Src = "https://example.com/valid.png", LocalPath = "/local.png" }
        };

        var map = CloneContentWriter.BuildAssetMap(assets);

        Assert.Single(map);
    }

    [Fact]
    public void AssetFileName_ExtractsFromUrl()
    {
        var asset = new CloneAsset { Type = "image", Src = "https://example.com/path/to/photo.png" };

        var result = CloneContentWriter.AssetFileName(asset, 1);

        Assert.Equal("photo.png", result);
    }

    [Fact]
    public void AssetFileName_WithoutExtension_AddsImgExtension()
    {
        var asset = new CloneAsset { Type = "image", Src = "https://example.com/path/to/file" };

        var result = CloneContentWriter.AssetFileName(asset, 1);

        Assert.Equal("file.img", result);
    }

    [Fact]
    public void AssetFileName_InvalidUri_FallsBackToType()
    {
        var asset = new CloneAsset { Type = "video", Src = "not a url!!!" };

        var result = CloneContentWriter.AssetFileName(asset, 5);

        Assert.Contains("video-5", result);
    }

    [Fact]
    public void AssetSubdir_Video_ReturnsVideos()
    {
        Assert.Equal("videos", CloneContentWriter.AssetSubdir("video"));
        Assert.Equal("videos", CloneContentWriter.AssetSubdir("videos"));
        Assert.Equal("videos", CloneContentWriter.AssetSubdir("movie"));
        Assert.Equal("videos", CloneContentWriter.AssetSubdir("lottie"));
    }

    [Fact]
    public void AssetSubdir_Font_ReturnsFonts()
    {
        Assert.Equal("fonts", CloneContentWriter.AssetSubdir("font"));
        Assert.Equal("fonts", CloneContentWriter.AssetSubdir("fonts"));
        Assert.Equal("fonts", CloneContentWriter.AssetSubdir("typeface"));
    }

    [Fact]
    public void AssetSubdir_Default_ReturnsImages()
    {
        Assert.Equal("images", CloneContentWriter.AssetSubdir("image"));
        Assert.Equal("images", CloneContentWriter.AssetSubdir("unknown"));
        Assert.Equal("images", CloneContentWriter.AssetSubdir(null));
        Assert.Equal("images", CloneContentWriter.AssetSubdir(""));
    }

    [Fact]
    public void LocalAssetPath_ProducesCorrectPath()
    {
        var asset = new CloneAsset { Type = "image", Src = "https://example.com/logo.png" };

        var result = CloneContentWriter.LocalAssetPath(asset, 1);

        Assert.StartsWith("/assets/images/", result);
        Assert.EndsWith("logo.png", result);
    }

    [Fact]
    public void LocalAssetPath_VideoType_UsesVideoSubdir()
    {
        var asset = new CloneAsset { Type = "video", Src = "https://example.com/demo.mp4" };

        var result = CloneContentWriter.LocalAssetPath(asset, 1);

        Assert.StartsWith("/assets/videos/", result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesSpecialChars()
    {
        var method = typeof(CloneContentWriter).GetMethod("SanitizeFileName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string)method.Invoke(null, ["file name with spaces.png"])!;
        Assert.Equal("file_name_with_spaces.png", result);
    }

    [Fact]
    public void SanitizeFileName_PreservesAllowedChars()
    {
        var method = typeof(CloneContentWriter).GetMethod("SanitizeFileName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string)method.Invoke(null, ["valid-name_123.txt"])!;
        Assert.Equal("valid-name_123.txt", result);
    }

    [Fact]
    public void SanitizeSlug_NormalizesText()
    {
        var method = typeof(CloneContentWriter).GetMethod("SanitizeSlug",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string)method.Invoke(null, ["Hello World!"])!;
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void SanitizeSlug_HandlesSpecialChars()
    {
        var method = typeof(CloneContentWriter).GetMethod("SanitizeSlug",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string)method.Invoke(null, ["Section: Hero / Banner"])!;
        Assert.Equal("section-hero-banner", result);
    }

    [Fact]
    public void SanitizeSlug_EmptyInput_ReturnsCloneSection()
    {
        var method = typeof(CloneContentWriter).GetMethod("SanitizeSlug",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (string)method.Invoke(null, ["!!!###"])!;
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void NormalizeType_FaqType_ReturnsFaq()
    {
        var method = typeof(CloneContentWriter).GetMethod("NormalizeType",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = method.Invoke(null, ["FAQ"]);
        Assert.Equal("faq", result);
    }

    [Fact]
    public void PartialFor_AllTypes_ReturnCorrectPartials()
    {
        var method = typeof(CloneContentWriter).GetMethod("PartialFor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal("clone-navigation", method.Invoke(null, ["navigation"]));
        Assert.Equal("clone-hero", method.Invoke(null, ["hero"]));
        Assert.Equal("clone-feature-grid", method.Invoke(null, ["features"]));
        Assert.Equal("clone-pricing", method.Invoke(null, ["pricing"]));
        Assert.Equal("clone-faq", method.Invoke(null, ["faq"]));
        Assert.Equal("clone-cta", method.Invoke(null, ["cta"]));
        Assert.Equal("clone-footer", method.Invoke(null, ["footer"]));
        Assert.Equal("clone-section", method.Invoke(null, ["unknown"]));
    }
}
