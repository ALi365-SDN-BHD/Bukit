using Bukit.Notion.Conversion;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class HtmlTokenizerTests
{
    [Fact]
    public void HtmlTokenType_OrdinalsRemainStable()
    {
        Assert.Equal(0, (int)HtmlTokenizer.HtmlTokenType.OpenTag);
        Assert.Equal(1, (int)HtmlTokenizer.HtmlTokenType.CloseTag);
        Assert.Equal(2, (int)HtmlTokenizer.HtmlTokenType.SelfClosingTag);
        Assert.Equal(3, (int)HtmlTokenizer.HtmlTokenType.Text);
    }

    [Fact]
    public void HtmlToken_DefaultsRemainStable()
    {
        var token = new HtmlTokenizer.HtmlToken();

        Assert.Equal(HtmlTokenizer.HtmlTokenType.OpenTag, token.Type);
        Assert.Equal(string.Empty, token.TagName);
        Assert.Equal(string.Empty, token.Attributes);
        Assert.Equal(string.Empty, token.TextContent);
    }

    [Fact]
    public void Tokenize_ProducesAllFourTokenKindsAndPreservesShape()
    {
        var tokens = HtmlTokenizer.Tokenize(
            "<p class=\"intro\">Hello &amp; goodbye<img src=\"https://example.com/a.png\" alt=\"Hero\" /></p>");

        Assert.Collection(
            tokens,
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.OpenTag, token.Type);
                Assert.Equal("p", token.TagName);
                Assert.Equal("p class=\"intro\"", token.Attributes);
                Assert.Equal(string.Empty, token.TextContent);
            },
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.Text, token.Type);
                Assert.Equal(string.Empty, token.TagName);
                Assert.Equal(string.Empty, token.Attributes);
                Assert.Equal("Hello & goodbye", token.TextContent);
            },
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.SelfClosingTag, token.Type);
                Assert.Equal("img", token.TagName);
                Assert.Contains("src=\"https://example.com/a.png\"", token.Attributes);
                Assert.Contains("alt=\"Hero\"", token.Attributes);
                Assert.Equal(string.Empty, token.TextContent);
            },
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.CloseTag, token.Type);
                Assert.Equal("p", token.TagName);
                Assert.Equal(string.Empty, token.Attributes);
                Assert.Equal(string.Empty, token.TextContent);
            });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    public void Tokenize_EmptyOrWhitespace_ReturnsNoTokens(string html)
    {
        Assert.Empty(HtmlTokenizer.Tokenize(html));
    }

    [Theory]
    [InlineData("<div")]
    [InlineData("<")]
    public void Tokenize_MissingClosingAngleBracket_ReturnsNoTokens(string html)
    {
        Assert.Empty(HtmlTokenizer.Tokenize(html));
    }

    [Theory]
    [InlineData("Before <div")]
    [InlineData("Before <")]
    public void Tokenize_UnmatchedOpeningAngleBracket_PreservesPriorText(string html)
    {
        var token = Assert.Single(HtmlTokenizer.Tokenize(html));

        Assert.Equal(HtmlTokenizer.HtmlTokenType.Text, token.Type);
        Assert.Equal("Before", token.TextContent);
    }

    [Fact]
    public void Tokenize_Null_PreservesCurrentExceptionType()
    {
        Assert.Throws<NullReferenceException>(() => HtmlTokenizer.Tokenize(null!));
    }

    [Theory]
    [InlineData("DIV class=\"notice\"", "div")]
    [InlineData("  h2  ", "h2")]
    [InlineData("img src=\"/a.png\"", "img")]
    public void ExtractTagName_TrimsAndNormalizesCase(string tagContent, string expected)
    {
        Assert.Equal(expected, HtmlTokenizer.ExtractTagName(tagContent));
    }

    [Fact]
    public void ExtractTagName_Null_PreservesCurrentExceptionType()
    {
        Assert.Throws<NullReferenceException>(() => HtmlTokenizer.ExtractTagName(null!));
    }

    [Fact]
    public void DecodeHtmlEntities_DecodesSupportedEntities()
    {
        var result = HtmlTokenizer.DecodeHtmlEntities(
            "&amp; &lt;tag&gt; &quot;quoted&quot; &#39;text&#39; &nbsp;");

        Assert.Equal("& <tag> \"quoted\" 'text'  ", result);
    }

    [Fact]
    public void DecodeHtmlEntities_Null_PreservesCurrentExceptionType()
    {
        Assert.Throws<NullReferenceException>(() => HtmlTokenizer.DecodeHtmlEntities(null!));
    }
}
