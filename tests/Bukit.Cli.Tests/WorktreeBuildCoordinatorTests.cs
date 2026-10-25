using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("CWD")]
public sealed class WorktreeBuildCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-worktree-build-" + Guid.NewGuid().ToString("N"));

    public WorktreeBuildCoordinatorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => TestCleanup.DeleteDirectory(_root, recursive: true);

    [Fact]
    public async Task ExplicitMultilingualBuild_RequiresGit()
    {
        var site = CreateSite(Path.Combine(_root, "not-a-repository"));

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(
            () => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-git-required", error.Code);
    }

    [Theory]
    [InlineData("unstaged")]
    [InlineData("staged")]
    [InlineData("untracked")]
    public async Task ExplicitMultilingualBuild_RejectsDirtyRepository(string changeKind)
    {
        var root = Path.Combine(_root, "dirty-" + changeKind);
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var sentinel = Path.Combine(root, "dist", "sentinel.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sentinel)!);
        await File.WriteAllTextAsync(sentinel, "unchanged");
        if (changeKind == "untracked")
        {
            await File.WriteAllTextAsync(Path.Combine(root, "untracked.txt"), "untracked");
        }
        else
        {
            await File.AppendAllTextAsync(site, "\n# dirty\n");
            if (changeKind == "staged")
            {
                Assert.Equal(0, (await RunProcessAsync("git", ["add", "site.yaml"], root)).ExitCode);
            }
        }

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(
            () => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-dirty", error.Code);
        Assert.Equal("unchanged", await File.ReadAllTextAsync(sentinel));
    }

    [Fact]
    public async Task DirtyDiagnostic_HasSameIdentityInTextAndJson()
    {
        var root = Path.Combine(_root, "diagnostic-identity");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        await File.AppendAllTextAsync(site, "\n# dirty\n");

        var textResult = await RunProcessAsync(
            "dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site],
            root);
        var jsonResult = await RunProcessAsync(
            "dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site, "--log-format", "json"],
            root);

        Assert.Equal(1, textResult.ExitCode);
        Assert.Equal(1, jsonResult.ExitCode);
        Assert.Contains("i18n-worktree-dirty", textResult.Output, StringComparison.Ordinal);
        Assert.Contains("i18n-worktree-dirty", jsonResult.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_RejectsIgnoredRequiredInput()
    {
        var root = Path.Combine(_root, "ignored-input");
        var site = CreateSite(root, "content/*.ignored.md\n");
        await InitializeRepositoryAsync(root);
        await File.WriteAllTextAsync(Path.Combine(root, "content", "secret.ignored.md"), "---\ntitle: Ignored\nlanguage: en\n---\nignored");

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(
            () => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-input-untracked", error.Code);
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_RejectsExternalContentInput()
    {
        var root = Path.Combine(_root, "external-input-site");
        var external = Path.Combine(_root, "external-content");
        Directory.CreateDirectory(external);
        await File.WriteAllTextAsync(Path.Combine(external, "page.md"), "---\ntitle: External\nlanguage: en\n---\nexternal");
        var site = CreateSite(root);
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace(
            "dir: content",
            $"dir: \"{external.Replace('\\', '/')}\"",
            StringComparison.Ordinal));
        await InitializeRepositoryAsync(root);

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(
            () => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-input-untracked", error.Code);
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_RejectsInputSymlinkOutsideRepository()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(_root, "external-symlink-site");
        var site = CreateSite(root);
        var external = Path.Combine(_root, "external-symlink-content");
        Directory.CreateDirectory(external);
        TestCleanup.DeleteDirectory(Path.Combine(root, "content"), recursive: true);
        Directory.CreateSymbolicLink(Path.Combine(root, "content"), external);
        await InitializeRepositoryAsync(root);

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(
            () => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-input-untracked", error.Code);
    }

    [Fact]
    public async Task InProcessBuild_RemainsAvailableForInternalCallersOutsideGit()
    {
        var site = CreateSite(Path.Combine(_root, "in-process"));

        var exitCode = await BuildCommand.RunInProcessAsync(Command(site));

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(site)!, "dist", "en", "index.html")));
    }

    [Fact]
    public async Task WorktreeBuild_MatchesInProcessOutputs_AndReusesIncrementalManifest()
    {
        var root = Path.Combine(_root, "equivalence");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);

        Assert.Equal(0, await BuildCommand.RunInProcessAsync(Command(site)));
        var expected = SnapshotPublishedOutputs(Path.Combine(root, "dist"));
        TestCleanup.DeleteDirectory(Path.Combine(root, "dist"), recursive: true);
        TestCleanup.DeleteDirectory(Path.Combine(root, ".cache"), recursive: true);

        var first = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
        Assert.True(first.ExitCode == 0, first.Output);
        Assert.Equal(expected, SnapshotPublishedOutputs(Path.Combine(root, "dist")));

        var second = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
        Assert.True(second.ExitCode == 0, second.Output);
        Assert.Equal(2, second.Output.Split('\n').Count(line => line.Contains("rendered=0, skipped=2", StringComparison.Ordinal)));

        var worktrees = await RunProcessAsync("git", ["worktree", "list", "--porcelain"], root);
        Assert.Equal(0, worktrees.ExitCode);
        Assert.Single(worktrees.Output.Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
    }

    [Fact]
    public void OutputCommit_RestoresEarlierDirectory_WhenLaterExchangeFails()
    {
        var transaction = Path.Combine(_root, "transaction");
        var finalOutput = Path.Combine(transaction, "dist");
        var stagingOutput = Path.Combine(transaction, ".dist.staging");
        var backupOutput = Path.Combine(transaction, ".dist.backup");
        var finalCache = Path.Combine(transaction, ".cache");
        Directory.CreateDirectory(finalOutput);
        Directory.CreateDirectory(stagingOutput);
        Directory.CreateDirectory(finalCache);
        File.WriteAllText(Path.Combine(finalOutput, "sentinel.txt"), "old-output");
        File.WriteAllText(Path.Combine(stagingOutput, "sentinel.txt"), "new-output");
        File.WriteAllText(Path.Combine(finalCache, "sentinel.txt"), "old-cache");

        Assert.Throws<DirectoryNotFoundException>(() => WorktreeBuildCoordinator.CommitDirectories(
            [
                (stagingOutput, finalOutput, backupOutput),
                (Path.Combine(transaction, ".missing-cache-staging"), finalCache, Path.Combine(transaction, ".cache.backup"))
            ],
            []));

        Assert.Equal("old-output", File.ReadAllText(Path.Combine(finalOutput, "sentinel.txt")));
        Assert.Equal("old-cache", File.ReadAllText(Path.Combine(finalCache, "sentinel.txt")));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task WorktreeBuild_PreservesOutputCleaningProtection(bool clean, bool marked)
    {
        var root = Path.Combine(_root, "output-protection");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var output = Path.Combine(root, "dist");
        Directory.CreateDirectory(output);
        var sentinel = Path.Combine(output, "user-file.txt");
        File.WriteAllText(sentinel, "keep me");
        var hidden = Path.Combine(output, ".npmrc");
        File.WriteAllText(hidden, "keep hidden file");
        var cache = Path.Combine(root, ".cache");
        Directory.CreateDirectory(cache);
        var cacheFile = Path.Combine(cache, "existing.key");
        File.WriteAllText(cacheFile, "keep cache file");
        if (marked)
        {
            File.WriteAllText(Path.Combine(output, ".bukit-output-marker"), "");
        }

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site, clean ? "--clean" : "--no-clean"], root);

        if (clean && !marked)
        {
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(".bukit-output-marker", result.Output, StringComparison.Ordinal);
            Assert.Equal("keep me", File.ReadAllText(sentinel));
        }
        else
        {
            Assert.True(result.ExitCode == 0, result.Output);
            Assert.Equal(!clean, File.Exists(sentinel));
            Assert.Equal(!clean, File.Exists(hidden));
            Assert.True(File.Exists(Path.Combine(output, "en", "index.html")));
        }
        Assert.Equal("keep cache file", File.ReadAllText(cacheFile));
    }

    [Fact]
    public void Staging_PreservesLinksAndEmptyDirectories_WithoutFollowingTargets()
    {
        if (OperatingSystem.IsWindows()) return; // Creating symlinks requires host privileges on Windows.
        var source = Path.Combine(_root, "source");
        var staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(Path.Combine(source, "empty"));
        File.WriteAllText(Path.Combine(source, ".env"), "retained");
        File.CreateSymbolicLink(Path.Combine(source, "file-link"), ".env");
        File.CreateSymbolicLink(Path.Combine(source, "dangling-link"), "missing");
        Directory.CreateSymbolicLink(Path.Combine(source, "cycle"), ".");

        WorktreeBuildCoordinator.PrepareStaging(source, staging, copyExisting: true);
        WorktreeBuildCoordinator.CommitDirectories([(staging, source, Path.Combine(_root, "backup"))], []);

        Assert.Equal("retained", File.ReadAllText(Path.Combine(source, ".env")));
        Assert.True(Directory.Exists(Path.Combine(source, "empty")));
        Assert.Equal(".env", new FileInfo(Path.Combine(source, "file-link")).LinkTarget);
        Assert.Equal("missing", new FileInfo(Path.Combine(source, "dangling-link")).LinkTarget);
        Assert.Equal(".", new DirectoryInfo(Path.Combine(source, "cycle")).LinkTarget);
    }

    [Fact]
    public void BackupCleanupFailure_DoesNotThrowOrRemoveRetainedBackup()
    {
        var backup = Path.Combine(_root, "retained-backup");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "original"), "old");

        // A directory at a file-cleanup path deterministically produces an I/O/access error on every OS.
        WorktreeBuildCoordinator.CleanupBackup(backup, isDirectory: false);

        Assert.Equal("old", File.ReadAllText(Path.Combine(backup, "original")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedBuild_PreservesOutputCacheAndExternalFiles(bool cacheLink)
    {
        if (cacheLink && OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_root, "failure-atomicity");
        var site = CreateSite(root, "/reports/\n");
        await InitializeRepositoryAsync(root);
        var invocation = new[] { typeof(BuildCommand).Assembly.Location, "build", "--config", site };
        var first = await RunProcessAsync("dotnet", invocation, root);
        Assert.True(first.ExitCode == 0, first.Output);
        var reports = Path.Combine(root, "reports");
        Directory.CreateDirectory(reports);
        var sentinel = Path.Combine(reports, "sentinel.txt");
        File.WriteAllText(sentinel, "unchanged");
        if (cacheLink)
        {
            File.CreateSymbolicLink(Path.Combine(root, ".cache", "build-manifest.json.tmp"), sentinel);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ invalid(");
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(reports, "result.json"));
            File.AppendAllText(Path.Combine(root, "content", "en", "index.md"), "\nnew content");
        }
        Assert.Equal(0, (await RunProcessAsync("git", ["add", "."], root)).ExitCode);
        Assert.Equal(0, (await RunProcessAsync("git", ["commit", "-m", "failure fixture"], root)).ExitCode);
        var outputBefore = SnapshotPublishedOutputs(Path.Combine(root, "dist"));
        var cacheBefore = SnapshotPublishedOutputs(Path.Combine(root, ".cache"));

        var result = await RunProcessAsync("dotnet", cacheLink ? invocation :
            [.. invocation, "--metrics", "reports/result.json"], root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(cacheLink ? "i18n-worktree-worker-failed" : "File transaction target is a directory", result.Output, StringComparison.Ordinal);
        Assert.Equal(outputBefore, SnapshotPublishedOutputs(Path.Combine(root, "dist")));
        Assert.Equal(cacheBefore, SnapshotPublishedOutputs(Path.Combine(root, ".cache")));
        Assert.Equal("unchanged", File.ReadAllText(sentinel));
        var worktrees = await RunProcessAsync("git", ["worktree", "list", "--porcelain"], root);
        Assert.Single(worktrees.Output.Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
    }

    [Fact]
    public void Commit_DoesNotDeleteBackupPathsItDidNotCreate()
    {
        var staging = Path.Combine(_root, "new-metrics.json");
        var final = Path.Combine(_root, "metrics.json");
        var backup = Path.Combine(_root, "unowned-backup");
        File.WriteAllText(staging, "new");
        File.WriteAllText(backup, "unrelated");

        WorktreeBuildCoordinator.CommitDirectories([], [(staging, final, backup)]);

        Assert.Equal("new", File.ReadAllText(final));
        Assert.Equal("unrelated", File.ReadAllText(backup));
    }

    [Fact]
    public async Task WorktreeBuild_RejectsGitDirectoryEvenWithoutCleaning()
    {
        var root = Path.Combine(_root, "unsafe-output");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var head = File.ReadAllText(Path.Combine(root, ".git", "HEAD"));

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site, "--output", ".git", "--no-clean"], root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unsafe output directory", result.Output, StringComparison.Ordinal);
        Assert.Equal(head, File.ReadAllText(Path.Combine(root, ".git", "HEAD")));
    }

    [Fact]
    public async Task WorkerFailure_PreservesPublishedOutputCacheAndExistingWorktrees()
    {
        var root = Path.Combine(_root, "worker-failure");
        var site = CreateSite(root);
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ invalid(");
        await InitializeRepositoryAsync(root);
        var existingWorktree = Path.Combine(_root, "existing-worktree");
        Assert.Equal(0, (await RunProcessAsync("git", ["worktree", "add", "--detach", existingWorktree, "HEAD"], root)).ExitCode);
        var outputSentinel = Path.Combine(root, "dist", "sentinel.txt");
        var cacheSentinel = Path.Combine(root, ".cache", "sentinel.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputSentinel)!);
        Directory.CreateDirectory(Path.GetDirectoryName(cacheSentinel)!);
        File.WriteAllText(outputSentinel, "old-output");
        File.WriteAllText(cacheSentinel, "old-cache");

        var result = await RunProcessAsync(
            "dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site],
            root);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("i18n-worktree-worker-failed", result.Output, StringComparison.Ordinal);
        Assert.Equal("old-output", File.ReadAllText(outputSentinel));
        Assert.Equal("old-cache", File.ReadAllText(cacheSentinel));
        var worktrees = await RunProcessAsync("git", ["worktree", "list", "--porcelain"], root);
        Assert.Equal(2, worktrees.Output.Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal)));
        Assert.True(Directory.Exists(existingWorktree));
    }

    [Fact]
    public async Task NextBuild_ProtectsActiveRun_ThenRecoversOnlyRegisteredResidualWorktree()
    {
        var root = Path.Combine(_root, "residual-recovery");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var repository = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
        var repositoryHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(repository)))[..16];
        var runRoot = Path.Combine(Path.GetTempPath(), "bukit-i18n-worktrees", repositoryHash, "interrupted-run");
        var residualWorktree = Path.Combine(runRoot, "workers", "en");
        Directory.CreateDirectory(runRoot);
        Assert.Equal(0, (await RunProcessAsync("git", ["worktree", "add", "--detach", residualWorktree, "HEAD"], root)).ExitCode);
        await File.WriteAllTextAsync(Path.Combine(runRoot, "owned-worktrees.txt"), residualWorktree + Environment.NewLine);

        using (WorktreeBuildCoordinator.AcquireRunLock(repository))
        {
            var competing = await RunProcessAsync("dotnet",
                [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
            Assert.NotEqual(0, competing.ExitCode);
            Assert.Contains("Cannot acquire the multilingual build lock", competing.Output, StringComparison.Ordinal);
            Assert.True(Directory.Exists(residualWorktree));
            Assert.True(File.Exists(Path.Combine(runRoot, "owned-worktrees.txt")));
        }

        var result = await RunProcessAsync(
            "dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site],
            root);

        Assert.Equal(0, result.ExitCode);
        Assert.False(Directory.Exists(residualWorktree));
        var worktrees = await RunProcessAsync("git", ["worktree", "list", "--porcelain"], root);
        Assert.Single(worktrees.Output.Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
    }

    private static CliBoundCommand Command(string site)
        => CliTestHelper.CreateCommand("build", ["--config", site]);

    private static string CreateSite(string root, string extraIgnore = "")
    {
        Directory.CreateDirectory(Path.Combine(root, "content", "en"));
        Directory.CreateDirectory(Path.Combine(root, "content", "zh"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(root, ".gitignore"), "/dist/\n/.cache/\n" + extraIgnore);
        File.WriteAllText(Path.Combine(root, "site.yaml"), """
            site:
              name: i18n-worktree-test
              title: I18N Worktree Test
              url: https://example.test
              languages: [en, zh-CN]
              defaultLanguage: en
              sitemapMode: merged
              search:
                mode: merged
              collections:
                page:
                  permalink: /{slug}/
                  template: pages/page.html
            content:
              sources:
                - type: markdown
                  name: pages
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
              clean: false
              languageJobs: 2
            theme:
              layouts: layouts
            """);
        File.WriteAllText(Path.Combine(root, "content", "en", "index.md"), "---\ntitle: English\nlanguage: en\ni18nKey: home\n---\nEnglish body");
        File.WriteAllText(Path.Combine(root, "content", "zh", "index.md"), "---\ntitle: Chinese\nlanguage: zh-CN\ni18nKey: home\n---\nChinese body");
        File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), "<!doctype html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ content }}");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{{ content }}");
        return Path.Combine(root, "site.yaml");
    }

    private static async Task InitializeRepositoryAsync(string root)
    {
        Assert.Equal(0, (await RunProcessAsync("git", ["init"], root)).ExitCode);
        Assert.Equal(0, (await RunProcessAsync("git", ["config", "user.email", "bukit-tests@example.test"], root)).ExitCode);
        Assert.Equal(0, (await RunProcessAsync("git", ["config", "user.name", "Bukit Tests"], root)).ExitCode);
        Assert.Equal(0, (await RunProcessAsync("git", ["add", "."], root)).ExitCode);
        Assert.Equal(0, (await RunProcessAsync("git", ["commit", "-m", "fixture"], root)).ExitCode);
    }

    private static Dictionary<string, byte[]> SnapshotPublishedOutputs(string outputDir)
        => Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(outputDir, path).Split(Path.DirectorySeparatorChar).Contains(".bukit", StringComparer.Ordinal) &&
                !string.Equals(Path.GetFileName(path), ".bukit-build-state.json", StringComparison.Ordinal))
            .ToDictionary(path => Path.GetRelativePath(outputDir, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await stdout + await stderr);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
