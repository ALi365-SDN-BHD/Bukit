using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Theme;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ThemeInfoPrinterTests : IDisposable
{
    private readonly string _themeRoot;
    private readonly StringWriter _stdout;

    public ThemeInfoPrinterTests()
    {
        _themeRoot = Path.Combine(Path.GetTempPath(), "bukit-info-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_themeRoot);
        _stdout = new StringWriter();
        Console.SetOut(_stdout);
    }

    public void Dispose()
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        _stdout.Dispose();
        if (Directory.Exists(_themeRoot))
        {
            Directory.Delete(_themeRoot, recursive: true);
        }
    }

    [Fact]
    public void PrintSections_EmptySections_NoOutput()
    {
        var manifest = CreateManifest();

        ThemeInfoPrinter.PrintSections(manifest, _themeRoot);

        Assert.DoesNotContain("Sections", _stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PrintSections_WithSections_PrintsList()
    {
        var manifest = CreateManifest(sections: new Dictionary<string, ThemeSectionDefinition>
        {
            ["hero"] = new() { Description = "A hero banner" },
            ["footer"] = new() { Description = "Page footer" }
        });

        ThemeInfoPrinter.PrintSections(manifest, _themeRoot);

        var output = _stdout.ToString();
        Assert.Contains("hero", output, StringComparison.Ordinal);
        Assert.Contains("footer", output, StringComparison.Ordinal);
        Assert.Contains("Sections (2)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintSections_LongDescription_Truncated()
    {
        var longDesc = new string('x', 60);
        var manifest = CreateManifest(sections: new Dictionary<string, ThemeSectionDefinition>
        {
            ["wide"] = new() { Description = longDesc }
        });

        ThemeInfoPrinter.PrintSections(manifest, _themeRoot);

        var output = _stdout.ToString();
        Assert.Contains("..", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintSections_WithPlugin_ShowsAnnotation()
    {
        var manifest = CreateManifest(sections: new Dictionary<string, ThemeSectionDefinition>
        {
            ["custom"] = new() { Plugin = "my-plugin" }
        });

        ThemeInfoPrinter.PrintSections(manifest, _themeRoot);

        var output = _stdout.ToString();
        Assert.Contains("[plugin: my-plugin]", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintComponents_EmptyComponents_NoOutput()
    {
        var manifest = CreateManifest();

        ThemeInfoPrinter.PrintComponents(manifest);

        Assert.DoesNotContain("Components", _stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PrintComponents_WithProps_ShowsPropList()
    {
        var manifest = CreateManifest(components: new Dictionary<string, ThemeComponentDefinition>
        {
            ["button"] = new() { Props = new Dictionary<string, string> { ["label"] = "string", ["url"] = "string" } }
        });

        ThemeInfoPrinter.PrintComponents(manifest);

        var output = _stdout.ToString();
        Assert.Contains("button", output, StringComparison.Ordinal);
        Assert.Contains("label", output, StringComparison.Ordinal);
        Assert.Contains("url", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintComponents_NoProps_ShowsNameOnly()
    {
        var manifest = CreateManifest(components: new Dictionary<string, ThemeComponentDefinition>
        {
            ["spacer"] = new()
        });

        ThemeInfoPrinter.PrintComponents(manifest);

        var output = _stdout.ToString();
        Assert.Contains("spacer", output, StringComparison.Ordinal);
        Assert.DoesNotContain("props:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintTokens_NoTokensFile_NoOutput()
    {
        var manifest = CreateManifest(tokens: "nonexistent.yaml");

        ThemeInfoPrinter.PrintTokens(manifest, _themeRoot);

        Assert.DoesNotContain("Design tokens", _stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PrintTokens_WithTokens_PrintsSummary()
    {
        var tokensYaml = """
                         colors:
                           primary: "#ff0000"
                           secondary: "#00ff00"
                         font:
                           heading: "Inter"
                         """;
        File.WriteAllText(Path.Combine(_themeRoot, "tokens.yaml"), tokensYaml);
        var manifest = CreateManifest();

        ThemeInfoPrinter.PrintTokens(manifest, _themeRoot);

        var output = _stdout.ToString();
        Assert.Contains("colors (2)", output, StringComparison.Ordinal);
        Assert.Contains("font (1)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintLayouts_NoLayoutsDir_NoOutput()
    {
        var manifest = CreateManifest();

        ThemeInfoPrinter.PrintLayouts(manifest, _themeRoot);

        Assert.DoesNotContain("Layout templates", _stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PrintLayouts_WithLayouts_PrintsFiles()
    {
        var layoutsDir = Path.Combine(_themeRoot, "layouts");
        Directory.CreateDirectory(layoutsDir);
        File.WriteAllText(Path.Combine(layoutsDir, "default.scriban"), "");
        File.WriteAllText(Path.Combine(layoutsDir, "post.scriban"), "");

        var manifest = CreateManifest();

        ThemeInfoPrinter.PrintLayouts(manifest, _themeRoot);

        var output = _stdout.ToString();
        Assert.Contains("default.scriban", output, StringComparison.Ordinal);
        Assert.Contains("post.scriban", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintFileStats_WithAssets_PrintsCount()
    {
        var assetsDir = Path.Combine(_themeRoot, "assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "style.css"), "");

        ThemeInfoPrinter.PrintFileStats(_themeRoot);

        var output = _stdout.ToString();
        Assert.Contains("Assets: 1 files", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintFileStats_Empty_NoOutput()
    {
        ThemeInfoPrinter.PrintFileStats(_themeRoot);

        Assert.DoesNotContain("Assets:", _stdout.ToString(), StringComparison.Ordinal);
    }

    private static ThemeManifestV2 CreateManifest(
        Dictionary<string, ThemeSectionDefinition>? sections = null,
        Dictionary<string, ThemeComponentDefinition>? components = null,
        string? tokens = null)
    {
        return new ThemeManifestV2
        {
            Name = "test-theme",
            Version = "1.0.0",
            Sections = sections ?? new Dictionary<string, ThemeSectionDefinition>(),
            Components = components ?? new Dictionary<string, ThemeComponentDefinition>(),
            Tokens = tokens
        };
    }
}
