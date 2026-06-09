using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeDoctorCommandTests : IDisposable
{
    private readonly string _testDir;

    public ThemeDoctorCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-theme-doctor-" + Guid.NewGuid().ToString("N"));
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
    public void Diagnose_EmptyTheme_ReportsMissingThemeYaml()
    {
        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0" };
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        var result = ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Contains("theme.yaml not found"));
    }

    [Fact]
    public void Diagnose_ThemeYamlExists_ReportsSuccess()
    {
        File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), "name: test");
        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0" };
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        var result = ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.Contains(result.Issues, i => i.Contains("theme.yaml exists"));
    }

    [Fact]
    public void Diagnose_MissingAsset_ReportsWarning()
    {
        File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), "name: test");
        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0",
            Assets = new ThemeAssets { Css = ["missing.css"] }
        };
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        var result = ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.Contains(result.Issues, i => i.Contains("missing.css"));
    }

    [Fact]
    public void Diagnose_EmptyManifest_NoErrorsForMissingSections()
    {
        File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), "name: test");
        var manifest = new ThemeManifestV2 { Name = "test", Version = "1.0" };
        var registry = new ThemeComponentRegistry(_testDir, manifest);

        var result = ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

        Assert.DoesNotContain(result.Issues, i => i.StartsWith("✗"));
    }

    [Fact]
    public void Diagnose_ExtendsParentTheme_ChecksExistence()
    {
        var parentDir = Path.Combine(_testDir, "..", "parent-theme-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), "name: test");
            var manifest = new ThemeManifestV2
            {
                Name = "test",
                Version = "1.0",
                Extends = Path.GetFileName(parentDir)
            };
            var registry = new ThemeComponentRegistry(_testDir, manifest);

            var result = ThemeDoctorCommand.Diagnose(_testDir, manifest, registry);

            Assert.Contains(result.Issues, i => i.Contains("parent theme") && i.Contains("found"));
        }
        finally
        {
            Directory.Delete(parentDir, recursive: true);
        }
    }
}
