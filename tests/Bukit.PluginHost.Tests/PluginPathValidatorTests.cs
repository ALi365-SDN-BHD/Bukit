using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginPathValidatorTests
{
    [Theory]
    [InlineData("plugins/import")]
    [InlineData("plugins/clone")]
    public void ValidatePluginSource_AllowsPluginsId(string source)
    {
        using var directory = TestDirectory.Create();
        var validator = new PluginPathValidator();

        PluginPathValidationResult result = validator.ValidatePluginSource(directory.Path, source);

        Assert.True(result.Success, result.Message);
        Assert.Equal(source.Replace('\\', '/'), result.NormalizedRelativePath);
        Assert.StartsWith(System.IO.Path.Combine(directory.Path, "plugins"), result.FullPath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".bukit/plugins/import")]
    [InlineData("../plugins/import")]
    [InlineData("/tmp/plugin")]
    [InlineData("plugins/../evil")]
    [InlineData("plugins/import/bin/tool")]
    [InlineData(@"C:\tools\plugin")]
    public void ValidatePluginSource_RejectsUnsafePaths(string source)
    {
        using var directory = TestDirectory.Create();
        var validator = new PluginPathValidator();

        PluginPathValidationResult result = validator.ValidatePluginSource(directory.Path, source);

        Assert.False(result.Success);
    }

    [Fact]
    public void ValidatePluginSource_RejectsSymlinkEscapingPluginsDirectory()
    {
        using var directory = TestDirectory.Create();
        string pluginsRoot = System.IO.Path.Combine(directory.Path, "plugins");
        string outsideRoot = System.IO.Path.Combine(directory.Path, "outside", "import");
        Directory.CreateDirectory(pluginsRoot);
        Directory.CreateDirectory(outsideRoot);
        if (!TryCreateDirectorySymlink(System.IO.Path.Combine(pluginsRoot, "import"), outsideRoot))
        {
            return;
        }

        var validator = new PluginPathValidator();

        PluginPathValidationResult result = validator.ValidatePluginSource(directory.Path, "plugins/import");

        Assert.False(result.Success);
        Assert.Contains("real path", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("bin/osx-arm64/bukit-plugin-import")]
    [InlineData("bin/win-x64/bukit-plugin-import.exe")]
    public void ValidatePluginEntry_AllowsRelativeEntryInsidePlugin(string entry)
    {
        using var directory = TestDirectory.Create();
        string pluginRoot = System.IO.Path.Combine(directory.Path, "plugins/import");
        Directory.CreateDirectory(pluginRoot);
        var validator = new PluginPathValidator();

        PluginPathValidationResult result = validator.ValidatePluginEntry(directory.Path, pluginRoot, entry);

        Assert.True(result.Success, result.Message);
        Assert.Equal(entry.Replace('\\', '/'), result.NormalizedRelativePath);
        Assert.StartsWith(pluginRoot, result.FullPath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../../evil")]
    [InlineData(".bukit/bin/plugin")]
    [InlineData("/usr/local/bin/plugin")]
    [InlineData(@"C:\tools\plugin.exe")]
    public void ValidatePluginEntry_RejectsUnsafePaths(string entry)
    {
        using var directory = TestDirectory.Create();
        string pluginRoot = System.IO.Path.Combine(directory.Path, "plugins/import");
        Directory.CreateDirectory(pluginRoot);
        var validator = new PluginPathValidator();

        PluginPathValidationResult result = validator.ValidatePluginEntry(directory.Path, pluginRoot, entry);

        Assert.False(result.Success);
    }

    [Fact]
    public void ValidatePluginEntry_RejectsSymlinkEscapingPluginDirectory()
    {
        using var directory = TestDirectory.Create();
        string pluginRoot = System.IO.Path.Combine(directory.Path, "plugins/import");
        string entryDirectory = System.IO.Path.Combine(pluginRoot, "bin", "test-rid");
        string outsideDirectory = System.IO.Path.Combine(directory.Path, "outside");
        Directory.CreateDirectory(entryDirectory);
        Directory.CreateDirectory(outsideDirectory);
        string outsideExecutable = System.IO.Path.Combine(outsideDirectory, "plugin");
        File.WriteAllText(outsideExecutable, string.Empty);
        if (!TryCreateFileSymlink(System.IO.Path.Combine(entryDirectory, "plugin"), outsideExecutable))
        {
            return;
        }

        var validator = new PluginPathValidator();

        PluginPathValidationResult result = validator.ValidatePluginEntry(
            directory.Path,
            pluginRoot,
            "bin/test-rid/plugin");

        Assert.False(result.Success);
        Assert.Contains("real path", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateDirectorySymlink(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateFileSymlink(string path, string target)
    {
        try
        {
            File.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
