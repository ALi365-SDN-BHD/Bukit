using Bukit.Cli.Tests;
using Xunit;
using Xunit.Sdk;

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
    public async Task RunAsync_WithDir_RefusesGitDescendantAndPreservesDirectory()
    {
        var gitDescendant = Path.Combine(_testDir, ".git", "refs", "tags");
        Directory.CreateDirectory(gitDescendant);
        using var cwd = new CurrentDirectoryScope(_testDir);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", ".git/refs/tags" }));

        Assert.Equal(2, exitCode);
        Assert.True(Directory.Exists(gitDescendant));
    }

    [Fact]
    public async Task RunAsync_WithDir_RefusesSymlinkAncestorEscapingCurrentDirectory()
    {
        var externalRoot = Path.Combine(Path.GetTempPath(), "bukit-clean-external-" + Guid.NewGuid().ToString("N"));
        var victim = Path.Combine(externalRoot, "victim");
        var sentinel = Path.Combine(victim, "sentinel.txt");
        var alias = Path.Combine(_testDir, "alias");
        Directory.CreateDirectory(victim);
        File.WriteAllText(Path.Combine(victim, ".bukit-output-marker"), "Bukit output directory");
        File.WriteAllText(sentinel, "keep");

        try
        {
            CreateDirectorySymlinkOrSkip(alias, externalRoot);

            using var cwd = new CurrentDirectoryScope(_testDir);
            var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
                CliTestHelper.CreateCommand("clean", new[] { "--dir", "alias/victim" }));

            Assert.Equal(2, exitCode);
            Assert.True(Directory.Exists(victim));
            Assert.True(File.Exists(sentinel));
        }
        finally
        {
            DeleteDirectoryLinkIfExists(alias);
            TestCleanup.DeleteDirectory(externalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithDir_RefusesTargetSymlinkEscapingCurrentDirectory()
    {
        var externalRoot = Path.Combine(Path.GetTempPath(), "bukit-clean-external-" + Guid.NewGuid().ToString("N"));
        var sentinel = Path.Combine(externalRoot, "sentinel.txt");
        var alias = Path.Combine(_testDir, "linked-output");
        Directory.CreateDirectory(externalRoot);
        File.WriteAllText(Path.Combine(externalRoot, ".bukit-output-marker"), "Bukit output directory");
        File.WriteAllText(sentinel, "keep");

        try
        {
            CreateDirectorySymlinkOrSkip(alias, externalRoot);

            using var cwd = new CurrentDirectoryScope(_testDir);
            var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
                CliTestHelper.CreateCommand("clean", new[] { "--dir", "linked-output" }));

            Assert.Equal(2, exitCode);
            Assert.True(Directory.Exists(externalRoot));
            Assert.True(File.Exists(sentinel));
        }
        finally
        {
            DeleteDirectoryLinkIfExists(alias);
            TestCleanup.DeleteDirectory(externalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithDir_AllowsGitNamedAncestorOutsideProjectRoot()
    {
        var projectRoot = Path.Combine(_testDir, ".git", "project");
        var outputDir = Path.Combine(projectRoot, "dist");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, ".bukit-output-marker"), "Bukit output directory");
        using var cwd = new CurrentDirectoryScope(projectRoot);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", "dist" }));

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(outputDir));
    }

    [Fact]
    public async Task RunAsync_DoesNotTraverseFixedCacheSymlinks()
    {
        var externalRoot = Path.Combine(Path.GetTempPath(), "bukit-clean-cache-external-" + Guid.NewGuid().ToString("N"));
        var cacheTarget = Path.Combine(externalRoot, "cache-target");
        var bukitTarget = Path.Combine(externalRoot, "bukit-target");
        var cacheSentinel = Path.Combine(cacheTarget, "sentinel.txt");
        var bukitSentinel = Path.Combine(bukitTarget, "sentinel.txt");
        var cacheLink = Path.Combine(_testDir, ".cache");
        var bukitLink = Path.Combine(_testDir, ".bukit");
        Directory.CreateDirectory(cacheTarget);
        Directory.CreateDirectory(bukitTarget);
        File.WriteAllText(cacheSentinel, "keep");
        File.WriteAllText(bukitSentinel, "keep");

        try
        {
            CreateDirectorySymlinkOrSkip(cacheLink, cacheTarget);
            CreateDirectorySymlinkOrSkip(bukitLink, bukitTarget);

            using var cwd = new CurrentDirectoryScope(_testDir);
            var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
                CliTestHelper.CreateCommand("clean", new[] { "--dir", "dist" }));

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(cacheSentinel));
            Assert.True(File.Exists(bukitSentinel));
        }
        finally
        {
            DeleteDirectoryLinkIfExists(cacheLink);
            DeleteDirectoryLinkIfExists(bukitLink);
            TestCleanup.DeleteDirectory(externalRoot, recursive: true);
        }
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

    private static void CreateDirectorySymlinkOrSkip(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
        }
    }

    private static void DeleteDirectoryLinkIfExists(string linkPath)
    {
        try
        {
            Directory.Delete(linkPath);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
