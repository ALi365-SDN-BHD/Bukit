using System.Text;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneStyleSheetGeneratorTests
{
    [Fact]
    public void GenerateStyleCss_DefaultTokens_EmitsFallbackVariablesAndCoreSelectors()
    {
        var css = CloneStyleSheetGenerator.GenerateStyleCss(CloneTokens.Default);

        Assert.Contains("--bg: #fbfaf8;", css, StringComparison.Ordinal);
        Assert.Contains("--surface: #ffffff;", css, StringComparison.Ordinal);
        Assert.Contains("--font-size-display: clamp(2rem, 5vw, 4.2rem);", css, StringComparison.Ordinal);
        Assert.Contains(".hero-cta {", css, StringComparison.Ordinal);
        Assert.Contains(".state-tab[aria-selected=\"true\"]", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 680px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 1024px) and (max-width: calc(1440px - 1px))", css, StringComparison.Ordinal);
        Assert.Contains("font-family: system-ui", css, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateStyleCss_CustomTokens_UsesOverridesAndSpacingVariables()
    {
        var css = CloneStyleSheetGenerator.GenerateStyleCss(new CloneTokens
        {
            Bg = " #101010 ",
            Surface = "#111111",
            Primary = "#ff6600",
            HeadingFontFamily = "\"Inter Tight\", sans-serif",
            CodeFontFamily = "\"Fira Code\", monospace",
            NavPadding = "12px 16px",
            ContainerPadding = "20px",
            ResponsiveBreakpoints = new ResponsiveBreakpoints
            {
                Mobile = "600px",
                Tablet = "960px",
                Desktop = "1280px"
            },
            SpacingScale = new SpacingScale
            {
                Xs = "0.25rem",
                Md = "1rem",
                Xl = "3rem"
            }
        });

        Assert.Contains("--bg: #101010;", css, StringComparison.Ordinal);
        Assert.Contains("--surface: #111111;", css, StringComparison.Ordinal);
        Assert.Contains("--primary: #ff6600;", css, StringComparison.Ordinal);
        Assert.Contains("--nav-padding: 12px 16px;", css, StringComparison.Ordinal);
        Assert.Contains("--container-padding: 20px;", css, StringComparison.Ordinal);
        Assert.Contains("--bp-mobile: 600px;", css, StringComparison.Ordinal);
        Assert.Contains("--bp-tablet: 960px;", css, StringComparison.Ordinal);
        Assert.Contains("--bp-desktop: 1280px;", css, StringComparison.Ordinal);
        Assert.Contains("--space-xs: 0.25rem;", css, StringComparison.Ordinal);
        Assert.Contains("--space-md: 1rem;", css, StringComparison.Ordinal);
        Assert.Contains("--space-xl: 3rem;", css, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Inter Tight\", sans-serif;", css, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Fira Code\", monospace;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--space-sm:", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Helpers_CAddVarAndEsc_HandleFallbacksTrimAndEscaping()
    {
        Assert.Equal("fallback", CloneStyleSheetGenerator.C(null, "fallback"));
        Assert.Equal("trimmed", CloneStyleSheetGenerator.C("  trimmed  ", "fallback"));

        var builder = new StringBuilder();
        CloneStyleSheetGenerator.AddVar(builder, "--space-xs", null);
        CloneStyleSheetGenerator.AddVar(builder, "--space-sm", "1rem");

        Assert.Equal("  --space-sm: 1rem;" + Environment.NewLine, builder.ToString());
        Assert.Equal("&lt;tag attr=&quot;x&quot;&gt;&amp;", CloneStyleSheetGenerator.Esc("<tag attr=\"x\">&"));
    }
}
