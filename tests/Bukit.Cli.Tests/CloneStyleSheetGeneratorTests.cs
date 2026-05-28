using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneStyleSheetGeneratorTests
{
    [Fact]
    public void GenerateStyleCss_DefaultTokens_ContainsRootAndCssVariables()
    {
        var tokens = new CloneTokens();

        var result = CloneStyleSheetGenerator.GenerateStyleCss(tokens);

        Assert.Contains(":root {", result);
        Assert.Contains("--bg:", result);
        Assert.Contains("--surface:", result);
        Assert.Contains("--primary:", result);
        Assert.Contains("--accent:", result);
        Assert.Contains("font-family:", result);
    }

    [Fact]
    public void GenerateStyleCss_CustomFontFamily_ContainsArial()
    {
        var tokens = new CloneTokens { FontFamily = "Arial" };

        var result = CloneStyleSheetGenerator.GenerateStyleCss(tokens);

        Assert.Contains("font-family: Arial", result);
    }

    [Fact]
    public void GenerateStyleCss_CustomResponsiveBreakpoints_ContainsMediaQuery()
    {
        var tokens = new CloneTokens
        {
            ResponsiveBreakpoints = new ResponsiveBreakpoints { Mobile = "480px" }
        };

        var result = CloneStyleSheetGenerator.GenerateStyleCss(tokens);

        Assert.Contains("@media (max-width: 480px)", result);
    }

    [Fact]
    public void C_NullValue_ReturnsFallback()
    {
        var result = CloneStyleSheetGenerator.C(null, "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void C_EmptyValue_ReturnsFallback()
    {
        var result = CloneStyleSheetGenerator.C("", "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void C_WhitespaceValue_ReturnsFallback()
    {
        var result = CloneStyleSheetGenerator.C("   ", "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void C_ValidValue_ReturnsTrimmedValue()
    {
        var result = CloneStyleSheetGenerator.C("  value  ", "fallback");

        Assert.Equal("value", result);
    }

    [Fact]
    public void C_ExactValue_ReturnsValue()
    {
        var result = CloneStyleSheetGenerator.C("value", "fallback");

        Assert.Equal("value", result);
    }

    [Fact]
    public void Esc_ScriptTag_ReturnsEncoded()
    {
        var result = CloneStyleSheetGenerator.Esc("<script>");

        Assert.Equal("&lt;script&gt;", result);
    }

    [Fact]
    public void Esc_Ampersand_ReturnsEncoded()
    {
        var result = CloneStyleSheetGenerator.Esc("a & b");

        Assert.Equal("a &amp; b", result);
    }

    [Fact]
    public void Esc_DoubleQuote_ReturnsEncoded()
    {
        var result = CloneStyleSheetGenerator.Esc("\"test\"");

        Assert.Equal("&quot;test&quot;", result);
    }

    [Fact]
    public void Esc_MixedSpecialChars_ReturnsAllEncoded()
    {
        var result = CloneStyleSheetGenerator.Esc("a<b>c&d\"e");

        Assert.Equal("a&lt;b&gt;c&amp;d&quot;e", result);
    }

    [Fact]
    public void Esc_PlainText_ReturnsUnchanged()
    {
        var result = CloneStyleSheetGenerator.Esc("hello world");

        Assert.Equal("hello world", result);
    }
}
