using Bukit.Cli;
using Xunit;

namespace Bukit.Cli.Tests;

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
        var reader = new ArgReader(Array.Empty<string>());
        var result = ConfigPathResolver.Resolve(reader);
        Assert.EndsWith("site.yaml", result.FullConfigPath);
    }

    [Fact]
    public void Resolve_WithConfig_ReturnsSpecifiedPath()
    {
        var configPath = Path.Combine(_testDir, "custom.yaml");
        var reader = new ArgReader(new[] { "--config", configPath });
        var result = ConfigPathResolver.Resolve(reader);
        Assert.Equal(Path.GetFullPath(configPath), result.FullConfigPath);
    }

    [Fact]
    public void Resolve_WithSite_ResolvesSitesSubdir()
    {
        var savedDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var reader = new ArgReader(new[] { "--site", "blog" });
            var result = ConfigPathResolver.Resolve(reader);
            var cwd = Directory.GetCurrentDirectory();
            var expected = Path.GetFullPath(Path.Combine(cwd, "sites", "blog.yaml"));
            Assert.Equal(expected, result.FullConfigPath);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedDir);
        }
    }

    [Fact]
    public void Resolve_WithSitePathTraversal_ThrowsInvalidOperation()
    {
        var savedDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var reader = new ArgReader(new[] { "--site", "../../../etc/passwd" });
            Assert.Throws<InvalidOperationException>(() =>
                ConfigPathResolver.Resolve(reader));
        }
        finally
        {
            Directory.SetCurrentDirectory(savedDir);
        }
    }

    [Fact]
    public void Resolve_WithSiteYamlExtension_DoesNotDoubleAppend()
    {
        var savedDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var reader = new ArgReader(new[] { "--site", "mysite.yaml" });
            var result = ConfigPathResolver.Resolve(reader);
            Assert.EndsWith("mysite.yaml", result.FullConfigPath);
            Assert.DoesNotContain(".yaml.yaml", result.FullConfigPath);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedDir);
        }
    }
}
