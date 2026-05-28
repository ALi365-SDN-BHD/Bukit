using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using Bukit.Cli.Commands;
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

    private static readonly MethodInfo s_installFromArchive = typeof(ThemeInstallCommand)
        .GetMethod("InstallFromArchiveAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

    public ThemeInstallCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-theme-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            try { Directory.Delete(_rootDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_FileNotFound_ReturnsTwo()
    {
        var reader = new ArgReader(new[] { "theme", "install", "/nonexistent/path/file.tar.gz" });
        var result = await ThemeInstallCommand.RunAsync(reader);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task RunAsync_InvalidArchive_ReturnsTwo()
    {
        var invalidFile = Path.Combine(_rootDir, "invalid.tar.gz");
        File.WriteAllText(invalidFile, "not a valid archive content");
        SetupSiteYaml();

        var reader = new ArgReader(new[] { "theme", "install", invalidFile, "--config", GetSiteYamlPath() });
        var result = await ThemeInstallCommand.RunAsync(reader);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task RunAsync_ValidArchive_InstallsSuccessfully()
    {
        var archivePath = CreateValidThemeArchive("valid-theme");
        SetupSiteYaml();

        var reader = new ArgReader(new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        var result = await ThemeInstallCommand.RunAsync(reader);

        Assert.Equal(0, result);
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "themes", "valid-theme", "layouts")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "valid-theme", "layouts", "default.scriban")));
    }

    [Fact]
    public async Task RunAsync_ExistingWithoutForce_ReturnsTwo()
    {
        var archivePath = CreateValidThemeArchive("conflict-theme");
        SetupSiteYaml();

        var readerFirst = new ArgReader(new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        Assert.Equal(0, await ThemeInstallCommand.RunAsync(readerFirst));

        var readerSecond = new ArgReader(new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        Assert.Equal(2, await ThemeInstallCommand.RunAsync(readerSecond));
    }

    [Fact]
    public async Task RunAsync_ExistingWithForce_Overwrites()
    {
        var archivePath = CreateValidThemeArchive("force-overwrite-theme");
        SetupSiteYaml();

        var readerFirst = new ArgReader(new[] { "theme", "install", archivePath, "--config", GetSiteYamlPath() });
        Assert.Equal(0, await ThemeInstallCommand.RunAsync(readerFirst));

        var readerSecond = new ArgReader(new[] { "theme", "install", archivePath, "--force", "--config", GetSiteYamlPath() });
        Assert.Equal(0, await ThemeInstallCommand.RunAsync(readerSecond));
    }

    [Fact]
    public async Task InstallFromArchive_NoSourceFile_ReturnsTwo()
    {
        var result = await InvokeInstallFromArchive("/nonexistent/path", _rootDir, false);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task InstallFromArchive_InvalidArchive_ReturnsTwo()
    {
        var fakeArchive = Path.Combine(_rootDir, "fake.tar.gz");
        File.WriteAllText(fakeArchive, "not a gzip");

        var result = await InvokeInstallFromArchive(fakeArchive, _rootDir, false);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task InstallFromArchive_ValidArchive_InstallsSuccessfully()
    {
        var archivePath = CreateValidThemeArchive("my-theme");

        var result = await InvokeInstallFromArchive(archivePath, _rootDir, false);

        Assert.Equal(0, result);
        Assert.True(Directory.Exists(Path.Combine(_rootDir, "my-theme", "layouts")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "my-theme", "layouts", "default.scriban")));
    }

    [Fact]
    public async Task InstallFromArchive_ExistingWithoutForce_ReturnsTwo()
    {
        var archivePath = CreateValidThemeArchive("existing-theme");

        Assert.Equal(0, await InvokeInstallFromArchive(archivePath, _rootDir, false));
        Assert.Equal(2, await InvokeInstallFromArchive(archivePath, _rootDir, false));
    }

    [Fact]
    public async Task InstallFromArchive_ExistingWithForce_Overwrites()
    {
        var archivePath = CreateValidThemeArchive("force-theme");

        Assert.Equal(0, await InvokeInstallFromArchive(archivePath, _rootDir, false));
        Assert.Equal(0, await InvokeInstallFromArchive(archivePath, _rootDir, true));
    }

    [Fact]
    public void ResolveThemeDestination_SafeName_ReturnsPath()
    {
        var result = InvokeResolveThemeDestination(_rootDir, "my-theme");

        Assert.Equal(Path.GetFullPath(Path.Combine(_rootDir, "my-theme")), result);
    }

    [Fact]
    public void ResolveThemeDestination_PathTraversal_Throws()
    {
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeResolveThemeDestination(_rootDir, "../etc/passwd"));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ResolveThemeDestination_MultipleTraversal_Throws()
    {
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeResolveThemeDestination(_rootDir, "a/../../etc/passwd"));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void DetectThemeName_WithThemeYaml_ReturnsName()
    {
        var dir = Path.Combine(_rootDir, "theme-dir");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theme.yaml"), """
            name: detected-theme
            version: 1.0.0
            """);

        var result = InvokeDetectThemeName(dir);

        Assert.Equal("detected-theme", result);
    }

    [Fact]
    public void DetectThemeName_WithNestedThemeYaml_ReturnsName()
    {
        var dir = Path.Combine(_rootDir, "outer");
        var inner = Path.Combine(dir, "my-theme-files");
        Directory.CreateDirectory(inner);
        File.WriteAllText(Path.Combine(inner, "theme.yaml"), """
            name: nested-theme
            version: 1.0.0
            """);

        var result = InvokeDetectThemeName(dir);

        Assert.Equal("nested-theme", result);
    }

    [Fact]
    public void DetectThemeName_SingleSubdirWithLayouts_ReturnsDirName()
    {
        var dir = Path.Combine(_rootDir, "outer");
        var inner = Path.Combine(dir, "my-theme-files");
        Directory.CreateDirectory(Path.Combine(inner, "layouts"));
        File.WriteAllText(Path.Combine(inner, "layouts", "default.scriban"), "");

        var result = InvokeDetectThemeName(dir);

        Assert.Equal("my-theme-files", result);
    }

    [Fact]
    public void DetectThemeName_SingleSubdirWithoutLayouts_ReturnsNull()
    {
        var dir = Path.Combine(_rootDir, "outer");
        Directory.CreateDirectory(Path.Combine(dir, "src"));

        var result = InvokeDetectThemeName(dir);

        Assert.Null(result);
    }

    [Fact]
    public void DetectThemeName_EmptyDir_ReturnsNull()
    {
        var dir = Path.Combine(_rootDir, "empty");
        Directory.CreateDirectory(dir);

        var result = InvokeDetectThemeName(dir);

        Assert.Null(result);
    }

    [Fact]
    public void DetectThemeName_UnknownDir_ReturnsNull()
    {
        var dir = Path.Combine(_rootDir, "mixed");
        Directory.CreateDirectory(Path.Combine(dir, "a"));
        Directory.CreateDirectory(Path.Combine(dir, "b"));

        var result = InvokeDetectThemeName(dir);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAndInstall_ValidArchive_InstallsWithName()
    {
        var archivePath = CreateValidThemeArchive("extract-theme");
        var methods = typeof(ThemeInstallCommand).GetMethod("ExtractAndInstallAsync",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var task = (Task<int>)methods.Invoke(null,
            new object[] { archivePath, _rootDir, false, "extract-theme" })!;
        var result = await task;

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_rootDir, "extract-theme", "layouts", "default.scriban")));
    }

    private string GetSiteYamlPath()
    {
        var path = Path.Combine(_rootDir, "site.yaml");
        if (!File.Exists(path))
            SetupSiteYaml();
        return path;
    }

    private void SetupSiteYaml()
    {
        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), """
            site:
              name: test
              title: Test
            content:
              provider: markdown
            """);
    }

    private static async Task<int> InvokeInstallFromArchive(string sourcePath, string themesDir, bool force)
    {
        var task = (Task<int>)s_installFromArchive.Invoke(null,
            new object[] { sourcePath, themesDir, force })!;
        return await task;
    }

    private static string InvokeResolveThemeDestination(string themesDir, string themeName)
    {
        return (string)s_resolveThemeDest.Invoke(null, new object[] { themesDir, themeName })!;
    }

    private static string? InvokeDetectThemeName(string dir)
    {
        return (string?)s_detectThemeName.Invoke(null, new object[] { dir })!;
    }

    internal static string CreateValidThemeArchive(string themeName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-archive-src-" + Guid.NewGuid().ToString("N"));
        try
        {
            var layoutDir = Path.Combine(tempDir, "layouts");
            Directory.CreateDirectory(layoutDir);
            File.WriteAllText(Path.Combine(layoutDir, "default.scriban"), "<html>{{content}}</html>");
            File.WriteAllText(Path.Combine(tempDir, "theme.yaml"), $$"""
                name: {{themeName}}
                version: 1.0.0
                """);

            var archivePath = Path.Combine(Path.GetTempPath(), themeName + "-" + Guid.NewGuid().ToString("N") + ".tar.gz");
            using (var fileStream = File.Create(archivePath))
            using (var gzip = new GZipStream(fileStream, CompressionMode.Compress))
            using (var writer = new TarWriter(gzip, leaveOpen: false))
            {
                AddFileToTar(writer, tempDir, "theme.yaml");
                AddFileToTar(writer, tempDir, "layouts/default.scriban");
            }

            return archivePath;
        }
        finally
        {
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void AddFileToTar(TarWriter writer, string baseDir, string relativePath)
    {
        var fullPath = Path.Combine(baseDir, relativePath);
        var entryPath = relativePath.Replace('\\', '/');
        var data = File.ReadAllBytes(fullPath);
        var entry = new PaxTarEntry(TarEntryType.RegularFile, entryPath);
        entry.DataStream = new MemoryStream(data);
        writer.WriteEntry(entry);
    }
}
