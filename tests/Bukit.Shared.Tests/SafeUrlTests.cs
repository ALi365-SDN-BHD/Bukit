using Xunit;

namespace Bukit.Shared.Tests;

public sealed class SafeUrlTests
{
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
