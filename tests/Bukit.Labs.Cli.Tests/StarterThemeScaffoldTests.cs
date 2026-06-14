using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class StarterThemeScaffoldTests : IDisposable
{
    private readonly string _rootDir;

    public StarterThemeScaffoldTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-scaffold-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
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
    }

    [Fact]
    public void ApplyColorOverrides_BothColors_ReplacesBoth()
    {
        var result = StarterThemeScaffold.ApplyColorOverrides(ThemeTemplateResource.Get("StyleCss"), "#111111", "#222222");

        Assert.Contains("--primary: #111111;", result, StringComparison.Ordinal);
        Assert.Contains("--accent: #222222;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("--primary: #0b5fff;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("--accent: #0f7b6c;", result, StringComparison.Ordinal);
    }
}
