using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class ThemeInstallCommandTests : IDisposable
{
    private readonly string _rootDir;

    private static readonly MethodInfo s_resolveThemeDest = typeof(ThemeInstallCommand)
        .GetMethod("ResolveThemeDestination", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_detectThemeName = typeof(ThemeInstallCommand)
        .GetMethod("DetectThemeName", BindingFlags.NonPublic | BindingFlags.Static)!;

    public ThemeInstallCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-theme-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            TestCleanup.DeleteDirectory(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_FileNotFound_ReturnsTwo()
    {
        var command = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", "/nonexistent/path/file.tar.gz" });
        var result = await ThemeInstallCommand.RunAsync(command);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task RunAsync_InvalidArchive_ReturnsTwo()
    {
        var invalidFile = Path.Combine(_rootDir, "invalid.tar.gz");
        File.WriteAllText(invalidFile, "not a valid archive content");
        SetupSiteYaml();

        var command = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", invalidFile, "--config", GetSiteYamlPath() });
        var result = await ThemeInstallCommand.RunAsync(command);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task RunAsync_ValidArchive_InstallsSuccessfully()
    {
        var archivePath = CreateValidThemeArchive("valid-theme");
        SetupSiteYaml();

        var command = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        var result = await ThemeInstallCommand.RunAsync(command);

        Assert.Equal(0, result);
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "themes", "valid-theme", "layouts")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "valid-theme", "layouts", "default.scriban")));
    }

    [Fact]
    public async Task RunAsync_ExistingWithoutForce_ReturnsTwo()
    {
        var archivePath = CreateValidThemeArchive("conflict-theme");
        SetupSiteYaml();

        var commandFirst = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        Assert.Equal(0, await ThemeInstallCommand.RunAsync(commandFirst));

        var commandSecond = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        Assert.Equal(2, await ThemeInstallCommand.RunAsync(commandSecond));
    }

    [Fact]
    public async Task RunAsync_ExistingWithForce_Overwrites()
    {
        var archivePath = CreateValidThemeArchive("force-overwrite-theme");
        SetupSiteYaml();

        var commandFirst = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        Assert.Equal(0, await ThemeInstallCommand.RunAsync(commandFirst));

        var commandSecond = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", archivePath, "--force", "--config", GetSiteYamlPath() });
        Assert.Equal(0, await ThemeInstallCommand.RunAsync(commandSecond));
    }

    [Fact]
    public async Task InstallFromArchive_NoSourceFile_ReturnsTwo()
    {
        SetupSiteYaml();

        var source = Path.Combine(_rootDir, "nope.tar.gz");
        var command = CliTestHelper.CreateCommand("theme", new[] { "theme", "install", source, "--config", GetSiteYamlPath() });
        var result = await ThemeInstallCommand.RunAsync(command);

        Assert.Equal(2, result);
    }

    [Fact]
    public void ResolveThemeDestination_SafeName_ReturnsWithinThemesDir()
    {
        var themesDir = Path.Combine(_rootDir, "themes");

        var result = (string)s_resolveThemeDest.Invoke(null, new object[] { themesDir, "mysafe" })!;

        Assert.Equal(Path.Combine(themesDir, "mysafe"), result);
    }

    [Fact]
    public void ResolveThemeDestination_PathTraversal_Throws()
    {
        var themesDir = Path.Combine(_rootDir, "themes");

        Assert.Throws<TargetInvocationException>(() =>
            s_resolveThemeDest.Invoke(null, new object[] { themesDir, "../evil" }));
    }

    [Fact]
    public void DetectThemeName_ThemeYaml_ReturnsManifestName()
    {
        var dir = Path.Combine(_rootDir, "extract-test");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.yaml"), "name: my-theme\nversion: 1.0.0\n");

        var result = (string?)s_detectThemeName.Invoke(null, new object[] { dir });

        Assert.Equal("my-theme", result);
    }

    [Fact]
    public void DetectThemeName_SingleSubdir_ReturnsDirName()
    {
        var dir = Path.Combine(_rootDir, "extract-test2");
        Directory.CreateDirectory(dir);
        var inner = Path.Combine(dir, "my-inner-theme");
        Directory.CreateDirectory(inner);
        Directory.CreateDirectory(Path.Combine(inner, "layouts"));

        var result = (string?)s_detectThemeName.Invoke(null, new object[] { dir });

        Assert.Equal("my-inner-theme", result);
    }

    private string CreateValidThemeArchive(string themeName)
    {
        var tmpDir = Path.Combine(_rootDir, "tmp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        Directory.CreateDirectory(Path.Combine(tmpDir, "layouts"));
        File.WriteAllText(Path.Combine(tmpDir, "theme.yaml"), $"name: {themeName}\nversion: 1.0.0\n");
        File.WriteAllText(Path.Combine(tmpDir, "layouts", "default.scriban"), "<html></html>");

        var archivePath = Path.Combine(_rootDir, $"{themeName}.tar.gz");
        using var fs = File.Create(archivePath);
        using var gzip = new GZipStream(fs, CompressionLevel.Optimal);
        using var writer = new TarWriter(gzip);
        writer.WriteEntryAsync(Path.Combine(tmpDir, "theme.yaml"), "theme.yaml").Wait();
        writer.WriteEntryAsync(Path.Combine(tmpDir, "layouts", "default.scriban"), "layouts/default.scriban").Wait();

        TestCleanup.DeleteDirectory(tmpDir, recursive: true);
        return archivePath;
    }

    private void SetupSiteYaml()
    {
        File.WriteAllText(GetSiteYamlPath(), """
site:
  name: test
  title: Test
content:
  provider: markdown
""");
    }

    private string GetSiteYamlPath() => Path.Combine(_rootDir, "site.yaml");
}
