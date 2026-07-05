using Xunit;

namespace Bukit.Importing.Tests;

[Collection("ImportingConsole")]
public sealed class ImportThemeSelectionServiceTests : IDisposable
{
    private readonly string _rootDir;

    public ImportThemeSelectionServiceTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-importing-theme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task SetThemeAsync_WritesThemeNameAndOptionalParamsWithoutDroppingExistingYaml()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "demo"));
        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, """
site:
  name: existing
  title: Existing Site
content:
  sources:
    - type: markdown
      markdown:
        dir: content
""");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportThemeSelectionService.SetThemeAsync(
                "demo",
                configPath,
                _rootDir,
                brand: "Bukit",
                primaryColor: "#123456",
                accentColor: "#abcdef"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Theme set: demo", result.StdOut, StringComparison.Ordinal);

        var yaml = File.ReadAllText(configPath);
        Assert.Contains("name: existing", yaml, StringComparison.Ordinal);
        Assert.Contains("theme:", yaml, StringComparison.Ordinal);
        Assert.Contains("name: demo", yaml, StringComparison.Ordinal);
        Assert.Contains("brand: Bukit", yaml, StringComparison.Ordinal);
        Assert.Contains("footer_text: Bukit", yaml, StringComparison.Ordinal);
        Assert.Contains("primary_color: '#123456'", yaml, StringComparison.Ordinal);
        Assert.Contains("accent_color: '#abcdef'", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetThemeAsync_MissingTheme_ReturnsTwoAndDoesNotWriteConfig()
    {
        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, "site:\n  name: existing\n");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportThemeSelectionService.SetThemeAsync(
                "missing",
                configPath,
                _rootDir,
                brand: null,
                primaryColor: null,
                accentColor: null));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Theme not found: missing", result.StdErr, StringComparison.Ordinal);
        Assert.Equal("site:\n  name: existing\n", File.ReadAllText(configPath));
    }
}
