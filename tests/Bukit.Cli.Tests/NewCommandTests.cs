using System.Security.Cryptography;
using Bukit.Cli.Commands;
using Bukit.Cli.Commands.New;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class NewCommandTests : IDisposable
{
    private readonly string _root;

    public NewCommandTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "bukit-new-command-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_root, recursive: true);
    }

    [Fact]
    public async Task RunAsync_CreatesBuildableSite()
    {
        var target = Path.Combine(_root, "my-site");

        var code = await NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target]));

        Assert.Equal(0, code);
        var config = ConfigLoader.Load(Path.Combine(target, "site.yaml"));
        Assert.Equal(["en-US"], config.Site.Languages);
        Assert.Null(config.Site.Url);

        var build = await BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand(
            "build",
            ["--config", Path.Combine(target, "site.yaml"), "--output", "dist"]));

        Assert.Equal(0, build);
        Assert.True(File.Exists(Path.Combine(target, "dist/index.html")));
    }

    [Fact]
    public async Task RunAsync_BlankTitleIsRejected()
    {
        var target = Path.Combine(_root, "blank");

        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target, "--title", " "])));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task RunAsync_WhitespaceTargetIsRejected()
    {
        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [" "])));
    }

    [Fact]
    public async Task RunAsync_InvalidLanguageLeavesNoDirectory()
    {
        var target = Path.Combine(_root, "invalid-language");

        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target, "--language", "en_US"])));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task RunAsync_InvalidTitleLeavesNoDirectory()
    {
        var target = Path.Combine(_root, "invalid-title");

        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target, "--title", "bad\u0001title"])));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task RunAsync_RefusesFileTarget()
    {
        var target = Path.Combine(_root, "file-target");
        File.WriteAllText(target, "keep");

        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target])));

        Assert.Equal("keep", File.ReadAllText(target));
    }

    [Fact]
    public async Task RunAsync_RefusesNonEmptyTarget()
    {
        var target = Path.Combine(_root, "non-empty");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");

        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target])));

        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "keep.txt")));
    }

    [Fact]
    public async Task RunAsync_RefusesBrokenSymlinkTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var link = Path.Combine(_root, "broken-link");
        Directory.CreateSymbolicLink(link, Path.Combine(_root, "missing-target"));

        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [link])));

        Assert.False(Directory.Exists(link));
    }

    [Fact]
    public async Task RunAsync_RefusesBrokenSymlinkTargetWithTrailingSlash()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var link = Path.Combine(_root, "broken-link-slash");
        Directory.CreateSymbolicLink(link, Path.Combine(_root, "missing-target"));

        await Assert.ThrowsAsync<CommandArgumentException>(() =>
            NewCommand.RunAsync(CliTestHelper.CreateCommand(
                "new", [link + Path.DirectorySeparatorChar])));

        Assert.False(Directory.Exists(link));
    }

    [Fact]
    public async Task RunAsync_DefaultTitleFromDirectory()
    {
        var target = Path.Combine(_root, "my-site");

        var code = await NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target]));

        Assert.Equal(0, code);
        var config = ConfigLoader.Load(Path.Combine(target, "site.yaml"));
        Assert.Equal("my-site", config.Site.Name);
        Assert.Equal("my-site", config.Site.Title);
    }

    [Fact]
    public async Task RunAsync_DiacriticsSlugMatchesBukitJalil()
    {
        var target = Path.Combine(_root, "diacritics");

        var code = await NewCommand.RunAsync(CliTestHelper.CreateCommand(
            "new", [target, "--title", "Résumé"]));

        Assert.Equal(0, code);
        var config = ConfigLoader.Load(Path.Combine(target, "site.yaml"));
        Assert.Equal("re-sume", config.Site.Name);
    }

    [Fact]
    public async Task RunAsync_NoGitAndNoDist()
    {
        var target = Path.Combine(_root, "no-git");

        var code = await NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target]));

        Assert.Equal(0, code);
        Assert.False(Directory.Exists(Path.Combine(target, ".git")));
        Assert.False(Directory.Exists(Path.Combine(target, "dist")));
    }

    [Fact]
    public async Task RunAsync_IsDeterministic()
    {
        var first = Path.Combine(_root, "first");
        var second = Path.Combine(_root, "second");

        var firstCode = await NewCommand.RunAsync(CliTestHelper.CreateCommand(
            "new", [first, "--title", "Same Site", "--language", "zh-CN"]));
        var secondCode = await NewCommand.RunAsync(CliTestHelper.CreateCommand(
            "new", [second, "--title", "Same Site", "--language", "zh-CN"]));

        Assert.Equal(0, firstCode);
        Assert.Equal(0, secondCode);
        Assert.Equal(HashTree(first), HashTree(second));
    }

    private static string[] HashTree(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
                $"{path}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))))}")
            .ToArray();
}
