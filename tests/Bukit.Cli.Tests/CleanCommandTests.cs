using Bukit.Cli;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CleanCommandTests : IDisposable
{
    private readonly string _testDir;

    public CleanCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-clean-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        var dist = Path.Combine(_testDir, "dist");
        Directory.CreateDirectory(dist);
        File.WriteAllText(Path.Combine(dist, "test.txt"), "hello");
        var config = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(config, """
site:
  name: test
  title: Test
  baseUrl: /
build:
  output: dist
content:
  provider: markdown
  markdown:
    dir: content
theme:
  name: starter
""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task RunAsync_WithConfig_CleansOutput()
    {
        var distDir = Path.Combine(_testDir, "dist");
        var configPath = Path.Combine(_testDir, "site.yaml");
        var reader = new ArgReader(new[] { "--config", configPath });
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(reader);
        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(distDir));
    }

    [Fact]
    public async Task RunAsync_NonExistentDir_DoesNotThrow()
    {
        var configPath = Path.Combine(_testDir, "site.yaml");
        var reader = new ArgReader(new[] { "--config", configPath, "--dir", Path.Combine(_testDir, "nonexistent") });
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(reader);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_PathTraversal_ReturnsError()
    {
        var reader = new ArgReader(new[] { "--dir", "../outside" });
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(reader);
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithDir_RejectsEscapeOutsideCwd()
    {
        var reader = new ArgReader(new[] { "--dir", "/etc" });
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(reader);
        Assert.Equal(2, exitCode);
    }
}
