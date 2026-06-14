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
}
