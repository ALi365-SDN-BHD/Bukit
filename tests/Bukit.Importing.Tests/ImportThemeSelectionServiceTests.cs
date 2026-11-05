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
    public async Task SetThemeAsync_KeepsCommentsAndEditsOnlyTheThemeBlock()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "demo"));
        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, """
# top comment
site:
  name: existing # inline comment
theme:
  name: old # keep this
  params:
    brand: Old

# tail comment
""");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportThemeSelectionService.SetThemeAsync(
                "demo", configPath, _rootDir, brand: "New", primaryColor: null, accentColor: null));

        Assert.Equal(0, result.ExitCode);
        var yaml = File.ReadAllText(configPath);
        Assert.Contains("# top comment", yaml, StringComparison.Ordinal);
        Assert.Contains("# inline comment", yaml, StringComparison.Ordinal);
        Assert.Contains("# keep this", yaml, StringComparison.Ordinal);
        Assert.Contains("# tail comment", yaml, StringComparison.Ordinal);
        Assert.Contains("name: demo", yaml, StringComparison.Ordinal);
        Assert.Contains("brand: New", yaml, StringComparison.Ordinal);
        Assert.Contains("name: existing", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetThemeAsync_FlowStyleTheme_KeepsTheRestOfTheConfig()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "demo"));
        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, """
# top comment
site:
  name: existing
theme: {name: old, params: {brand: Old}} # trailing
# bottom comment
""");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportThemeSelectionService.SetThemeAsync(
                "demo", configPath, _rootDir, brand: "New", primaryColor: null, accentColor: null));

        Assert.Equal(0, result.ExitCode);
        var yaml = File.ReadAllText(configPath);
        Assert.Contains("# top comment", yaml, StringComparison.Ordinal);
        Assert.Contains("# bottom comment", yaml, StringComparison.Ordinal);
        Assert.Contains("name: existing", yaml, StringComparison.Ordinal);
        Assert.Contains("name: demo", yaml, StringComparison.Ordinal);
        Assert.Contains("brand: New", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetThemeAsync_NestedName_IsNotMistakenForThemeName()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "demo"));
        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, """
theme:
  params:
    name: brand-name
""");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportThemeSelectionService.SetThemeAsync(
                "demo", configPath, _rootDir, brand: null, primaryColor: null, accentColor: null));

        Assert.Equal(0, result.ExitCode);

        var stream = new YamlDotNet.RepresentationModel.YamlStream();
        stream.Load(new StringReader(File.ReadAllText(configPath)));
        var root = Assert.IsType<YamlDotNet.RepresentationModel.YamlMappingNode>(stream.Documents[0].RootNode);
        var theme = Assert.IsType<YamlDotNet.RepresentationModel.YamlMappingNode>(
            root.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("theme")]);
        var parameters = Assert.IsType<YamlDotNet.RepresentationModel.YamlMappingNode>(
            theme.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("params")]);

        Assert.Equal("demo", ((YamlDotNet.RepresentationModel.YamlScalarNode)
            theme.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("name")]).Value);
        Assert.Equal("brand-name", ((YamlDotNet.RepresentationModel.YamlScalarNode)
            parameters.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("name")]).Value);
    }

    [Fact]
    public async Task SetThemeAsync_SameThemeTwice_DoesNotRewriteTheFile()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "demo"));
        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, """
# keep
theme:
  name: demo
""");

        var before = File.ReadAllText(configPath);

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportThemeSelectionService.SetThemeAsync(
                "demo", configPath, _rootDir, brand: null, primaryColor: null, accentColor: null));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(before, File.ReadAllText(configPath));
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
