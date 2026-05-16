using Bukit.Content.Markdown;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class BasicMarkdownToHtmlTests
{
    [Fact]
    public void Convert_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BasicMarkdownToHtml.Convert(""));
        Assert.Equal(string.Empty, BasicMarkdownToHtml.Convert(" \n\t "));
    }

    [Fact]
    public void Convert_Headings_RendersH1H2H3()
    {
        var html = BasicMarkdownToHtml.Convert("""
        # Title
        ## Section
        ### Detail
        """);

        Assert.Equal("""
        <h1>Title</h1>
        <h2>Section</h2>
        <h3>Detail</h3>
        """.Replace("\r\n", "\n"), html);
    }

    [Fact]
    public void Convert_Paragraph_EscapesHtml()
    {
        var html = BasicMarkdownToHtml.Convert("Hello <script>alert(1)</script>");

        Assert.Equal("<p>Hello &lt;script&gt;alert(1)&lt;/script&gt;</p>", html);
    }

    [Fact]
    public void Convert_ImageWithoutTitle_RendersImg()
    {
        var html = BasicMarkdownToHtml.Convert("![Alt <text>](https://example.com/a.png)");

        Assert.Equal("<img src=\"https://example.com/a.png\" alt=\"Alt &lt;text&gt;\" />", html);
    }

    [Fact]
    public void Convert_ImageWithTitle_RendersTitleAttribute()
    {
        var html = BasicMarkdownToHtml.Convert("![Logo](https://example.com/logo.png \"Company <Logo>\")");

        Assert.Equal("<img src=\"https://example.com/logo.png\" alt=\"Logo\" title=\"Company &lt;Logo&gt;\" />", html);
    }

    [Fact]
    public void Convert_MixedLines_SkipsBlankLinesAndTrimsTrailingWhitespace()
    {
        var html = BasicMarkdownToHtml.Convert("""
        # Title   

        Body text   
        ![Image](https://example.com/img.jpg)
        """);

        Assert.Equal("""
        <h1>Title</h1>
        <p>Body text</p>
        <img src="https://example.com/img.jpg" alt="Image" />
        """.Replace("\r\n", "\n"), html);
    }
}
