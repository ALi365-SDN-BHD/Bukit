using Xunit;

namespace Bukit.Shared.Tests;

public sealed class SafeUrlTests
{
    [Theory]
    [InlineData(null, false, false, false)]
    [InlineData(" \t\r\n", false, false, false)]
    [InlineData(" https://example.com/a?b=1#part ", true, true, true)]
    [InlineData(" HTTPS://example.com/a ", true, true, true)]
    [InlineData(" HTTP://example.com/a ", true, true, false)]
    [InlineData(" MAILTO:user@example.com ", true, false, false)]
    [InlineData(" tel:+12345 ", true, false, false)]
    [InlineData(" /assets/中文.png ", true, true, true)]
    [InlineData("\t/assets/image.png\r\n", true, true, true)]
    [InlineData("/path%5Cpart?q=%09", true, true, true)]
    [InlineData("https://example.com/path%5Cpart?q=%0A", true, true, true)]
    [InlineData(" //example.com/a ", false, false, false)]
    [InlineData("relative/page.html", false, false, false)]
    [InlineData("#section", false, false, false)]
    [InlineData("https://", false, false, false)]
    [InlineData(" JaVaScRiPt:alert(1) ", false, false, false)]
    [InlineData("ftp://example.com/file", false, false, false)]
    public void UrlPolicies_TrimAndApplyDistinctSchemeAllowlists(string? url, bool link, bool media, bool embed)
    {
        Assert.Equal(link ? url!.Trim() : null, SafeUrl.ForLink(url));
        Assert.Equal(media ? url!.Trim() : null, SafeUrl.ForMedia(url));
        Assert.Equal(embed ? url!.Trim() : null, SafeUrl.ForEmbed(url));
    }

    [Theory]
    [InlineData("link")]
    [InlineData("media")]
    [InlineData("embed")]
    public void UrlPolicies_BackslashAuthorityConfusion_IsRejected(string policy)
    {
        const string url = @"/\attacker.example/payload";
        var result = policy switch
        {
            "link" => SafeUrl.ForLink(url),
            "media" => SafeUrl.ForMedia(url),
            _ => SafeUrl.ForEmbed(url)
        };

        Assert.Null(result);
    }

    [Theory]
    [InlineData("/\t/attacker.example/payload")]
    [InlineData("/\r/attacker.example/payload")]
    [InlineData("/\n/attacker.example/payload")]
    [InlineData("/path\0part")]
    [InlineData("/path\u001Fpart")]
    [InlineData("/path\u007Fpart")]
    [InlineData("/path\u0085part")]
    [InlineData(@"/path\part")]
    [InlineData(@"https://example.com\@attacker.example/payload")]
    [InlineData("https://example.com/\tpath")]
    [InlineData("https://example.com/\rpath")]
    [InlineData("https://example.com/\npath")]
    public void UrlPolicies_EmbeddedControlsOrBackslashes_AreRejected(string url)
    {
        Assert.Null(SafeUrl.ForLink(url));
        Assert.Null(SafeUrl.ForMedia(url));
        Assert.Null(SafeUrl.ForEmbed(url));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(" \t", false)]
    [InlineData(" /page ", false)]
    [InlineData(" //example.com/page ", true)]
    [InlineData("mailto:user@example.com", true)]
    public void IsExternal_EmptyAndTrimmedInput_ReturnExpected(string? url, bool expected)
    {
        Assert.Equal(expected, SafeUrl.IsExternal(url));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("mailto:user@example.com")]
    [InlineData("tel:+1234567890")]
    [InlineData("/assets/a.png")]
    [InlineData("/internal/path")]
    public void ForLink_ValidUrls_ReturnUrl(string url)
    {
        var result = SafeUrl.ForLink(url);

        Assert.Equal(url.Trim(), result);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("")]
    [InlineData("   ")]
    public void ForLink_DangerousOrEmptyUrls_ReturnNull(string url)
    {
        var result = SafeUrl.ForLink(url);

        Assert.Null(result);
    }

    [Fact]
    public void ForLink_NullInput_ReturnNull()
    {
        var result = SafeUrl.ForLink(null);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("https://example.com/img.png")]
    [InlineData("http://img.com/1.png")]
    [InlineData("/assets/a.png")]
    public void ForMedia_ValidUrls_ReturnUrl(string url)
    {
        var result = SafeUrl.ForMedia(url);

        Assert.Equal(url.Trim(), result);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    public void ForMedia_DangerousUrls_ReturnNull(string url)
    {
        var result = SafeUrl.ForMedia(url);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("https://youtube.com/embed/test")]
    [InlineData("https://example.com/widget")]
    [InlineData("/local/videos/intro.mp4")]
    [InlineData("/assets/embed.html")]
    public void ForEmbed_ValidUrls_ReturnUrl(string url)
    {
        var result = SafeUrl.ForEmbed(url);

        Assert.Equal(url.Trim(), result);
    }

    [Theory]
    [InlineData("http://youtube.com/embed/test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("file:///etc/passwd")]
    public void ForEmbed_NonHttpsOrDangerous_ReturnNull(string url)
    {
        var result = SafeUrl.ForEmbed(url);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("https://evil.com", true)]
    [InlineData("/internal/path", false)]
    [InlineData("/", false)]
    public void IsExternal_VariousUrls_ReturnExpected(string url, bool expected)
    {
        var result = SafeUrl.IsExternal(url);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/x.js")]
    public void ForLink_ProtocolRelativeUrl_ReturnsNull(string url)
    {
        Assert.Null(SafeUrl.ForLink(url));
    }

    [Theory]
    [InlineData("//evil.com/a.png")]
    [InlineData("//cdn.evil.com/audio.mp3")]
    public void ForMedia_ProtocolRelativeUrl_ReturnsNull(string url)
    {
        Assert.Null(SafeUrl.ForMedia(url));
    }

    [Theory]
    [InlineData("//evil.com/embed")]
    [InlineData("//attacker.com/widget")]
    public void ForEmbed_ProtocolRelativeUrl_ReturnsNull(string url)
    {
        Assert.Null(SafeUrl.ForEmbed(url));
    }

    [Fact]
    public void IsExternal_ProtocolRelativeUrl_ReturnsTrue()
    {
        Assert.True(SafeUrl.IsExternal("//evil.com"));
    }
}
