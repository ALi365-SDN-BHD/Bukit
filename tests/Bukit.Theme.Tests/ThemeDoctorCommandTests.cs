using Bukit.Theme;
using Xunit;

namespace Bukit.Theme.Tests;

[CollectionDefinition("Theme doctor console", DisableParallelization = true)]
public sealed class ThemeDoctorConsoleCollection
{
}

[Collection("Theme doctor console")]
public sealed class ThemeDoctorCommandTests : IDisposable
{
    private readonly string _testDir;

    public ThemeDoctorCommandTests()
    {
        _testDir = Path.Combine(
            Path.GetTempPath(),
            "bukit-theme-doctor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void Diagnose_CleanTheme_ReturnsExactCleanResult()
    {
        ThemeManifestV2 manifest = CreateCleanTheme();
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        ThemeDoctorCommand.DoctorResult result =
            ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Equal(
            [
                "✓ theme.yaml exists",
                "✓ pageTemplate 'home' OK"
            ],
            result.Issues);
    }

    [Fact]
    public void Diagnose_MissingThemeYaml_SetsOnlyErrorFlag()
    {
        ThemeManifestV2 manifest = CreateCleanTheme();
        File.Delete(Path.Combine(_testDir, "theme.yaml"));
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        ThemeDoctorCommand.DoctorResult result =
            ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.True(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Equal(
            [
                "✗ theme.yaml not found",
                "✓ pageTemplate 'home' OK"
            ],
            result.Issues);
    }

    [Fact]
    public void Diagnose_MissingAsset_SetsWarningFlagForWarningGlyph()
    {
        ThemeManifestV2 manifest = CreateCleanTheme();
        manifest.Assets.Css = ["missing.css"];
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        ThemeDoctorCommand.DoctorResult result =
            ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.False(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.Equal(
            [
                "✓ theme.yaml exists",
                "✓ pageTemplate 'home' OK",
                "⚠ asset CSS not found: missing.css"
            ],
            result.Issues);
    }

    [Fact]
    public void Diagnose_MissingOptionalDefinitions_SetsWarningFlagForNoteGlyph()
    {
        File.WriteAllText(
            Path.Combine(_testDir, "theme.yaml"),
            "name: test");
        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0"
        };
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        ThemeDoctorCommand.DoctorResult result =
            ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.False(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.Equal(
            [
                "✓ theme.yaml exists",
                "◌ No pageTemplates defined in theme.yaml",
                "◌ No sections defined in theme.yaml"
            ],
            result.Issues);
    }

    [Fact]
    public void Diagnose_MultipleStages_PreservesIssueOrder()
    {
        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0",
            Components = new Dictionary<string, ThemeComponentDefinition>(
                StringComparer.Ordinal)
            {
                ["Hero"] = new(),
                ["hero"] = new()
            },
            Assets = new ThemeAssetsConfig
            {
                Css = ["missing.css"]
            },
            Extends = "missing-parent",
            Tokens = "missing-tokens.json"
        };
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        ThemeDoctorCommand.DoctorResult result =
            ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        string parentRoot = Path.Combine(
            Path.GetDirectoryName(_testDir)!,
            manifest.Extends);
        Assert.True(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.Equal(
            [
                "✗ theme.yaml not found",
                "◌ No pageTemplates defined in theme.yaml",
                "◌ No sections defined in theme.yaml",
                "✗ component 'hero': duplicate name",
                "⚠ asset CSS not found: missing.css",
                $"✗ extends: parent theme 'missing-parent' not found at '{parentRoot}'",
                $"⚠ tokens file not found: {Path.Combine(_testDir, manifest.Tokens!)}",
                "◌ Unused component detection: not yet implemented"
            ],
            result.Issues);
    }

    [Fact]
    public void DoctorResult_PreservesMutableListAndRecordReferenceSemantics()
    {
        var issues = new List<string> { "✓ initial" };
        var result = new ThemeDoctorCommand.DoctorResult(
            HasErrors: false,
            HasWarnings: false,
            Issues: issues);

        issues.Add("⚠ appended");
        ThemeDoctorCommand.DoctorResult clone = result with { };
        var equalContents = new ThemeDoctorCommand.DoctorResult(
            HasErrors: false,
            HasWarnings: false,
            Issues: [.. issues]);

        Assert.Same(issues, result.Issues);
        Assert.Equal(["✓ initial", "⚠ appended"], result.Issues);
        Assert.Same(result.Issues, clone.Issues);
        Assert.Equal(result, clone);
        Assert.NotEqual(result, equalContents);
    }

    [Fact]
    public void PrintReport_WritesExactTextAndPrioritizesErrors()
    {
        var result = new ThemeDoctorCommand.DoctorResult(
            HasErrors: true,
            HasWarnings: true,
            Issues:
            [
                "✓ clean",
                "⚠ warning",
                "✗ error"
            ]);
        using var writer = new StringWriter();
        TextWriter originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            ThemeDoctorCommand.PrintReport(result);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        string expected = string.Join(
            Environment.NewLine,
            [
                "",
                "═══ Theme Doctor Report ═══",
                "",
                "  ✓ clean",
                "  ⚠ warning",
                "  ✗ error",
                "",
                "Summary: ERRORS FOUND",
                ""
            ]);
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void Diagnose_ExtendsParentTheme_ChecksExistence()
    {
        string parentDir = Path.Combine(
            _testDir,
            "..",
            "parent-theme-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        try
        {
            File.WriteAllText(
                Path.Combine(_testDir, "theme.yaml"),
                "name: test");
            var manifest = new ThemeManifestV2
            {
                Name = "test",
                Version = "1.0",
                Extends = Path.GetFileName(parentDir)
            };
            var registry = new ThemeComponentRegistry(_testDir, manifest);

            ThemeDoctorCommand.DoctorResult result =
                ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Contains("parent theme", StringComparison.Ordinal) &&
                    issue.Contains("found", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(parentDir, recursive: true);
        }
    }

    private ThemeManifestV2 CreateCleanTheme()
    {
        string pagesDir = Path.Combine(_testDir, "layouts", "pages");
        string sectionsDir = Path.Combine(_testDir, "layouts", "sections");
        string schemasDir = Path.Combine(_testDir, "schemas");
        Directory.CreateDirectory(pagesDir);
        Directory.CreateDirectory(sectionsDir);
        Directory.CreateDirectory(schemasDir);
        File.WriteAllText(
            Path.Combine(_testDir, "theme.yaml"),
            "name: test");
        File.WriteAllText(
            Path.Combine(pagesDir, "home.html"),
            "{{ page.title }}");
        File.WriteAllText(
            Path.Combine(sectionsDir, "hero.html"),
            "<section></section>");
        File.WriteAllText(
            Path.Combine(schemasDir, "hero.json"),
            "{}");

        return new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0",
            PageTemplates = new Dictionary<
                string,
                ThemePageTemplateDefinition>
            {
                ["home"] = new()
                {
                    Template = "pages/home.html"
                }
            },
            Sections = new Dictionary<string, ThemeSectionDefinition>
            {
                ["hero"] = new()
                {
                    Template = "sections/hero.html",
                    Schema = "schemas/hero.json"
                }
            }
        };
    }
}
