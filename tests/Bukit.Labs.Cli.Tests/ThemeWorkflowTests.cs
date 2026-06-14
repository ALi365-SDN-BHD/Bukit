using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class ThemeWorkflowTests : IDisposable
{
    private readonly string _rootDir;

    public ThemeWorkflowTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-theme-workflows-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task InitCommand_RunAsync_BlogTemplate_WritesStarterScaffold()
    {
        var targetDir = Path.Combine(_rootDir, "blog-site");

        var result = await CommandTestSupport.CaptureAsync(() =>
            InitCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--template"] = "blog" },
                [targetDir])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Template: blog", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(targetDir, "site.yaml")));
        Assert.True(File.Exists(Path.Combine(targetDir, "content", "posts", "welcome.md")));
        Assert.True(File.Exists(Path.Combine(targetDir, "content", "pages", "about.md")));
        Assert.True(File.Exists(Path.Combine(targetDir, "data", "features-10.md")));
        Assert.True(File.Exists(Path.Combine(targetDir, "themes", "starter", "assets", "og-default.gif")));

        var siteYaml = File.ReadAllText(Path.Combine(targetDir, "site.yaml"));
        Assert.Contains("latest_heading: Latest posts", siteYaml, StringComparison.Ordinal);
        Assert.Contains("collection: post", File.ReadAllText(Path.Combine(targetDir, "content", "posts", "welcome.md")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitCommand_RunAsync_NoneTemplate_SkipsSiteYaml()
    {
        var targetDir = Path.Combine(_rootDir, "empty-site");

        var result = await CommandTestSupport.CaptureAsync(() =>
            InitCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--template"] = "none" },
                [targetDir])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Template: none", result.StdOut, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(targetDir, "site.yaml")));
        Assert.True(File.Exists(Path.Combine(targetDir, "README.md")));
        Assert.True(Directory.Exists(Path.Combine(targetDir, "themes", "starter", "layouts", "partials")));
    }

    [Fact]
    public async Task TemplateCommand_Workflow_CreatesShowsValidatesAndSyncsTemplates()
    {
        CreateGeneratedTheme("starter");
        WriteSiteConfig("starter");

        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);
        using (var input = new CommandTestSupport.ConsoleInputScope("1\n\n\n\ny\n"))
        {
            var createResult = await CommandTestSupport.CaptureAsync(() =>
                TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["create", "pages/article.html"])));

            Assert.Equal(0, createResult.ExitCode);
            Assert.Contains("Created: themes/starter/layouts/pages/article.html", createResult.StdOut, StringComparison.Ordinal);
        }

        var templatePath = Path.Combine(_rootDir, "themes", "starter", "layouts", "pages", "article.html");
        Assert.True(File.Exists(templatePath));
        Assert.Contains("{{ page.title }}", File.ReadAllText(templatePath), StringComparison.Ordinal);

        var listResult = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["list"])));
        Assert.Equal(0, listResult.ExitCode);
        Assert.Contains("[pages/]", listResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("article.html", listResult.StdOut, StringComparison.Ordinal);

        var showResult = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["show", "pages/article.html"])));
        Assert.Equal(0, showResult.ExitCode);
        Assert.Contains("=== themes/starter/layouts/pages/article.html ===", showResult.StdOut, StringComparison.Ordinal);

        var validateOk = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["validate"])));
        Assert.Equal(0, validateOk.ExitCode);
        Assert.Contains("Validated:", validateOk.StdOut, StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(_rootDir, "themes", "starter", "layouts", "broken.html"), "{{ if ");
        var validateBroken = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["validate"])));
        Assert.Equal(1, validateBroken.ExitCode);
        Assert.Contains("errors in", validateBroken.StdOut, StringComparison.Ordinal);

        var syncResult = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--force"] = "" },
                ["sync"])));
        Assert.Equal(0, syncResult.ExitCode);
        var manifestPath = Path.Combine(_rootDir, "themes", "starter", "layouts", "bukit.templates.yaml");
        Assert.True(File.Exists(manifestPath));
        Assert.Contains("pages/article.html:", File.ReadAllText(manifestPath), StringComparison.Ordinal);

        var snippetsResult = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["snippets", "post-card"])));
        Assert.Equal(0, snippetsResult.ExitCode);
        Assert.Contains("=== Scriban snippet: post-card ===", snippetsResult.StdOut, StringComparison.Ordinal);
        Assert.Contains("=== CSS snippet: post-card ===", snippetsResult.StdOut, StringComparison.Ordinal);

        var hintsResult = await CommandTestSupport.CaptureAsync(() =>
            TemplateCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["hints"])));
        Assert.Equal(0, hintsResult.ExitCode);
        Assert.Contains("site.name", hintsResult.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeWizardCommand_RunAsync_WithPresetAndUse_CreatesThemeAndUpdatesConfig()
    {
        WriteMinimalSiteConfig();

        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);
        using var input = new CommandTestSupport.ConsoleInputScope("\n\n\n\n\n\n\n");

        var result = await CommandTestSupport.CaptureAsync(() =>
            ThemeWizardCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--preset"] = "blog", ["--use"] = "" },
                ["wizard", "brand-kit"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Preset: blog", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Theme set: brand-kit", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "brand-kit", "theme.yaml")));

        var siteYaml = File.ReadAllText(Path.Combine(_rootDir, "site.yaml"));
        Assert.Contains("name: brand-kit", siteYaml, StringComparison.Ordinal);
        Assert.Contains("brand: brand-kit", siteYaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemePackAndInstallCommands_RoundTripArchive()
    {
        CreateGeneratedTheme("starter");
        WriteSiteConfig("starter");

        var archivePath = Path.Combine(_rootDir, "starter-export.tar.gz");

        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var packResult = await CommandTestSupport.CaptureAsync(() =>
            ThemePackCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--output"] = archivePath },
                ["pack"])));

        Assert.Equal(0, packResult.ExitCode);
        Assert.True(File.Exists(archivePath));

        TestCleanup.DeleteDirectory(Path.Combine(_rootDir, "themes", "starter"), recursive: true);

        var installResult = await CommandTestSupport.CaptureAsync(() =>
            ThemeInstallCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["install", archivePath])));

        Assert.Equal(0, installResult.ExitCode);
        Assert.Contains("Theme installed: starter", installResult.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "starter", "theme.yaml")));
    }

    [Fact]
    public async Task VisualCommand_RunAsync_GeneratesPlaywrightScript()
    {
        WriteMinimalSiteConfig();

        var distDir = Path.Combine(_rootDir, "dist", "blog");
        Directory.CreateDirectory(distDir);
        File.WriteAllText(Path.Combine(_rootDir, "dist", "index.html"), "<html><body>Home</body></html>");
        File.WriteAllText(Path.Combine(distDir, "index.html"), "<html><body>Blog</body></html>");

        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            VisualCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--out"] = "visual-tests.spec.js" },
                ["generate"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Generated visual-tests.spec.js with 2 page(s) to test.", result.StdOut, StringComparison.Ordinal);

        var script = File.ReadAllText(Path.Combine(_rootDir, "visual-tests.spec.js"));
        Assert.Contains("test('/', async", script, StringComparison.Ordinal);
        Assert.Contains("test('/blog/', async", script, StringComparison.Ordinal);
        Assert.Contains("toHaveScreenshot('home.png'", script, StringComparison.Ordinal);
    }

    private void CreateGeneratedTheme(string themeName)
    {
        CloneThemeGenerator.WriteTo(
            _rootDir,
            themeName,
            CloneTokens.Default,
            new CloneLayoutInfo
            {
                SiteTitle = "Acme Docs",
                HeroHeading = "Ship better",
                NavLinks =
                [
                    new NavLinkInfo { Label = "Docs", Url = "/docs/" }
                ],
                ExtraSections =
                [
                    new SectionInfo
                    {
                        Heading = "Highlights",
                        ContentHtml = "<p>Useful details</p>"
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
    }

    private void WriteSiteConfig(string themeName)
    {
        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), $"""
site:
  name: demo
  title: Demo
theme:
  name: {themeName}
""");
    }

    private void WriteMinimalSiteConfig()
    {
        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), """
site:
  name: demo
  title: Demo
theme:
  name: starter
""");
    }
}
