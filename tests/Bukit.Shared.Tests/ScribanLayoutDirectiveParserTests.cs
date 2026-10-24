using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class ScribanLayoutDirectiveParserTests
{
    [Fact]
    public void TryExtractLayoutDirective_ExtractsLiquidDirectiveAndBody()
    {
        var template = """

            {% layout "layouts/base.html" %}
            <main>{{ page.content }}</main>
            """;

        var ok = ScribanLayoutDirectiveParser.TryExtractLayoutDirective(
            template,
            out var layoutPath,
            out var body);

        Assert.True(ok);
        Assert.Equal("layouts/base.html", layoutPath);
        Assert.DoesNotContain("layout", body);
        Assert.Contains("<main>{{ page.content }}</main>", body);
    }

    [Fact]
    public void TryParseLayoutLine_SupportsMustacheSyntax()
    {
        var ok = ScribanLayoutDirectiveParser.TryParseLayoutLine(
            "{{ layout 'pages/post.html' }}",
            out var layoutPath);

        Assert.True(ok);
        Assert.Equal("pages/post.html", layoutPath);
    }

    [Fact]
    public void TryParseLayoutLine_NonLayoutDirective_ReturnsFalse()
    {
        var ok = ScribanLayoutDirectiveParser.TryParseLayoutLine(
            "{{ include 'header.html' }}",
            out var layoutPath);

        Assert.False(ok);
        Assert.Equal(string.Empty, layoutPath);
    }

    [Fact]
    public void TryParseDirective_RequiresMatchingOpenAndCloseTokens()
    {
        Assert.True(ScribanLayoutDirectiveParser.TryParseDirective("{{", "}}", "{{ layout \"x\" }}", out var inner));
        Assert.Equal("layout \"x\"", inner);
        Assert.False(ScribanLayoutDirectiveParser.TryParseDirective("{{", "}}", "{% layout \"x\" %}", out _));
    }

    [Fact]
    public void TryExtractQuotedString_SupportsSingleQuotesAndRejectsBlankValues()
    {
        Assert.True(ScribanLayoutDirectiveParser.TryExtractQuotedString("layout 'partials/hero.html'", out var singleQuoted));
        Assert.Equal("partials/hero.html", singleQuoted);
        Assert.False(ScribanLayoutDirectiveParser.TryExtractQuotedString("layout \"   \"", out _));
        Assert.False(ScribanLayoutDirectiveParser.TryExtractQuotedString("layout pages/base.html", out _));
    }

    [Fact]
    public void NormalizePath_ReplacesBackslashes()
    {
        Assert.Equal("layouts/base.html", ScribanLayoutDirectiveParser.NormalizePath(@"layouts\base.html"));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void TryExtractLayoutDirective_FirstContentLine_PreservesBodyAndNormalizesNewlines(string newline)
    {
        var template = $"{newline}  {newline}  {{% LAYOUT 'layouts/base.html' %}}  {newline}<main>body</main>{newline}";

        Assert.True(ScribanLayoutDirectiveParser.TryExtractLayoutDirective(template, out var path, out var body));
        Assert.Equal("layouts/base.html", path);
        Assert.Equal("\n  \n<main>body</main>\n", body);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    [InlineData("body\r\n{% layout 'base.html' %}")]
    [InlineData("{{ include 'header.html' }}\r\nbody")]
    [InlineData("{% layout base.html %}\r\nbody")]
    [InlineData("{% layout '' %}\r\nbody")]
    [InlineData("{% layout 'base.html %}\r\nbody")]
    [InlineData("{% layout 'base.html' }}\r\nbody")]
    public void TryExtractLayoutDirective_MissingOrInvalidFirstDirective_LeavesOriginalBody(string template)
    {
        Assert.False(ScribanLayoutDirectiveParser.TryExtractLayoutDirective(template, out var path, out var body));
        Assert.Empty(path);
        Assert.Equal(template, body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unquoted")]
    [InlineData("'unterminated")]
    [InlineData("\"unterminated")]
    [InlineData("''")]
    public void TryExtractQuotedString_EmptyOrIncompleteValue_ReturnsFalse(string text)
    {
        Assert.False(ScribanLayoutDirectiveParser.TryExtractQuotedString(text, out var value));
        Assert.Empty(value);
    }

    [Theory]
    [InlineData(@"..\layouts\\base.html", "../layouts//base.html")]
    [InlineData("/layouts/base.html", "/layouts/base.html")]
    [InlineData("", "")]
    public void NormalizePath_OnlyReplacesSeparators_AndIsIdempotent(string input, string expected)
    {
        var normalized = ScribanLayoutDirectiveParser.NormalizePath(input);

        Assert.Equal(expected, normalized);
        Assert.Equal(normalized, ScribanLayoutDirectiveParser.NormalizePath(normalized));
    }
}
