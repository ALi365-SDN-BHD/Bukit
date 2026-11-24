using Bukit.Content.Markdown;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class UntrustedLinkMarkdownTests
{
    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("<javascript:alert(1)>")]
    [InlineData("[x](JAVASCRIPT:alert(1))")]
    [InlineData("[x](jav&#x61;script:alert(1))")]
    [InlineData("[x](&#x6A;avascript:alert(1))")]
    [InlineData("[x](vbscript:msgbox(1))")]
    [InlineData("[x](data:text/html,<script>alert(1)</script>)")]
    [InlineData("![x](data:image/png;base64,AAAA)")]
    [InlineData("[x](//evil.example/path)")]
    [InlineData("[r]: javascript:alert(1)\n\n[r]")]
    public void Convert_UntrustedLinks_DropsDangerousSchemes(string markdown)
    {
        var html = ConvertUntrusted(markdown);

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"//", html, StringComparison.Ordinal);
        Assert.DoesNotContain("src=\"//", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_UntrustedLinks_EncodedTabIsNotExecutable()
    {
        var html = ConvertUntrusted("[x](java&#9;script:alert(1))");

        Assert.Equal("<p><a href=\"java%09script:alert(1)\">x</a></p>", html);
    }

    [Theory]
    [InlineData("[x](#section)", "href=\"#section\"")]
    [InlineData("[x](other-post/)", "href=\"other-post/\"")]
    [InlineData("[x](../img.png)", "href=\"../img.png\"")]
    [InlineData("[x](/abs)", "href=\"/abs\"")]
    [InlineData("[x](https://example.com/a?b=c#d)", "href=\"https://example.com/a?b=c#d\"")]
    [InlineData("[x](http://example.com)", "href=\"http://example.com\"")]
    [InlineData("[x](mailto:a@example.com)", "href=\"mailto:a@example.com\"")]
    [InlineData("[x](tel:+15551234)", "href=\"tel:+15551234\"")]
    public void Convert_UntrustedLinks_KeepsSafeTargets(string markdown, string expected)
    {
        Assert.Contains(expected, ConvertUntrusted(markdown), StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_UntrustedLinks_KeepsImageTitle()
    {
        var html = ConvertUntrusted("""![alt](https://example.com/a.png "Title")""");

        Assert.Contains("src=\"https://example.com/a.png\"", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Title\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"alt\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_UntrustedLinks_KeepsReferenceStyleSafeLink()
    {
        var html = ConvertUntrusted("[r]: https://example.com/a\n\n[r]");

        Assert.Contains("href=\"https://example.com/a\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_UntrustedLinks_KeepsAutoLinkedWwwHost()
    {
        var html = ConvertUntrusted("www.example.com");

        Assert.Contains("www.example.com", html, StringComparison.Ordinal);
        Assert.Contains("<a ", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_OptionOff_MatchesCurrentRendering()
    {
        const string sample = """
            # Title

            A [link](https://example.com) and ![img](https://example.com/a.png "t").

            - item
            """;

        Assert.Equal(BasicMarkdownToHtml.Convert(sample), BasicMarkdownToHtml.Convert(sample, untrustedLinks: false));
        Assert.Contains("href=\"https://example.com\"", BasicMarkdownToHtml.Convert(sample, untrustedLinks: false), StringComparison.Ordinal);
    }

    private static string ConvertUntrusted(string markdown)
        => BasicMarkdownToHtml.Convert(markdown, untrustedLinks: true);
}
