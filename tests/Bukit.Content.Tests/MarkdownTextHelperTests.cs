using Bukit.Content.Markdown;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class MarkdownTextHelperTests
{
    [Fact]
    public void ExtractSummaryFromMarkdown_ShortText_ReturnsFull()
    {
        var md = "Just a short line.";

        var result = MarkdownTextHelper.ExtractSummaryFromMarkdown(md, 100);

        Assert.Equal("Just a short line.", result);
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_LongText_Truncates()
    {
        var md = "This is a very long sentence that should be truncated at some word boundary.";

        var result = MarkdownTextHelper.ExtractSummaryFromMarkdown(md, 30);

        Assert.True(result.Length <= 33);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_WithHtml_StripsTags()
    {
        var md = "Hello **world** with _emphasis_";

        var result = MarkdownTextHelper.ExtractSummaryFromMarkdown(md, 50);

        Assert.Contains("Hello", result);
        Assert.Contains("world", result);
        Assert.Contains("emphasis", result);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_MaxLengthZero_ReturnsEmpty()
    {
        var md = "Some text.";

        var result = MarkdownTextHelper.ExtractSummaryFromMarkdown(md, 0);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_NegativeLength_ReturnsEmpty()
    {
        var md = "Text.";

        var result = MarkdownTextHelper.ExtractSummaryFromMarkdown(md, -1);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_EmptyString_ReturnsEmpty()
    {
        var result = MarkdownTextHelper.ExtractSummaryFromMarkdown("", 100);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractSummaryFromMarkdown_WhitespaceOnly_ReturnsEmpty()
    {
        var result = MarkdownTextHelper.ExtractSummaryFromMarkdown("   ", 100);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractTitle_H1Found_ReturnsTitle()
    {
        var md = "# Hello World\n\nSome body.";

        var result = MarkdownTextHelper.ExtractTitle(md);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ExtractTitle_NoH1_ReturnsNull()
    {
        var md = "## Subheading\n\nBody.";

        var result = MarkdownTextHelper.ExtractTitle(md);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractTitle_EmptyString_ReturnsNull()
    {
        var result = MarkdownTextHelper.ExtractTitle("");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractTitle_H1WithLeadingWhitespace_ReturnsTrimmed()
    {
        var md = "  #  Hello  ";

        var result = MarkdownTextHelper.ExtractTitle(md);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void ExtractTitle_H2NotH1_ReturnsNull()
    {
        var md = "## Not H1\n\nBody.";

        var result = MarkdownTextHelper.ExtractTitle(md);

        Assert.Null(result);
    }

    [Fact]
    public async Task RenderHtmlFromFileAsync_WithFrontMatter_StripsFrontMatter()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, """
                ---
                title: Test
                ---
                # Hello
                Body text.
                """);

            var html = await MarkdownTextHelper.RenderHtmlFromFileAsync(filePath, CancellationToken.None);

            Assert.Contains("<h1", html);
            Assert.Contains("Hello", html);
            Assert.Contains("Body text.", html);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task RenderHtmlFromFileAsync_NoFrontMatter_RendersFull()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "# Just a heading\n\nSome paragraph.");

            var html = await MarkdownTextHelper.RenderHtmlFromFileAsync(filePath, CancellationToken.None);

            Assert.Contains("<h1", html);
            Assert.Contains("Some paragraph.", html);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
