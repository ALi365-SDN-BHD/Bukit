using Bukit.Engine.Feeds;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SitemapUriTests
{
    [Theory]
    [InlineData("https://example.com/新闻/", "https://example.com/%E6%96%B0%E9%97%BB/")]
    [InlineData("https://example.com/a b/?q=中&x=1#片", "https://example.com/a%20b/?q=%E4%B8%AD&x=1#%E7%89%87")]
    [InlineData("https://example.com/100%/", "https://example.com/100%25/")]
    [InlineData("https://example.com/%E6%96%B0/", "https://example.com/%E6%96%B0/")]
    [InlineData("https://example.com/a/(b)/~c;d=e/", "https://example.com/a/(b)/~c;d=e/")]
    [InlineData("https://example.com/{x}|y^`\\\"<>", "https://example.com/%7Bx%7D%7Cy%5E%60%5C%22%3C%3E")]
    [InlineData("https://example.com/😀", "https://example.com/%F0%9F%98%80")]
    [InlineData("https://bücher.example:8080/b", "https://bücher.example:8080/b")]
    [InlineData("https://bücher.example", "https://bücher.example")]
    [InlineData("/相对/", "/%E7%9B%B8%E5%AF%B9/")]
    public void Encode_PercentEncodesCharactersOutsideTheUriSet(string url, string expected)
    {
        Assert.Equal(expected, SitemapUri.Encode(url));
        Assert.Equal(expected, SitemapUri.Encode(expected));
    }

    [Fact]
    public void Encode_AsciiUrl_ReturnsSameInstance()
    {
        const string url = "https://example.com/posts/a-b/?page=2";
        Assert.Same(url, SitemapUri.Encode(url));
    }
}
