using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class ThemeFileHelperTests : IDisposable
{
    private readonly string _tempDir;

    public ThemeFileHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-labs-theme-file-helper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Theory]
    [InlineData("starter", true)]
    [InlineData("starter-2", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("../evil", false)]
    public void IsSafeThemeName_ReturnsExpectedValue(string name, bool expected)
    {
        Assert.Equal(expected, ThemeFileHelper.IsSafeThemeName(name));
    }

    [Fact]
    public void ApplyCssColorOverrides_UpdatesExistingStyleFile()
    {
        var themeRoot = Path.Combine(_tempDir, "theme");
        var assetsDir = Path.Combine(themeRoot, "assets");
        Directory.CreateDirectory(assetsDir);
        var stylePath = Path.Combine(assetsDir, "style.css");
        File.WriteAllText(stylePath, ":root { --primary: #0b5fff; --accent: #0f7b6c; }");

        ThemeFileHelper.ApplyCssColorOverrides(themeRoot, "#ffffff", "#000000");

        var css = File.ReadAllText(stylePath);
        Assert.Contains("--primary: #ffffff;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #000000;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyDirectory_CopiesNestedFiles()
    {
        var sourceDir = Path.Combine(_tempDir, "source");
        var destinationDir = Path.Combine(_tempDir, "destination");
        Directory.CreateDirectory(Path.Combine(sourceDir, "nested"));
        File.WriteAllText(Path.Combine(sourceDir, "nested", "theme.yaml"), "name: test");

        ThemeFileHelper.CopyDirectory(sourceDir, destinationDir);

        Assert.True(File.Exists(Path.Combine(destinationDir, "nested", "theme.yaml")));
    }
}
