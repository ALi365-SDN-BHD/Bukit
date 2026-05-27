using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class ImageHelperTests
{
    [Fact]
    public void BuildSrcset_ValidPath_ReturnsSrcset()
    {
        var result = ImageHelper.BuildSrcset("/img/photo.jpg");
        Assert.Contains("/img/photo.jpg?w=480 480w", result);
        Assert.Contains("/img/photo.jpg?w=768 768w", result);
        Assert.Contains("/img/photo.jpg?w=1200 1200w", result);
    }

    [Fact]
    public void BuildSrcset_EmptySource_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ImageHelper.BuildSrcset(""));
    }

    [Fact]
    public void BuildSrcset_WhitespaceSource_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ImageHelper.BuildSrcset("   "));
    }

    [Fact]
    public void BuildSrcset_JavascriptUrl_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ImageHelper.BuildSrcset("javascript:alert(1)"));
    }

    [Fact]
    public void BuildSrcset_ProtocolRelativeUrl_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ImageHelper.BuildSrcset("//example.com/img.jpg"));
    }

    [Fact]
    public void BuildSrcset_AbsoluteUrl_ReturnsSrcset()
    {
        var result = ImageHelper.BuildSrcset("https://example.com/img.jpg");
        Assert.Contains("https://example.com/img.jpg?w=480", result);
    }

    [Fact]
    public void BuildSrcset_CustomSizes_UsesProvidedSizes()
    {
        var result = ImageHelper.BuildSrcset("/img.jpg", "100,200");
        Assert.Contains("100w", result);
        Assert.Contains("200w", result);
        Assert.DoesNotContain("480w", result);
    }

    [Fact]
    public void BuildSrcset_InvalidSize_Skipped()
    {
        var result = ImageHelper.BuildSrcset("/img.jpg", "abc,200");
        Assert.DoesNotContain("abc", result);
        Assert.Contains("200w", result);
    }

    [Fact]
    public void BuildSrcset_ZeroOrNegativeSize_Skipped()
    {
        var result = ImageHelper.BuildSrcset("/img.jpg", "0,-1,200");
        Assert.DoesNotContain("?w=0 ", result);
        Assert.DoesNotContain("?w=-1 ", result);
        Assert.Contains("200w", result);
    }

    [Fact]
    public void BuildSrcset_HtmlEncodesSource()
    {
        var result = ImageHelper.BuildSrcset("/img/photo & more.jpg");
        Assert.Contains("&amp;", result);
    }

    [Fact]
    public void BuildImgTag_ValidSrc_ReturnsImgTag()
    {
        var result = ImageHelper.BuildImgTag("/img.jpg", "alt text", "480", "hero");
        Assert.Contains("<img src=\"/img.jpg\"", result);
        Assert.Contains("srcset=\"/img.jpg?w=480 480w\"", result);
        Assert.Contains("alt=\"alt text\"", result);
        Assert.Contains("class=\"hero\"", result);
        Assert.Contains("loading=\"lazy\"", result);
    }

    [Fact]
    public void BuildImgTag_EmptyAlt_OmitsAltAttribute()
    {
        var result = ImageHelper.BuildImgTag("/img.jpg");
        Assert.DoesNotContain("alt=", result);
    }

    [Fact]
    public void BuildImgTag_EmptyClassName_OmitsClassAttribute()
    {
        var result = ImageHelper.BuildImgTag("/img.jpg", "alt");
        Assert.DoesNotContain("class=", result);
    }

    [Fact]
    public void BuildImgTag_InvalidSource_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ImageHelper.BuildImgTag(""));
        Assert.Equal(string.Empty, ImageHelper.BuildImgTag("javascript:alert(1)"));
    }

    [Fact]
    public void BuildImgTag_EscapesAttributes()
    {
        var result = ImageHelper.BuildImgTag("/img.jpg", "A \"quote\"", "480", "cls \"x\"");
        Assert.Contains("alt=\"A &quot;quote&quot;\"", result);
        Assert.Contains("class=\"cls &quot;x&quot;\"", result);
    }
}
