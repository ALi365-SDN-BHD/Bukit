using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class ThemeCommandTests : IDisposable
{
    private readonly string _rootDir;

    public ThemeCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-theme-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["mystery"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown theme subcommand: mystery", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_InvalidThemeName_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.CreateAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["create"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing or invalid theme name.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_SourceAndDestinationMustDiffer()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.CreateAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--from"] = "starter" },
                ["create", "starter"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Source and destination theme names must be different.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_WithoutThemesDirectory_PrintsHintAndReturnsZero()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.ListAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["list"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No themes directory found.", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InfoAsync_WithoutThemeName_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.InfoAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["info"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing theme name.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewAsync_WithoutThemeName_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.PreviewAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["preview"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing theme name.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParamsAsync_WithoutThemeName_ReturnsTwo()
    {
        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.ParamsAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["params"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing theme name.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseAsync_WithoutThemeName_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.UseAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["use"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing theme name.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetThemeAsync_WhenThemeDoesNotExist_ReturnsTwo()
    {
        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, "site:\n  name: demo\n");

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.SetThemeAsync("missing", configPath, _rootDir, null, null, null));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Theme not found: missing", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetThemeAsync_WhenConfigDoesNotExist_ReturnsTwo()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "starter");
        Directory.CreateDirectory(themeRoot);

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.SetThemeAsync("starter", Path.Combine(_rootDir, "site.yaml"), _rootDir, null, null, null));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Config not found:", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeCommands_WithGeneratedTheme_RunHappyPaths()
    {
        CloneThemeGenerator.WriteTo(
            _rootDir,
            "starter",
            CloneTokens.Default,
            new CloneLayoutInfo
            {
                SiteTitle = "Acme",
                HeroHeading = "Launch faster",
                NavLinks =
                [
                    new NavLinkInfo { Label = "Docs", Url = "/docs/" }
                ],
                ExtraSections =
                [
                    new SectionInfo
                    {
                        Heading = "Hero",
                        ContentHtml = "<p>Hello</p>"
                    }
                ]
            },
            brand: "Acme",
            behaviors: new CloneBehaviors
            {
                DarkModeToggle = true,
                MobileHamburger = true
            },
            templateScope: TemplateScope.Full,
            includePageTemplate: true);

        File.WriteAllText(Path.Combine(_rootDir, "themes", "starter", "theme.yaml"), """
name: starter
version: 1.0.0
description: Generated starter
tokens: tokens.yaml
assets:
  css:
    - assets/style.css
  js:
    - assets/behaviors.js
page_templates:
  home:
    template: pages/index.html
  page:
    template: pages/page.html
sections:
  hero:
    template: partials/section-1.html
    description: Hero section
components:
  badge:
    template: partials/dropdown.html
    props:
      text: string
capabilities:
  dark_mode: true
  search: true
""");

        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), """
site:
  name: demo
theme:
  name: starter
""");

        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var listResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.ListAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["list"])));
        Assert.Equal(0, listResult.ExitCode);
        Assert.Contains("starter", listResult.StdOut, StringComparison.Ordinal);

        var infoResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.InfoAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["info", "starter"])));
        Assert.Equal(0, infoResult.ExitCode);
        Assert.Contains("Name:        starter", infoResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("Description: Generated starter", infoResult.StdOut, StringComparison.Ordinal);

        var previewResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.PreviewAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["preview", "starter"])));
        Assert.Equal(0, previewResult.ExitCode);
        Assert.Contains("Theme preview: starter", previewResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("Layout templates", previewResult.StdOut, StringComparison.Ordinal);

        var paramsResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.ParamsAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["params", "starter"])));
        Assert.Equal(0, paramsResult.ExitCode);
        Assert.Contains("No parameters declared in theme 'starter'.", paramsResult.StdOut, StringComparison.Ordinal);

        var componentsResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.ListComponentsAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));
        Assert.Equal(0, componentsResult.ExitCode);
        Assert.Contains("Sections:", componentsResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("Components:", componentsResult.StdOut, StringComparison.Ordinal);

        var exportResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.ExportCatalogAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));
        Assert.Equal(0, exportResult.ExitCode);
        Assert.True(File.Exists(Path.Combine(_rootDir, ".cache", "theme-catalog.json")));

        var doctorResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.DoctorAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));
        Assert.Equal(0, doctorResult.ExitCode);
        Assert.Contains("Doctor", doctorResult.StdOut, StringComparison.OrdinalIgnoreCase);

        var useResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeCommand.UseAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["use", "starter"])));
        Assert.Equal(0, useResult.ExitCode);
        Assert.Contains("Theme set: starter", useResult.StdOut, StringComparison.Ordinal);
    }
}
