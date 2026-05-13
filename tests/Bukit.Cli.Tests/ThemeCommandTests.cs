using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ThemeCommandTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;

    public ThemeCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-theme-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        _configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                       content:
                                         provider: markdown
                                       theme:
                                         name: starter
                                       """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsTwo_WhenThemeNameMissing()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[] { "theme", "use" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task CreateAsync_CreatesStarterBasedTheme()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "custom",
            "--config", _configPath,
            "--brand", "Custom Site",
            "--primary-color", "#123456",
            "--accent-color", "#654321"
        }));

        Assert.Equal(0, exitCode);
        var themeRoot = Path.Combine(_rootDir, "themes", "custom");
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "list-card.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "search.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "bukit.templates.yaml")));
        var css = await File.ReadAllTextAsync(Path.Combine(themeRoot, "assets", "style.css"));
        Assert.Contains("--primary: #123456;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #654321;", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_ReturnsTwo_WhenThemeAlreadyExistsWithoutForce()
    {
        var first = await ThemeCommand.RunAsync(new ArgReader(new[] { "theme", "create", "custom", "--config", _configPath }));
        var second = await ThemeCommand.RunAsync(new ArgReader(new[] { "theme", "create", "custom", "--config", _configPath }));

        Assert.Equal(0, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task CreateAsync_WithUse_UpdatesSiteYaml()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "custom",
            "--config", _configPath,
            "--brand", "Custom Site",
            "--primary-color", "#123456",
            "--use"
        }));

        Assert.Equal(0, exitCode);
        var yaml = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("name: custom", yaml, StringComparison.Ordinal);
        Assert.Contains("brand: Custom Site", yaml, StringComparison.Ordinal);
        Assert.Contains("footer_text: Custom Site", yaml, StringComparison.Ordinal);
        Assert.Contains("primary_color: '#123456'", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_FromExistingTheme_CopiesThemeFiles()
    {
        var sourceRoot = Path.Combine(_rootDir, "themes", "source");
        Directory.CreateDirectory(Path.Combine(sourceRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(sourceRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "layouts", "pages", "index.html"), "source-index");
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "assets", "style.css"), ":root { --primary: #0b5fff; --accent: #0f7b6c; }");

        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "copy",
            "--config", _configPath,
            "--from", "source",
            "--primary-color", "#abcdef"
        }));

        Assert.Equal(0, exitCode);
        Assert.Equal(
            "source-index",
            await File.ReadAllTextAsync(Path.Combine(_rootDir, "themes", "copy", "layouts", "pages", "index.html")));
        var css = await File.ReadAllTextAsync(Path.Combine(_rootDir, "themes", "copy", "assets", "style.css"));
        Assert.Contains("--primary: #abcdef;", css, StringComparison.Ordinal);
    }
}
