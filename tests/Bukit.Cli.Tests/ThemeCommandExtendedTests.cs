using System.Reflection;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Bukit.Cli.Tests;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ThemeCommandExtendedTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;

    private static readonly MethodInfo s_isSafeThemeName = typeof(ThemeFileHelper)
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
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[] { "theme", "unknown-cmd" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_ListWithNoThemesDir_ReturnsZero()
    {
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "list", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_UseMissingTheme_ReturnsTwo()
    {
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "use"
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_UseValidTheme_UpdatesSiteYaml()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "my-theme", "layouts"));

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "use", "some-theme", "--config", Path.Combine(_rootDir, "nonexistent.yaml")
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task CreateAsync_WithBrandParam_SetsBrandAndFooterText()
    {
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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

        var copyMethod = typeof(ThemeFileHelper)
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

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
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

    [Fact]
    public void ThemeManifest_Load_ReturnsNullWhenFileDoesNotExist()
    {
        var manifest = ThemeManifest.Load(Path.Combine(_rootDir, "nonexistent"));
        Assert.Null(manifest);
    }

    [Fact]
    public void ThemeManifest_Load_ParsesYamlCorrectly()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "test-manifest");
        Directory.CreateDirectory(themeRoot);
        File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), """
name: test-theme
version: 2.0.0
description: A test theme
author: Tester
license: Apache-2.0
tags: [blog, dark-mode]
requires_bukit: ">=2.0"
params:
  - key: primary_color
    label: Primary
    type: color
    default: "#ff0000"
  - key: show_footer
    label: Show Footer
    type: boolean
    default: "true"
""");

        var manifest = ThemeManifest.Load(themeRoot);
        Assert.NotNull(manifest);
        Assert.Equal("test-theme", manifest!.Name);
        Assert.Equal("2.0.0", manifest.Version);
        Assert.Equal("A test theme", manifest.Description);
        Assert.Equal("Tester", manifest.Author);
        Assert.Equal("Apache-2.0", manifest.License);
        Assert.Equal(2, manifest.Tags.Count);
        Assert.Contains("blog", manifest.Tags);
        Assert.Contains("dark-mode", manifest.Tags);
        Assert.Equal(2, manifest.DeclaredParamCount);
        Assert.Equal(2, manifest.Params.Count);
        Assert.Equal("primary_color", manifest.Params[0].Key);
        Assert.Equal("Primary", manifest.Params[0].Label);
        Assert.Equal("color", manifest.Params[0].Type);
        Assert.Equal("#ff0000", manifest.Params[0].Default);
    }

    [Fact]
    public async Task RunAsync_InfoWithThemeName_ShowsThemeDetails()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "info-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "layouts", "pages", "index.html"), "test");
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
name: info-theme
version: 1.0.0
description: Info command test theme
author: TestAuthor
""");

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "info", "info-theme", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_InfoWithoutName_UsesActiveTheme()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "active-info");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
name: active-info
version: 1.0.0
description: Active info theme
""");

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "use", "active-info", "--config", _configPath
        }));
        Assert.Equal(0, exitCode);

        var infoExitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "info", "--config", _configPath
        }));
        Assert.Equal(0, infoExitCode);
    }

    [Fact]
    public async Task RunAsync_InfoNonExistentTheme_ReturnsTwo()
    {
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "info", "nonexistent", "--config", _configPath
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_ParamsShowsDeclaredParameters()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "params-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
name: params-theme
params:
  - key: bg_color
    label: Background
    type: color
    default: "#ffffff"
  - key: font_size
    label: Font Size
    type: string
    default: "16px"
""");

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "params", "params-theme", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_ParamsNoParamsDeclared_ReturnsZero()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "no-params-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
name: no-params-theme
""");

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "params", "no-params-theme", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void StarterThemeScaffold_GeneratesThemeYaml()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "yaml-check");
        StarterThemeScaffold.WriteTo(_rootDir, "yaml-check", primaryColor: null, accentColor: null);

        var themeYamlPath = Path.Combine(themeRoot, "theme.yaml");
        Assert.True(File.Exists(themeYamlPath));

        var yaml = File.ReadAllText(themeYamlPath);
        Assert.Contains("name: starter", yaml, StringComparison.Ordinal);
        Assert.Contains("version: 1.0.0", yaml, StringComparison.Ordinal);
        Assert.Contains("tags:", yaml, StringComparison.Ordinal);
        Assert.Contains("params:", yaml, StringComparison.Ordinal);
        Assert.Contains("primary_color", yaml, StringComparison.Ordinal);
        Assert.Contains("accent_color", yaml, StringComparison.Ordinal);

        var manifest = ThemeManifest.Load(themeRoot);
        Assert.NotNull(manifest);
        Assert.Equal("starter", manifest!.Name);
        Assert.True(manifest.Params.Count >= 4);
    }

    [Fact]
    public async Task RunAsync_ListWithThemeYaml_ShowsMetadata()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "rich-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
name: rich-theme
version: 3.0.0
description: A rich theme with metadata
author: Dev
tags: [blog, dark-mode, responsive]
""");

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "list", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_WizardRequiresName()
    {
        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "wizard", "--config", _configPath
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_WizardReturnsTwoWhenThemeExistsWithoutForce()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "wiz-existing");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "wizard", "wiz-existing", "--config", _configPath
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void TemplateSnippets_HasAllScribanSnippets()
    {
        Assert.True(TemplateSnippets.ScribanSnippets.Count >= 8);
        Assert.Contains("post-card", TemplateSnippets.ScribanSnippets.Keys);
        Assert.Contains("tag-cloud", TemplateSnippets.ScribanSnippets.Keys);
        Assert.Contains("breadcrumb", TemplateSnippets.ScribanSnippets.Keys);
    }

    [Fact]
    public void TemplateSnippets_HasAllCssSnippets()
    {
        Assert.True(TemplateSnippets.CssSnippets.Count >= 9);
        Assert.Contains("btn", TemplateSnippets.CssSnippets.Keys);
        Assert.Contains("callout", TemplateSnippets.CssSnippets.Keys);
        Assert.Contains("code-block", TemplateSnippets.CssSnippets.Keys);
    }

    [Fact]
    public async Task TemplateCommand_List_NoTemplatesDir_ReturnsZero()
    {
        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "list", "--config", _configPath
        }));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TemplateCommand_Show_MissingName_ReturnsTwo()
    {
        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "show", "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task TemplateCommand_Show_PathTraversal_ReturnsTwo()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "safe-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), "name: safe-theme");
        await File.WriteAllTextAsync(Path.Combine(_rootDir, "outside.html"), "outside");

        await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "use", "safe-theme", "--config", _configPath
        }));

        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "show", "../../outside.html", "--config", _configPath
        }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task TemplateCommand_Create_PathTraversal_ReturnsTwoAndDoesNotWriteOutsideLayouts()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "safe-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), "name: safe-theme");

        await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "use", "safe-theme", "--config", _configPath
        }));

        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "create", "../../outside.html", "--config", _configPath
        }));

        Assert.Equal(2, exitCode);
        Assert.False(File.Exists(Path.Combine(_rootDir, "outside.html")));
    }

    [Fact]
    public async Task TemplateCommand_Validate_ActiveTheme()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "validate-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "layouts", "pages", "index.html"),
            "{% layout \"layouts/base.html\" %}\n<h1>{{ page.title }}</h1>");
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
name: validate-theme
""");

        await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "use", "validate-theme", "--config", _configPath
        }));

        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "validate", "--config", _configPath
        }));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TemplateCommand_Snippets_ListAll()
    {
        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "snippets"
        }));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TemplateCommand_Snippets_ShowOne()
    {
        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "snippets", "post-card"
        }));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ThemePack_RequiresName()
    {
        var exitCode = await ThemePackCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "pack", "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ThemePack_NonExistentTheme_ReturnsTwo()
    {
        var exitCode = await ThemePackCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "pack", "nonexistent", "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ThemePack_PacksTheme()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "packable");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
name: packable
version: 1.0.0
""");
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "layouts", "pages", "index.html"), "test");

        var exitCode = await ThemePackCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "pack", "packable", "--config", _configPath
        }));
        Assert.Equal(0, exitCode);

        var outputFile = Path.Combine(Directory.GetCurrentDirectory(), "packable-1.0.0.tar.gz");
        Assert.True(File.Exists(outputFile));
        try { File.Delete(outputFile); } catch { }
    }

    [Fact]
    public async Task ThemeInstall_MissingSource_ReturnsTwo()
    {
        var exitCode = await ThemeInstallCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "install", "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ThemeInstall_NonexistentFile_ReturnsTwo()
    {
        var exitCode = await ThemeInstallCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "install", "/nonexistent/path.tar.gz", "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ThemeInstall_InvalidArchive_ReturnsTwo()
    {
        var badArchive = Path.Combine(_rootDir, "bad.tar.gz");
        await File.WriteAllTextAsync(badArchive, "not a real archive");

        var exitCode = await ThemeInstallCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "install", badArchive, "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ThemeInstall_UnsafeManifestName_ReturnsTwoAndDoesNotWriteOutsideThemes()
    {
        var archive = Path.Combine(_rootDir, "unsafe.tar.gz");
        await CreateThemeArchiveAsync(archive, "../escaped");

        var exitCode = await ThemeInstallCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "install", archive, "--config", _configPath
        }));

        Assert.Equal(2, exitCode);
        Assert.False(Directory.Exists(Path.Combine(_rootDir, "escaped")));
        Assert.False(Directory.Exists(Path.Combine(_rootDir, "themes", "escaped")));
    }

    [Fact]
    public async Task TemplateCommand_Hints_ReturnsZero()
    {
        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[] { "template", "hints" }));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TemplateCommand_Sync_NoActiveTheme_ReturnsTwo()
    {
        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "sync", "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task TemplateCommand_Sync_GeneratesManifest()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "sync-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "layouts", "pages", "index.html"),
            "{% layout \"layouts/base.html\" %}\n<h1>{{ page.title }}</h1>");
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "layouts", "pages", "list.html"),
            "{% layout \"layouts/base.html\" %}\n{{ for p in pages }}<a href=\"{{ p.url }}\">{{ p.title }}</a>{{ end }}");
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), "name: sync-theme");

        await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "use", "sync-theme", "--config", _configPath
        }));

        var exitCode = await TemplateCommand.RunAsync(CliTestHelper.CreateCommand("template", new[]
        {
            "template", "sync", "--config", _configPath
        }));
        Assert.Equal(0, exitCode);

        var manifestPath = Path.Combine(themeRoot, "layouts", "bukit.templates.yaml");
        Assert.True(File.Exists(manifestPath));
        var yaml = await File.ReadAllTextAsync(manifestPath);
        Assert.Contains("pages/index.html:", yaml, StringComparison.Ordinal);
        Assert.Contains("pages/list.html:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeTemplateResource_Get_ReturnsContent()
    {
        var css = ThemeTemplateResource.Get("StyleCss");
        Assert.Contains(":root", css, StringComparison.Ordinal);
        Assert.Contains("--primary", css, StringComparison.Ordinal);

        var layout = ThemeTemplateResource.Get("BaseLayout");
        Assert.Contains("<!DOCTYPE html>", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeTemplateResource_ProcessPlaceholders_ReplacesMarkers()
    {
        var input = "Hello {{-- bukit:brand --}}, welcome {{-- bukit:name --}}";
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brand"] = "ACME",
            ["name"] = "World"
        };
        var result = ThemeTemplateResource.ProcessPlaceholders(input, replacements);
        Assert.Equal("Hello ACME, welcome World", result);
    }

    [Fact]
    public void ThemeTemplateResource_ProcessPlaceholders_NoReplacements_ReturnsOriginal()
    {
        var input = "Hello {{-- bukit:brand --}}";
        var result = ThemeTemplateResource.ProcessPlaceholders(input, null!);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ThemeTemplateResource_ApplyColorOverrides_Works()
    {
        var css = ":root { --primary: #0b5fff; --accent: #0f7b6c; }";
        var result = ThemeTemplateResource.ApplyColorOverrides(css, "#ff0000", "#00ff00");
        Assert.Contains("--primary: #ff0000;", result, StringComparison.Ordinal);
        Assert.Contains("--accent: #00ff00;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterThemeScaffold_UsesResourceHelper()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "resource-test");
        StarterThemeScaffold.WriteTo(_rootDir, "resource-test", primaryColor: null, accentColor: null);

        var baseLayout = File.ReadAllText(Path.Combine(themeRoot, "layouts", "layouts", "base.html"));
        Assert.Contains("<!DOCTYPE html>", baseLayout, StringComparison.Ordinal);

        var styleCss = File.ReadAllText(Path.Combine(themeRoot, "assets", "style.css"));
        Assert.Contains("--primary", styleCss, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryIndex_Parse_HandlesValidYaml()
    {
        var yaml = """
registry:
  updated: "2026-01-01T00:00:00Z"
themes:
  - name: test-theme
    version: 1.0.0
    description: A test
    author: Tester
    tags: [blog]
    download:
      url: https://example.com/test.tar.gz
      sha256: abc123
""";
        var index = RegistryIndex.Parse(yaml);
        Assert.NotNull(index);
        Assert.Single(index!.Themes);
        Assert.Equal("test-theme", index.Themes[0].Name);
        Assert.Equal("1.0.0", index.Themes[0].Version);
        Assert.Equal("abc123", index.Themes[0].Download?.Sha256);
    }

    [Fact]
    public void RegistryIndex_Parse_HandlesEmptyYaml()
    {
        var index = RegistryIndex.Parse("");
        Assert.Null(index);
    }

    [Fact]
    public async Task ThemeInstall_RegistryUnknownTheme_ReturnsTwo()
    {
        var exitCode = await ThemeInstallCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "install", "--registry", "totally-fake-theme-xyz",
            "--config", _configPath,
            "--registry-url", "https://invalid.url/registry.yaml"
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ThemeSearch_RoutesFromThemeCommand()
    {
        var testCacheFile = ThemeRegistryCommand.CacheFilePath;
        try { File.Delete(testCacheFile); } catch { }

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "search", "--config", _configPath
        }));
        Assert.True(exitCode is 0 or 1);
    }

    [Fact]
    public async Task ThemeRegistryCommand_VerifySha256_EmptyExpected_ReturnsTrue()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "data");
            var ok = await ThemeRegistryCommand.VerifySha256Async(path, "");
            Assert.True(ok);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public async Task ThemePreview_PrintsPreviewMetadata()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "previewable");
        Directory.CreateDirectory(themeRoot);
        await File.WriteAllTextAsync(Path.Combine(themeRoot, "theme.yaml"), """
            name: previewable
            version: 1.2.3
            description: Preview theme
            homepage: https://example.com/theme
            thumbnail: https://example.com/theme.png
            tags: [blog, docs]
            """);

        var exitCode = await ThemeCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "preview", "previewable", "--config", _configPath
        }));

        Assert.Equal(0, exitCode);
    }

    private static async Task CreateThemeArchiveAsync(string archivePath, string themeName)
    {
        await using var file = File.Create(archivePath);
        await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        await using var writer = new TarWriter(gzip, leaveOpen: false);

        await WriteTextEntryAsync(writer, "theme.yaml", $"name: {themeName}\nversion: 1.0.0\n");
        await WriteTextEntryAsync(writer, "layouts/pages/index.html", "index");
    }

    private static async Task WriteTextEntryAsync(TarWriter writer, string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await using var stream = new MemoryStream(bytes);
        var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
        {
            DataStream = stream
        };
        await writer.WriteEntryAsync(entry);
    }

    [Fact]
    public void WizardPresets_AllFiveDefined()
    {
        Assert.Equal(5, WizardPreset.All.Count);
        Assert.Contains(WizardPreset.All, p => p.Name == "blog");
        Assert.Contains(WizardPreset.All, p => p.Name == "docs");
        Assert.Contains(WizardPreset.All, p => p.Name == "landing");
        Assert.Contains(WizardPreset.All, p => p.Name == "minimal");
        Assert.Contains(WizardPreset.All, p => p.Name == "portfolio");
    }

    [Fact]
    public void WizardPreset_Blog_HasCorrectDefaults()
    {
        var blog = WizardPreset.Blog;
        Assert.Equal("blog", blog.Name);
        Assert.NotNull(blog.Tokens.Primary);
        Assert.NotNull(blog.Tokens.Accent);
        Assert.Equal(3, blog.Layout.NavLinks.Count);
        Assert.True(blog.Behaviors.DarkModeToggle);
        Assert.False(blog.Behaviors.StickyHeader);
    }

    [Fact]
    public void WizardPreset_Landing_HasHeroAndCta()
    {
        var landing = WizardPreset.Landing;
        Assert.True(landing.Layout.HasHeroCta);
        Assert.True(landing.Layout.HasFeaturesSection);
        Assert.True(landing.Layout.HasCTASection);
        Assert.True(landing.Behaviors.AnimateOnScroll);
    }

    [Fact]
    public async Task ThemeWizard_UnknownPreset_ReturnsTwo()
    {
        var exitCode = await ThemeWizardCommand.RunAsync(CliTestHelper.CreateCommand("theme", new[]
        {
            "theme", "wizard", "test-preset", "--preset", "unknown-preset-xyz",
            "--config", _configPath
        }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void StarterThemeScaffold_FooterHasBrandPlaceholder()
    {
        var footer = ThemeTemplateResource.Get("FooterPartial");
        Assert.Contains("Powered by", footer, StringComparison.Ordinal);
        Assert.Contains("bukit", footer, StringComparison.Ordinal);
    }
}
