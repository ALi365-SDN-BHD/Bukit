using Xunit;

namespace Bukit.Cli.Tests;

[Collection("CWD")]
public sealed class ConfigPathResolverTests : IDisposable
{
    private readonly string _testDir;

    public ConfigPathResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-cli-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(Path.Combine(_testDir, "sites"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_DefaultConfig_ReturnsSiteYaml()
    {
        var result = ConfigPathResolver.Resolve(null, null);
        Assert.EndsWith("site.yaml", result.FullConfigPath);
    }

    [Fact]
    public void Resolve_WithConfig_ReturnsSpecifiedPath()
    {
        var configPath = Path.Combine(_testDir, "custom.yaml");
        var result = ConfigPathResolver.Resolve(configPath, null);
        Assert.Equal(Path.GetFullPath(configPath), result.FullConfigPath);
    }

    [Fact]
    public void Resolve_WithSite_ResolvesSitesSubdir()
    {
        using var _ = new CurrentDirectoryScope(_testDir);
        var result = ConfigPathResolver.Resolve(null, "blog");
        var cwd = Directory.GetCurrentDirectory();
        var expected = Path.GetFullPath(Path.Combine(cwd, "sites", "blog.yaml"));
        Assert.Equal(expected, result.FullConfigPath);
    }

    [Fact]
    public void Resolve_WithSitePathTraversal_ThrowsInvalidOperation()
    {
        using var _ = new CurrentDirectoryScope(_testDir);
        Assert.Throws<InvalidOperationException>(() =>
            ConfigPathResolver.Resolve(null, "../../../etc/passwd"));
    }

    [Fact]
    public void Resolve_WithSiteYamlExtension_DoesNotDoubleAppend()
    {
        using var _ = new CurrentDirectoryScope(_testDir);
        var result = ConfigPathResolver.Resolve(null, "mysite.yaml");
        Assert.EndsWith("mysite.yaml", result.FullConfigPath);
        Assert.DoesNotContain(".yaml.yaml", result.FullConfigPath);
    }
}
