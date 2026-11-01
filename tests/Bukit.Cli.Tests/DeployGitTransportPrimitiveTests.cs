using System.Diagnostics;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// Real Git transport primitives used by deployment, exercised without any PATH
/// shim so the same tests run on Windows, macOS and Linux.
/// </summary>
public sealed class DeployGitTransportPrimitiveTests
{
    [Fact]
    public async Task FetchRefsHeads_WhenBranchAndTagShareName_UsesTheBranchHead()
    {
        using var scope = new GitScope();
        var seed = await scope.CreateSeedAsync("gh-pages", tagSameName: true);

        var work = await scope.CreateWorkRepositoryAsync(seed.Remote);
        await scope.GitAsync(work, "fetch", "origin", "refs/heads/gh-pages");
        await scope.GitAsync(work, "checkout", "-B", "gh-pages", "FETCH_HEAD");

        Assert.True(File.Exists(Path.Combine(work, "branch-only.txt")));
        Assert.False(File.Exists(Path.Combine(work, "tag-only.txt")));
        Assert.Equal(seed.BranchHead, (await scope.GitAsync(work, "rev-parse", "FETCH_HEAD")).Trim());
    }

    [Fact]
    public async Task ShallowFetchDepthOne_FetchesOnlyTheTip()
    {
        using var scope = new GitScope();
        var seed = await scope.CreateSeedAsync("gh-pages", tagSameName: false);

        var work = await scope.CreateWorkRepositoryAsync(seed.Remote);
        await scope.GitAsync(work, "fetch", "--depth", "1", "origin", "refs/heads/gh-pages");
        await scope.GitAsync(work, "checkout", "-B", "gh-pages", "FETCH_HEAD");

        Assert.Equal("true", (await scope.GitAsync(work, "rev-parse", "--is-shallow-repository")).Trim());
        Assert.Equal("1", (await scope.GitAsync(work, "rev-list", "--count", "FETCH_HEAD")).Trim());
        Assert.True(File.Exists(Path.Combine(work, "branch-only.txt")));
    }

    [Fact]
    public async Task KeepHistoryFetch_RetainsTheFullBranchHistory()
    {
        using var scope = new GitScope();
        var seed = await scope.CreateSeedAsync("gh-pages", tagSameName: false, commits: 3);

        var work = await scope.CreateWorkRepositoryAsync(seed.Remote);
        await scope.GitAsync(work, "fetch", "origin", "refs/heads/gh-pages");

        Assert.Equal("false", (await scope.GitAsync(work, "rev-parse", "--is-shallow-repository")).Trim());
        Assert.Equal("3", (await scope.GitAsync(work, "rev-list", "--count", "FETCH_HEAD")).Trim());
    }

    [Fact]
    public async Task CheckoutFetchHeadAndPush_UpdatesTheBranchWithoutDroppingTheTag()
    {
        using var scope = new GitScope();
        var seed = await scope.CreateSeedAsync("gh-pages", tagSameName: true);

        var work = await scope.CreateWorkRepositoryAsync(seed.Remote);
        await scope.GitAsync(work, "fetch", "origin", "refs/heads/gh-pages");
        await scope.GitAsync(work, "checkout", "-B", "gh-pages", "FETCH_HEAD");
        await File.WriteAllTextAsync(Path.Combine(work, "index.html"), "new page");
        await scope.GitAsync(work, "add", "index.html");
        await scope.GitAsync(work, "commit", "-m", "deploy");
        await scope.GitAsync(work, "push", "origin", "gh-pages");

        var branchTree = await scope.GitAsync(scope.Root, "--git-dir", seed.Remote, "ls-tree", "--name-only", "refs/heads/gh-pages");
        Assert.Contains("index.html", branchTree);
        Assert.DoesNotContain("tag-only.txt", branchTree);
        var tagTree = await scope.GitAsync(scope.Root, "--git-dir", seed.Remote, "ls-tree", "--name-only", "refs/tags/gh-pages");
        Assert.Contains("tag-only.txt", tagTree);
    }

    [Fact]
    public async Task ForcePush_OverwritesDivergedBranchHistory()
    {
        using var scope = new GitScope();
        var seed = await scope.CreateSeedAsync("gh-pages", tagSameName: false);

        var first = await scope.CreateWorkRepositoryAsync(seed.Remote);
        await scope.GitAsync(first, "fetch", "origin", "refs/heads/gh-pages");
        await scope.GitAsync(first, "checkout", "-B", "gh-pages", "FETCH_HEAD");
        await File.WriteAllTextAsync(Path.Combine(first, "first.txt"), "first");
        await scope.GitAsync(first, "add", "-A");
        await scope.GitAsync(first, "commit", "-m", "first");
        await scope.GitAsync(first, "push", "origin", "gh-pages");

        var second = await scope.CreateWorkRepositoryAsync(seed.Remote);
        await scope.GitAsync(second, "fetch", "origin", "refs/heads/gh-pages");
        await scope.GitAsync(second, "checkout", "-B", "gh-pages", seed.BranchHead);
        await File.WriteAllTextAsync(Path.Combine(second, "second.txt"), "second");
        await scope.GitAsync(second, "add", "-A");
        await scope.GitAsync(second, "commit", "-m", "second");
        await scope.GitAsync(second, "push", "--force", "origin", "gh-pages");

        var tree = await scope.GitAsync(scope.Root, "--git-dir", seed.Remote, "ls-tree", "--name-only", "refs/heads/gh-pages");
        Assert.Contains("second.txt", tree);
        Assert.DoesNotContain("first.txt", tree);
    }

    private sealed class GitScope : IDisposable
    {
        public GitScope()
        {
            Root = Path.Combine(Path.GetTempPath(), "bukit-git-primitive-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public async Task<SeedRepository> CreateSeedAsync(string branch, bool tagSameName, int commits = 1)
        {
            var remote = Path.Combine(Root, "remote-" + Guid.NewGuid().ToString("N") + ".git");
            await GitAsync(Root, "init", "--bare", remote);
            var seed = Path.Combine(Root, "seed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(seed);
            await GitAsync(seed, "init");
            await GitAsync(seed, "config", "core.autocrlf", "false");
            await GitAsync(seed, "config", "user.email", "tests@example.test");
            await GitAsync(seed, "config", "user.name", "Bukit Tests");
            await GitAsync(seed, "checkout", "-B", branch);
            await File.WriteAllTextAsync(Path.Combine(seed, "branch-only.txt"), "branch");
            for (var index = 0; index < commits; index++)
            {
                await File.WriteAllTextAsync(Path.Combine(seed, $"branch-{index}.txt"), $"branch {index}");
                await GitAsync(seed, "add", "-A");
                await GitAsync(seed, "commit", "-m", $"branch {index}");
            }

            var branchHead = (await GitAsync(seed, "rev-parse", "HEAD")).Trim();
            if (tagSameName)
            {
                await GitAsync(seed, "checkout", "-B", "tag-source");
                await File.WriteAllTextAsync(Path.Combine(seed, "tag-only.txt"), "tag");
                await GitAsync(seed, "add", "-A");
                await GitAsync(seed, "commit", "-m", "tag");
                await GitAsync(seed, "tag", branch);
                await GitAsync(seed, "push", remote, $"refs/heads/{branch}:refs/heads/{branch}");
                await GitAsync(seed, "push", remote, $"refs/tags/{branch}:refs/tags/{branch}");
            }
            else
            {
                await GitAsync(seed, "push", remote, $"refs/heads/{branch}:refs/heads/{branch}");
            }

            return new SeedRepository(remote, branchHead);
        }

        public async Task<string> CreateWorkRepositoryAsync(string remote)
        {
            var work = Path.Combine(Root, "work-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            await GitAsync(work, "init");
            await GitAsync(work, "config", "core.autocrlf", "false");
            await GitAsync(work, "config", "user.email", "tests@example.test");
            await GitAsync(work, "config", "user.name", "Bukit Tests");
            await GitAsync(work, "remote", "add", "origin", remote);
            return work;
        }

        public async Task<string> GitAsync(string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException("git could not be started");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var output = await stdout;
            var error = await stderr;
            Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed record SeedRepository(string Remote, string BranchHead);
}
