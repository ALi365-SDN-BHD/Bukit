using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class HtmlToMarkdownConverterTests
{
    [Fact]
    public void Convert_HeadingsParagraphsAndInlineFormatting_BecomeMarkdown()
    {
        var markdown = HtmlToMarkdownConverter.Convert(
            "<main><h1>Title</h1><p>Some <strong>bold</strong> and <em>italic</em> text.</p></main>");

        Assert.Equal("""
# Title

Some **bold** and *italic* text.
""", markdown);
    }

    [Fact]
    public void Convert_LinksImagesAndLists_BecomeMarkdown()
    {
        var markdown = HtmlToMarkdownConverter.Convert(
            """
            <ul><li>One</li><li>Two <a href="https://example.com">link</a></li></ul>
            <p><img src="/img/a.png" alt="A"></p>
            """);

        Assert.Contains("- One", markdown, StringComparison.Ordinal);
        Assert.Contains("- Two [link](https://example.com)", markdown, StringComparison.Ordinal);
        Assert.Contains("![A](/img/a.png)", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_CodeBlocks_KeepLanguageAndContent()
    {
        var markdown = HtmlToMarkdownConverter.Convert(
            """<pre><code class="language-csharp">var x = 1;</code></pre>""");

        Assert.Contains("```csharp", markdown, StringComparison.Ordinal);
        Assert.Contains("var x = 1;", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_RawHtmlAndScripts_DoNotLeakIntoTheBody()
    {
        var markdown = HtmlToMarkdownConverter.Convert(
            "<div class=\"wrapper\"><p>Visible</p><script>alert(1)</script><style>p{color:red}</style></div>");

        Assert.Equal("Visible", markdown);
        Assert.DoesNotContain("<p>", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("alert", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_EmptyInput_ReturnsEmptyString()
    {
        Assert.Equal("", HtmlToMarkdownConverter.Convert(null));
        Assert.Equal("", HtmlToMarkdownConverter.Convert("   "));
    }
}
