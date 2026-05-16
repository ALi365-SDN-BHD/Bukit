using System.Reflection;
using Bukit.Cli;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ThemeCommandExtendedTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;

    private static readonly MethodInfo s_isSafeThemeName = typeof(ThemeCommand)
        .GetMethod("IsSafeThemeName", BindingFlags.NonPublic | BindingFlags.Static)!;

    public ThemeCommandExtendedTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-theme-ext-tests-" + Guid.NewGuid().ToString("N"));
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

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("normal-theme", true)]
    public void IsSafeThemeName_ReturnsExpected(string? name, bool expected)
    {
        var result = (bool)s_isSafeThemeName.Invoke(null, new object?[] { name })!;
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwo()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[] { "theme", "unknown-cmd" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_ListWithNoThemesDir_ReturnsZero()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "list", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_ListWithThemesDir_ListsThemeNames()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "theme-a", "layouts"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "theme-b", "assets"));

        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "list", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_ListSkipsDirsWithoutLayoutsAssetsOrStatic()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "has-layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "empty-dir"));

        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "list", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_UseMissingTheme_ReturnsTwo()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "use"
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_UseValidTheme_UpdatesSiteYaml()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "my-theme", "layouts"));

        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "use", "my-theme", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
        var yaml = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("name: my-theme", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseAsync_NonExistentConfig_ReturnsTwo()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "use", "some-theme", "--config", Path.Combine(_rootDir, "nonexistent.yaml")
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task CreateAsync_WithBrandParam_SetsBrandAndFooterText()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "branded",
            "--config", _configPath,
            "--from", "starter",
            "--brand", "My Site",
            "--use"
        }));

        Assert.Equal(0, exitCode);
        var yaml = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("brand: My Site", yaml, StringComparison.Ordinal);
        Assert.Contains("footer_text: My Site", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_WithPrimaryAccentColorParams_WritesColors()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "colorful",
            "--config", _configPath,
            "--from", "starter",
            "--primary-color", "#ff0000",
            "--accent-color", "#00ff00",
            "--use"
        }));

        Assert.Equal(0, exitCode);
        var yaml = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("primary_color: '#ff0000'", yaml, StringComparison.Ordinal);
        Assert.Contains("accent_color: '#00ff00'", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyDirectory_CopiesFilesFromSourceToDest()
    {
        var sourceDir = Path.Combine(_rootDir, "source");
        var destDir = Path.Combine(_rootDir, "dest");
        Directory.CreateDirectory(Path.Combine(sourceDir, "subdir"));
        File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "content1");
        File.WriteAllText(Path.Combine(sourceDir, "subdir", "file2.txt"), "content2");

        var copyMethod = typeof(ThemeCommand)
            .GetMethod("CopyDirectory", BindingFlags.NonPublic | BindingFlags.Static)!;
        copyMethod.Invoke(null, new object[] { sourceDir, destDir });

        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "subdir", "file2.txt")));
        Assert.Equal("content1", File.ReadAllText(Path.Combine(destDir, "file1.txt")));
        Assert.Equal("content2", File.ReadAllText(Path.Combine(destDir, "subdir", "file2.txt")));
    }

    [Fact]
    public async Task CreateAsync_WithForce_OverwritesExistingTheme()
    {
        var themeDir = Path.Combine(_rootDir, "themes", "overwrite-me");
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
        File.WriteAllText(Path.Combine(themeDir, "old.txt"), "old");

        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "overwrite-me",
            "--config", _configPath,
            "--from", "starter",
            "--force"
        }));

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(themeDir, "old.txt")));
    }

    [Fact]
    public async Task CreateAsync_SameSourceAndDestination_ReturnsTwo()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "same-name", "layouts"));

        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "same-name",
            "--config", _configPath,
            "--from", "same-name"
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task CreateAsync_FromExistingNonStarterTheme_CopiesThemeFiles()
    {
        var sourceRoot = Path.Combine(_rootDir, "themes", "source-theme");
        Directory.CreateDirectory(Path.Combine(sourceRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(sourceRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "layouts", "pages", "index.html"), "custom-index");
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "assets", "custom.css"), ".custom {}");

        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[]
        {
            "theme", "create", "copied-theme",
            "--config", _configPath,
            "--from", "source-theme"
        }));

        Assert.Equal(0, exitCode);
        var destRoot = Path.Combine(_rootDir, "themes", "copied-theme");
        Assert.True(File.Exists(Path.Combine(destRoot, "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(destRoot, "assets", "custom.css")));
        Assert.Equal(
            "custom-index",
            await File.ReadAllTextAsync(Path.Combine(destRoot, "layouts", "pages", "index.html")));
    }
}
