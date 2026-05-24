using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeTokensProcessorTests
{
    [Fact]
    public void GenerateCss_EmptyTokens_GeneratesRootOnly()
    {
        var tokens = new ThemeTokens();
        var css = ThemeTokensProcessor.GenerateCss(tokens);
        Assert.Contains(":root {", css);
        Assert.Contains("}", css);
    }

    [Fact]
    public void GenerateCss_WithColors_GeneratesColorVariables()
    {
        var tokens = new ThemeTokens
        {
            Colors = new Dictionary<string, string>
            {
                ["background"] = "#ffffff",
                ["primary"] = "#0b5fff"
            }
        };

        var css = ThemeTokensProcessor.GenerateCss(tokens);
        Assert.Contains("--color-background: #ffffff;", css);
        Assert.Contains("--color-primary: #0b5fff;", css);
    }

    [Fact]
    public void GenerateCss_WithRadius_GeneratesRadiusVariables()
    {
        var tokens = new ThemeTokens
        {
            Radius = new Dictionary<string, string>
            {
                ["md"] = "8px",
                ["lg"] = "16px"
            }
        };

        var css = ThemeTokensProcessor.GenerateCss(tokens);
        Assert.Contains("--radius-md: 8px;", css);
        Assert.Contains("--radius-lg: 16px;", css);
    }

    [Fact]
    public void GenerateCss_UnderscoresInKeys_ConvertedToHyphens()
    {
        var tokens = new ThemeTokens
        {
            Spacing = new Dictionary<string, string>
            {
                ["section_y"] = "64px"
            }
        };

        var css = ThemeTokensProcessor.GenerateCss(tokens);
        Assert.Contains("--spacing-section-y: 64px;", css);
    }

    [Fact]
    public void GenerateCss_DotsInNestedKeys_ConvertedToHyphens()
    {
        var tokens = new ThemeTokens
        {
            Colors = new Dictionary<string, string>
            {
                ["brand.primary"] = "#0b5fff"
            }
        };

        var css = ThemeTokensProcessor.GenerateCss(tokens);
        Assert.Contains("--color-brand-primary: #0b5fff;", css);
    }
}
