using Bukit.Cli.Tests;
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
  sources:
    - type: markdown
      name: page
      collection: page
      markdown:
        dir: content
  markdown:
    dir: content
theme:
  name: starter
""");
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_testDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_WithConfig_CleansOutput()
    {
        var distDir = Path.Combine(_testDir, "dist");
        var configPath = Path.Combine(_testDir, "site.yaml");
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", new[] { "--config", configPath }));
        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(distDir));
    }

    [Fact]
    public async Task RunAsync_NonExistentDir_DoesNotThrow()
    {
        var configPath = Path.Combine(_testDir, "site.yaml");
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", new[] { "--config", configPath, "--dir", Path.Combine(_testDir, "nonexistent") }));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_PathTraversal_ReturnsError()
    {
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", new[] { "--dir", "../outside" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithDir_RejectsEscapeOutsideCwd()
    {
        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", new[] { "--dir", "/etc" }));
        Assert.Equal(2, exitCode);
    }
}
