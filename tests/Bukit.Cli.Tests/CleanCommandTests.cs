using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("CWD")]
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
        File.WriteAllText(Path.Combine(dist, ".bukit-output-marker"), "Bukit output directory");
        WriteConfig(Path.Combine(_testDir, "site.yaml"), "dist");
    }

    private static void WriteConfig(string path, string output)
    {
        File.WriteAllText(path, $$"""
site:
  name: test
  title: Test
  baseUrl: /
build:
  output: {{output}}
content:
  sources:
    - type: markdown
      name: page
      collection: page
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
    public async Task RunAsync_WithConfig_RefusesUnsafeOutputDirectory()
    {
        var gitDir = Path.Combine(_testDir, ".git");
        Directory.CreateDirectory(gitDir);
        var sentinel = Path.Combine(gitDir, "sentinel.txt");
        File.WriteAllText(sentinel, "keep");
        var configPath = Path.Combine(_testDir, "unsafe.yaml");
        WriteConfig(configPath, ".git");

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", new[] { "--config", configPath }));

        Assert.Equal(2, exitCode);
        Assert.True(File.Exists(sentinel));
    }

    [Fact]
    public async Task RunAsync_WithConfig_RefusesUnmarkedOutputDirectory()
    {
        var outputDir = Path.Combine(_testDir, "unmarked");
        Directory.CreateDirectory(outputDir);
        var sentinel = Path.Combine(outputDir, "user-file.txt");
        File.WriteAllText(sentinel, "keep");
        var configPath = Path.Combine(_testDir, "unmarked.yaml");
        WriteConfig(configPath, "unmarked");

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", new[] { "--config", configPath }));

        Assert.Equal(2, exitCode);
        Assert.True(File.Exists(sentinel));
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

    [Fact]
    public async Task RunAsync_WithDir_RefusesGitDirectoryAndPreservesSentinel()
    {
        var gitDir = Path.Combine(_testDir, ".git");
        Directory.CreateDirectory(gitDir);
        var sentinel = Path.Combine(gitDir, "sentinel.txt");
        File.WriteAllText(sentinel, "keep");
        using var cwd = new CurrentDirectoryScope(_testDir);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", ".git" }));

        Assert.Equal(2, exitCode);
        Assert.True(File.Exists(sentinel));
    }

    [Fact]
    public async Task RunAsync_WithDir_RefusesProjectRoot()
    {
        using var cwd = new CurrentDirectoryScope(_testDir);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", "." }));

        Assert.Equal(2, exitCode);
        Assert.True(Directory.Exists(_testDir));
        Assert.True(File.Exists(Path.Combine(_testDir, "site.yaml")));
    }

    [Fact]
    public async Task RunAsync_WithDir_RefusesUnmarkedNonEmptyDirectory()
    {
        var outputDir = Path.Combine(_testDir, "unmarked");
        Directory.CreateDirectory(outputDir);
        var sentinel = Path.Combine(outputDir, "user-file.txt");
        File.WriteAllText(sentinel, "keep");
        using var cwd = new CurrentDirectoryScope(_testDir);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", "unmarked" }));

        Assert.Equal(2, exitCode);
        Assert.True(File.Exists(sentinel));
    }

    [Fact]
    public async Task RunAsync_WithDir_CleansMarkedOutputDirectory()
    {
        var outputDir = Path.Combine(_testDir, "dist");
        using var cwd = new CurrentDirectoryScope(_testDir);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", "dist" }));

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(outputDir));
    }

    [Fact]
    public async Task RunAsync_WithDir_CleansEmptyDirectory()
    {
        var outputDir = Path.Combine(_testDir, "empty");
        Directory.CreateDirectory(outputDir);
        using var cwd = new CurrentDirectoryScope(_testDir);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", "empty" }));

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(outputDir));
    }
}
