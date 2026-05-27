using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class StarterThemeScaffoldTests : IDisposable
{
    private readonly string _rootDir;

    public StarterThemeScaffoldTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-scaffold-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void WriteTo_DefaultOverload_CreatesStarterThemeFiles()
    {
        StarterThemeScaffold.WriteTo(_rootDir);

        var themeRoot = Path.Combine(_rootDir, "themes", "starter");
        Assert.True(File.Exists(Path.Combine(themeRoot, "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "seo.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "footer.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "post.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "bukit.templates.yaml")));

        var css = File.ReadAllText(Path.Combine(themeRoot, "assets", "style.css"));
        Assert.Contains("--primary: #0b5fff;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #0f7b6c;", css, StringComparison.Ordinal);
        Assert.Contains("pre code[class*=\"language-\"]", css, StringComparison.Ordinal);
        Assert.Contains(".token.keyword", css, StringComparison.Ordinal);
        Assert.Contains(".hljs-keyword", css, StringComparison.Ordinal);
    }

    [Fact]
    public void StyleCssFallback_IncludesSyntaxHighlightingSelectors()
    {
        var css = StarterThemeResources.StyleCss;

        Assert.Contains("pre code[class*=\"language-\"]", css, StringComparison.Ordinal);
        Assert.Contains(".token.comment", css, StringComparison.Ordinal);
        Assert.Contains(".hljs-string", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_CustomThemeNameAndColors_ReplacesColorVars()
    {
        StarterThemeScaffold.WriteTo(_rootDir, "custom", "#ff0000", "#00ff00");

        var themeRoot = Path.Combine(_rootDir, "themes", "custom");
        Assert.True(File.Exists(Path.Combine(themeRoot, "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "layouts", "base.html")));

        var css = File.ReadAllText(Path.Combine(themeRoot, "assets", "style.css"));
        Assert.Contains("--primary: #ff0000;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #00ff00;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--primary: #0b5fff;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--accent: #0f7b6c;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyColorOverrides_NullColors_ReturnsUnchanged()
    {
        var original = StarterThemeResources.StyleCss;

        var result = StarterThemeScaffold.ApplyColorOverrides(original, null, null);

        Assert.Equal(original, result);
    }

    [Fact]
    public void ApplyColorOverrides_PrimaryOnly_ReplacesOnlyPrimary()
    {
        var result = StarterThemeScaffold.ApplyColorOverrides(StarterThemeResources.StyleCss, "#ffffff", null);

        Assert.Contains("--primary: #ffffff;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("--primary: #0b5fff;", result, StringComparison.Ordinal);
        Assert.Contains("--accent: #0f7b6c;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyColorOverrides_AccentOnly_ReplacesOnlyAccent()
    {
        var result = StarterThemeScaffold.ApplyColorOverrides(StarterThemeResources.StyleCss, null, "#abcdef");

        Assert.Contains("--accent: #abcdef;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("--accent: #0f7b6c;", result, StringComparison.Ordinal);
        Assert.Contains("--primary: #0b5fff;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyColorOverrides_BothColors_ReplacesBoth()
    {
        var result = StarterThemeScaffold.ApplyColorOverrides(StarterThemeResources.StyleCss, "#111111", "#222222");

        Assert.Contains("--primary: #111111;", result, StringComparison.Ordinal);
        Assert.Contains("--accent: #222222;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("--primary: #0b5fff;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("--accent: #0f7b6c;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyColorOverrides_WhitespaceColors_Ignored()
    {
        var result = StarterThemeScaffold.ApplyColorOverrides(StarterThemeResources.StyleCss, "   ", "\t");

        Assert.Contains("--primary: #0b5fff;", result, StringComparison.Ordinal);
        Assert.Contains("--accent: #0f7b6c;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTo_CreatesCorrectDirectoryStructure()
    {
        StarterThemeScaffold.WriteTo(_rootDir, "mystarter", null, null);

        var layoutsRoot = Path.Combine(_rootDir, "themes", "mystarter", "layouts");
        Assert.True(Directory.Exists(Path.Combine(layoutsRoot, "layouts")));
        Assert.True(Directory.Exists(Path.Combine(layoutsRoot, "partials")));
        Assert.True(Directory.Exists(Path.Combine(layoutsRoot, "pages")));

        var assetsRoot = Path.Combine(_rootDir, "themes", "mystarter", "assets");
        Assert.True(Directory.Exists(assetsRoot));

        Assert.True(File.Exists(Path.Combine(layoutsRoot, "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "partials", "seo.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "partials", "analytics.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "partials", "footer.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "partials", "list-card.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "partials", "pagination-nav.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "page.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "post.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "list.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "pagination.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "taxonomy-index.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "taxonomy-term.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "pages", "search.html")));
        Assert.True(File.Exists(Path.Combine(layoutsRoot, "bukit.templates.yaml")));
    }
}
