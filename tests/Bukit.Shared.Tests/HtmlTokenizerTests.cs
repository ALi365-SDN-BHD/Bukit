using Bukit.Shared.Notion;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class HtmlTokenizerTests
{
    [Fact]
    public void Tokenize_PreservesSelfClosingTagAttributes_AndDecodesTextEntities()
    {
        var tokens = HtmlTokenizer.Tokenize(
            "<p>Hello &amp; goodbye<img src=\"https://example.com/a.png\" alt=\"Hero\" /></p>");

        Assert.Collection(tokens,
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.OpenTag, token.Type);
                Assert.Equal("p", token.TagName);
            },
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.Text, token.Type);
                Assert.Equal("Hello & goodbye", token.TextContent);
            },
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.SelfClosingTag, token.Type);
                Assert.Equal("img", token.TagName);
                Assert.Contains("src=\"https://example.com/a.png\"", token.Attributes);
                Assert.Contains("alt=\"Hero\"", token.Attributes);
            },
            token =>
            {
                Assert.Equal(HtmlTokenizer.HtmlTokenType.CloseTag, token.Type);
                Assert.Equal("p", token.TagName);
            });
    }

    [Theory]
    [InlineData("DIV class=\"notice\"", "div")]
    [InlineData("  h2  ", "h2")]
    [InlineData("img src=\"/a.png\"", "img")]
    public void ExtractTagName_TrimsAndNormalizesCase(string tagContent, string expected)
    {
        var result = HtmlTokenizer.ExtractTagName(tagContent);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DecodeHtmlEntities_DecodesSupportedEntities()
    {
        var result = HtmlTokenizer.DecodeHtmlEntities("&lt;tag&gt; &quot;quoted&quot; &#39;text&#39; &nbsp;");

        Assert.Equal("<tag> \"quoted\" 'text'  ", result);
    }
}
