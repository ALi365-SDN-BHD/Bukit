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
    public async Task RunAsync_WithConfig_RefusesSourceInsideCacheBeforeDeletingAnything()
    {
        var cacheSource = Path.Combine(_testDir, ".cache", "content");
        Directory.CreateDirectory(cacheSource);
        File.WriteAllText(Path.Combine(cacheSource, "draft.md"), "---\ntitle: Draft\n---\nbody");
        var outputSentinel = Path.Combine(_testDir, "dist", "test.txt");
        var configPath = Path.Combine(_testDir, "source-in-cache.yaml");
        File.WriteAllText(configPath, """
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
        dir: .cache/content
theme:
  name: starter
""");

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", ["--config", configPath]));

        Assert.Equal(2, exitCode);
        Assert.True(File.Exists(outputSentinel));
        Assert.True(File.Exists(Path.Combine(_testDir, "dist", ".bukit-output-marker")));
        Assert.Equal("---\ntitle: Draft\n---\nbody", File.ReadAllText(Path.Combine(cacheSource, "draft.md")));
    }

    [Fact]
    public async Task RunAsync_WithConfig_CleansIndependentOutputAndCache()
    {
        var content = Path.Combine(_testDir, "content");
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(content, "draft.md"), "---\ntitle: Draft\n---\nbody");
        var cache = Path.Combine(_testDir, ".cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "cached.txt"), "cache");
        var configPath = Path.Combine(_testDir, "safe.yaml");
        File.WriteAllText(configPath, """
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
theme:
  name: starter
""");

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", ["--config", configPath]));

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(_testDir, "dist")));
        Assert.False(Directory.Exists(cache));
        Assert.True(File.Exists(Path.Combine(content, "draft.md")));
    }

    [Fact]
    public async Task RunAsync_WithConfig_CleansOutputWithMultipleIndependentContentSources()
    {
        var first = Path.Combine(_testDir, "content-a");
        var second = Path.Combine(_testDir, "content-b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "a.md"), "---\ntitle: A\n---\nAlpha");
        File.WriteAllText(Path.Combine(second, "b.md"), "---\ntitle: B\n---\nBeta");
        var cache = Path.Combine(_testDir, ".cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "cached.txt"), "cache");
        var configPath = Path.Combine(_testDir, "multi-source.yaml");
        File.WriteAllText(configPath, """
site:
  name: test
  title: Test
  baseUrl: /
build:
  output: dist
content:
  sources:
    - type: markdown
      name: a
      collection: page
      markdown:
        dir: content-a
    - type: markdown
      name: b
      collection: page
      markdown:
        dir: content-b
theme:
  name: starter
""");

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", ["--config", configPath]));

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(_testDir, "dist")));
        Assert.False(Directory.Exists(cache));
        Assert.True(File.Exists(Path.Combine(first, "a.md")));
        Assert.True(File.Exists(Path.Combine(second, "b.md")));
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

    [Theory]
    [InlineData("dist")]
    [InlineData(".bukit-output")]
    public async Task RunAsync_WithDir_CleansMarkedOutputDirectory(string directory)
    {
        var outputDir = Path.Combine(_testDir, directory);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, ".bukit-output-marker"), "Bukit output directory");
        using var cwd = new CurrentDirectoryScope(_testDir);

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(
            CliTestHelper.CreateCommand("clean", new[] { "--dir", directory }));

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

    [Theory]
    [InlineData("default")]
    [InlineData("dir")]
    [InlineData("config")]
    [InlineData("site")]
    [Trait("Defect", "KD-002")]
    public async Task RunAsync_PreservesProjectMetadataAndCleansOutputAndCache(string mode)
    {
        var pluginDir = Path.Combine(_testDir, ".bukit");
        var files = new Dictionary<string, byte[]>
        {
            ["plugins.yaml"] = System.Text.Encoding.UTF8.GetBytes("# retain formatting\r\nversion: 1\r\nplugins: {}\r\n"),
            ["plugins.lock.yaml"] = System.Text.Encoding.UTF8.GetBytes("version: 1\nresolved: {}\n"),
            [Path.Combine("user", "custom.bin")] = [0, 255, 128, 10],
            ["build-report.json"] = System.Text.Encoding.UTF8.GetBytes("{ \"retained\": true }\n")
        };
        foreach (var (relativePath, bytes) in files)
        {
            var path = Path.Combine(pluginDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
        var cacheDir = Path.Combine(_testDir, ".cache");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "cached.txt"), "disposable");
        var sitesDir = Path.Combine(_testDir, "sites");
        Directory.CreateDirectory(sitesDir);
        WriteConfig(Path.Combine(sitesDir, "example.yaml"), "dist");
        using var cwd = new CurrentDirectoryScope(_testDir);
        string[] args = mode switch
        {
            "config" => ["--config", Path.Combine(_testDir, "site.yaml")],
            "site" => ["--site", "example"],
            "dir" => ["--dir", "dist"],
            _ => []
        };

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", args));

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(_testDir, "dist")));
        Assert.False(Directory.Exists(cacheDir));
        foreach (var (relativePath, bytes) in files)
        {
            var path = Path.Combine(pluginDir, relativePath);
            Assert.True(File.Exists(path), $"User metadata was deleted: {relativePath}");
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        Assert.Equal(files.Count, Directory.EnumerateFiles(pluginDir, "*", SearchOption.AllDirectories).Count());
    }

    [Theory]
    [InlineData(".bukit", false)]
    [InlineData(".bukit", true)]
    [InlineData(".bukit/user", false)]
    [InlineData(".bukit/user", true)]
    [InlineData(".BUKIT", false)]
    [InlineData(".BUKIT", true)]
    [InlineData(".BUKIT/user", false)]
    [InlineData(".BUKIT/user", true)]
    public async Task RunAsync_RefusesProjectMetadataAsOutput(string relativeOutput, bool configured)
    {
        var outputDir = Path.Combine(_testDir, relativeOutput);
        var pluginDir = Path.Combine(_testDir, ".bukit");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(pluginDir);
        var pluginConfig = Path.Combine(pluginDir, "plugins.yaml");
        var sentinel = Path.Combine(outputDir, "user.bin");
        byte[] bytes = [0, 255, 128, 10];
        File.WriteAllText(pluginConfig, "version: 1\nplugins: {}\n");
        File.WriteAllBytes(sentinel, bytes);
        File.WriteAllText(Path.Combine(outputDir, ".bukit-output-marker"), "Bukit output directory");
        var cacheDir = Path.Combine(_testDir, ".cache");
        Directory.CreateDirectory(cacheDir);
        var cacheFile = Path.Combine(cacheDir, "cached.txt");
        File.WriteAllText(cacheFile, "retain on refusal");
        var configPath = Path.Combine(_testDir, "site.yaml");
        WriteConfig(configPath, relativeOutput);
        using var cwd = new CurrentDirectoryScope(_testDir);
        string[] args = configured ? ["--config", configPath] : ["--dir", relativeOutput];

        var exitCode = await Bukit.Cli.Commands.CleanCommand.RunAsync(CliTestHelper.CreateCommand("clean", args));

        Assert.Equal(2, exitCode);
        Assert.Equal("version: 1\nplugins: {}\n", File.ReadAllText(pluginConfig));
        Assert.Equal(bytes, File.ReadAllBytes(sentinel));
        Assert.True(File.Exists(Path.Combine(outputDir, ".bukit-output-marker")));
        Assert.Equal("retain on refusal", File.ReadAllText(cacheFile));
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
