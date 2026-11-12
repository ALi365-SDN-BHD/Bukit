using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Incremental;
using Bukit.Shared;
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

    [Fact]
    public async Task ExplicitSingleLanguageBuild_DoesNotRequireGit()
    {
        var root = Path.Combine(_root, "single-language");
        var site = CreateSite(root);
        File.WriteAllText(site, File.ReadAllText(site).Replace("languages: [en, zh-CN]", "languages: [en]", StringComparison.Ordinal));

        var result = await BuildCommand.RunAsync(Command(site));

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(root, "dist", "en", "index.html")));
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
            await File.WriteAllTextAsync(Path.Combine(root, "content", "untracked.md"), "untracked");
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
    public async Task ExplicitMultilingualBuild_RejectsDeletedInputDirectory()
    {
        var root = Path.Combine(_root, "deleted-input");
        var site = CreateSite(root);
        Directory.CreateDirectory(Path.Combine(root, "data"));
        await File.WriteAllTextAsync(Path.Combine(root, "data", "example.json"), "{}");
        await InitializeRepositoryAsync(root);
        File.Delete(Path.Combine(root, "data", "example.json"));
        Directory.Delete(Path.Combine(root, "data"));

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(() => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-dirty", error.Code);
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_AllowsUntrackedFileOutsideSite()
    {
        var root = Path.Combine(_root, "unrelated-untracked");
        var siteRoot = Path.Combine(root, "site");
        var site = CreateSite(siteRoot);
        await InitializeRepositoryAsync(root);
        await File.WriteAllTextAsync(Path.Combine(root, "scratch.txt"), "not a build input");

        var result = await RunProcessAsync(
            "dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site],
            root);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.True(File.Exists(Path.Combine(siteRoot, "dist", "en", "index.html")));
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_PublishesGitIgnoredMediaFromCustomDownloadDir()
    {
        var root = Path.Combine(_root, "ignored-media");
        var site = CreateSite(root, extraIgnore: "/media/\n");
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace(
            "content:\n  sources:",
            "content:\n  media:\n    downloadToLocal: true\n    downloadDir: media\n  sources:",
            StringComparison.Ordinal));
        Directory.CreateDirectory(Path.Combine(root, "media"));
        var photo = Path.Combine(root, "media", "photo.png");
        await File.WriteAllBytesAsync(photo, [1, 2, 3, 4]);
        await File.WriteAllTextAsync(
            Path.Combine(root, "content", "en", "index.md"),
            "---\ntitle: English\nlanguage: en\ni18nKey: home\n---\n![Photo](/assets/uploads/photo.png)");
        await InitializeRepositoryAsync(root);

        var result = await RunProcessAsync(
            "dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site],
            root);

        Assert.True(result.ExitCode == 0, result.Output);
        var published = Directory.EnumerateFiles(Path.Combine(root, "dist"), "photo.png", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(published);
        Assert.All(published, path => Assert.Equal([1, 2, 3, 4], File.ReadAllBytes(path)));
        Assert.Equal([1, 2, 3, 4], File.ReadAllBytes(photo));
    }

    [Fact]
    public async Task Prepare_AbsoluteMediaSymlink_UsesIsolatedSharedSnapshot()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_root, "absolute-media-snapshot");
        var site = CreateSite(root, extraIgnore: "/shared/media/\n");
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace("content:\n  sources:",
            "content:\n  media:\n    downloadToLocal: true\n    downloadDir: media\n  sources:", StringComparison.Ordinal));
        var media = Path.Combine(root, "shared", "media");
        Directory.CreateDirectory(media);
        await File.WriteAllTextAsync(Path.Combine(media, "photo.png"), "original");
        Directory.CreateSymbolicLink(Path.Combine(root, "media"), media);
        await InitializeRepositoryAsync(root);
        var run = await WorktreeBuildRun.StartAsync(ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
            new ConfigOverrides(), "text", new ConsoleLogger(LogLevel.Error), CancellationToken.None);
        try
        {
            await run.PrepareAsync(CancellationToken.None);
            var repository = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
            var runsRoot = WorktreeBuildPaths.RunsRoot(repository);
            var requests = Directory.GetFiles(runsRoot, "request.v1.json", SearchOption.AllDirectories);
            Assert.Equal(2, requests.Length);
            using var first = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(requests[0]));
            using var second = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(requests[1]));
            var mediaPath = first.RootElement.GetProperty("mediaDownloadDir").GetString()!;
            Assert.Equal(mediaPath, second.RootElement.GetProperty("mediaDownloadDir").GetString());
            Assert.NotEqual(PathUtils.GetCanonicalFullPath(media), PathUtils.GetCanonicalFullPath(mediaPath));
            await File.WriteAllTextAsync(Path.Combine(media, "photo.png"), "changed");
            Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(mediaPath, "photo.png")));
            await File.WriteAllTextAsync(Path.Combine(mediaPath, "download.png"), "download");
        }
        finally
        {
            await run.CleanupAsync();
        }
        Assert.Equal("changed", await File.ReadAllTextAsync(Path.Combine(media, "photo.png")));
        Assert.False(File.Exists(Path.Combine(media, "download.png")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitMultilingualBuild_CommitsNewStagedMedia(bool changeOriginal)
    {
        var root = Path.Combine(_root, "staged-media-commit");
        var site = CreateSite(root, extraIgnore: "/media/\n");
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace("content:\n  sources:",
            "content:\n  media:\n    downloadToLocal: true\n    downloadDir: media\n  sources:", StringComparison.Ordinal));
        Directory.CreateDirectory(Path.Combine(root, "media"));
        await InitializeRepositoryAsync(root);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var arg in new[] { typeof(BuildCommand).Assembly.Location, "build", "--config", site })
            process.StartInfo.ArgumentList.Add(arg);
        Assert.True(process.Start());
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var output = new StringBuilder();
        var added = false;
        while (await process.StandardError.ReadLineAsync() is { } line)
        {
            output.AppendLine(line);
            if (!line.Contains("event=build.i18n.worktree.start", StringComparison.Ordinal)) continue;
            var staged = Assert.Single(Directory.GetDirectories(root, ".media.bukit-staging-*"));
            await File.WriteAllTextAsync(Path.Combine(staged, "download.png"), "downloaded");
            if (changeOriginal)
                await File.WriteAllTextAsync(Path.Combine(root, "media", "user.png"), "user change");
            added = true;
        }
        await process.WaitForExitAsync();
        Assert.True(added, output.ToString());
        var details = output + await standardOutput;
        if (changeOriginal)
        {
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Media directory changed", details, StringComparison.Ordinal);
            Assert.Equal("user change", await File.ReadAllTextAsync(Path.Combine(root, "media", "user.png")));
            Assert.False(File.Exists(Path.Combine(root, "media", "download.png")));
        }
        else
        {
            Assert.True(process.ExitCode == 0, details);
            Assert.Equal("downloaded", await File.ReadAllTextAsync(Path.Combine(root, "media", "download.png")));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitMultilingualBuild_PersistsActualDownloadedMedia(bool failWorker)
    {
        var root = Path.Combine(_root, "real-media-download");
        var site = CreateSite(root, extraIgnore: "/media/\n");
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace("content:\n  sources:",
            "content:\n  media:\n    downloadToLocal: true\n    downloadDir: media\n    blockPrivateNetworks: false\n    maxRetries: 0\n  sources:", StringComparison.Ordinal));
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        using var bitmap = new SkiaSharp.SKBitmap(2, 2);
        using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var image = encoded.ToArray();
        var mediaDir = Path.Combine(root, "media");
        Directory.CreateDirectory(mediaDir);
        await File.WriteAllTextAsync(Path.Combine(mediaDir, "sentinel.txt"), "user data");
        if (failWorker) await File.WriteAllTextAsync(Path.Combine(root, "layouts", "pages", "page.html"), "{{ invalid(");
        await File.WriteAllTextAsync(Path.Combine(root, "content", "en", "index.md"),
            $"---\ntitle: English\nlanguage: en\ni18nKey: home\n---\n![Photo](http://127.0.0.1:{port}/photo.png)");
        await InitializeRepositoryAsync(root);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {image.Length}\r\nConnection: close\r\n\r\n"), timeout.Token);
            await stream.WriteAsync(image, timeout.Token);
        });

        var result = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
        await serve;

        Assert.Equal("user data", await File.ReadAllTextAsync(Path.Combine(mediaDir, "sentinel.txt")));
        if (failWorker)
        {
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(Directory.GetFiles(mediaDir, "*.png"));
            Assert.False(File.Exists(Path.Combine(mediaDir, ".media-index.json")));
            return;
        }
        Assert.True(result.ExitCode == 0, result.Output);
        var stored = Assert.Single(Directory.GetFiles(mediaDir, "*.png"));
        Assert.Equal(image, await File.ReadAllBytesAsync(stored));
        Assert.True(File.Exists(Path.Combine(mediaDir, ".media-index.json")));
        Assert.Contains(Directory.EnumerateFiles(Path.Combine(root, "dist", "en"), "*.png", SearchOption.AllDirectories),
            path => File.ReadAllBytes(path).SequenceEqual(image));
        listener.Stop();
        var offline = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
        Assert.True(offline.ExitCode == 0, offline.Output);
        Assert.Equal(image, await File.ReadAllBytesAsync(stored));
    }

    [Fact]
    public async Task GitWorktree_RecoversInterruptedJournalWriteWithoutDeletingPendingFile()
    {
        var repository = Path.Combine(_root, "journal-source");
        CreateSite(repository);
        await InitializeRepositoryAsync(repository);
        var root = Path.Combine(_root, "linked-worktree");
        var add = await RunProcessAsync("git", ["worktree", "add", "--detach", root, "HEAD"], repository);
        Assert.True(add.ExitCode == 0, add.Output);
        Assert.True(File.Exists(Path.Combine(root, ".git")));
        var site = Path.Combine(root, "site.yaml");
        Directory.CreateDirectory(Path.Combine(root, "dist"));
        await File.WriteAllTextAsync(Path.Combine(root, "dist", "old.txt"), "old");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(WorktreeBuildCoordinatorTests).Assembly.Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName~WorktreeCommitCrashChildProbe");
        start.Environment["BUKIT_TEST_WORKTREE_CRASH_ROOT"] = root;
        start.Environment["BUKIT_TEST_WORKTREE_CRASH_PHASE"] = "journal";
        using var child = Process.Start(start)!;
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch { child.Kill(true); throw; }
        Assert.True(child.ExitCode != 0, await stdout + await stderr);
        var gitDir = (await RunProcessAsync("git", ["rev-parse", "--absolute-git-dir"], root)).Output.Trim();
        var pending = Assert.Single(Directory.EnumerateFiles(gitDir, ".bukit-transaction-*.json.pending-*.tmp", SearchOption.AllDirectories));

        var run = await WorktreeBuildRun.StartAsync(ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
            new ConfigOverrides(), "text", new ConsoleLogger(LogLevel.Error), CancellationToken.None);
        try
        {
            Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(root, "dist", "old.txt")));
            Assert.Equal("partial", await File.ReadAllTextAsync(pending));
        }
        finally { await run.CleanupAsync(); }
    }

    [Fact]
    public async Task GitRepository_IgnoresInterruptedInitialJournalWriteWithoutDeletingIt()
    {
        var root = Path.Combine(_root, "initial-journal-interrupted");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var output = PathUtils.GetCanonicalFullPath(Path.Combine(root, "dist"));
        var identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(output)));
        var gitDir = (await RunProcessAsync("git", ["rev-parse", "--absolute-git-dir"], root)).Output.Trim();
        var journalDir = Path.Combine(gitDir, "bukit-build-transactions-v1");
        Directory.CreateDirectory(journalDir);
        var pending = Path.Combine(journalDir, ".bukit-transaction-" + identity + ".json.pending-12345678901234567890123456789012.tmp");
        await File.WriteAllTextAsync(pending, "partial");

        var run = await WorktreeBuildRun.StartAsync(ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
            new ConfigOverrides(), "text", new ConsoleLogger(LogLevel.Error), CancellationToken.None);
        try
        {
            await run.PrepareAsync(CancellationToken.None);
            Assert.Equal("partial", await File.ReadAllTextAsync(pending));
        }
        finally { await run.CleanupAsync(); }
    }

    [Fact]
    public async Task WorktreeBuild_RejectsJournalForDifferentOutput()
    {
        var root = Path.Combine(_root, "orphan-journal");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var otherOutput = PathUtils.GetCanonicalFullPath(Path.Combine(root, "other-output"));
        var identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(otherOutput)));
        var gitDir = (await RunProcessAsync("git", ["rev-parse", "--absolute-git-dir"], root)).Output.Trim();
        var journalDir = Path.Combine(gitDir, "bukit-build-transactions-v1");
        Directory.CreateDirectory(journalDir);
        var journal = Path.Combine(journalDir, ".bukit-transaction-" + identity + ".json");
        await File.WriteAllTextAsync(journal, """{"version":1,"pid":999999,"committed":false,"entries":[]}""");

        var failure = await Record.ExceptionAsync(() => WorktreeBuildRun.StartAsync(
            ConfigLoader.Load(site), new ResolvedConfigPath(site, root), new ConfigOverrides(), "text",
            new ConsoleLogger(LogLevel.Error), CancellationToken.None));

        Assert.IsType<IOException>(failure);
        Assert.Contains("different output", failure.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(journal));
    }

    [Fact]
    public async Task ActiveWorktreeBuild_BlocksInProcessBuildUsingSameOutput()
    {
        var root = Path.Combine(_root, "worktree-resource-conflict");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var config = ConfigLoader.Load(site);
        var logger = new ConsoleLogger(LogLevel.Error);
        var run = await WorktreeBuildRun.StartAsync(config, new ResolvedConfigPath(site, root),
            new ConfigOverrides(), "text", logger, CancellationToken.None);
        try
        {
            var engine = new SiteEngine(logger);
            var single = config with { Site = config.Site with { Languages = [] } };
            var error = await Assert.ThrowsAsync<IOException>(() => engine.BuildAsync(single, root, new ConfigOverrides()));
            Assert.Contains("Build resource is busy", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            await run.CleanupAsync();
        }
    }

    [Theory]
    [InlineData("commit")]
    [InlineData("conflict")]
    [InlineData("prepare")]
    [InlineData("journal")]
    [InlineData("cache")]
    [InlineData("cross")]
    [InlineData("metrics")]
    [InlineData("metrics-cross")]
    public async Task NextBuild_RecoversOutputAfterCommitProcessDies(string phase)
    {
        var root = Path.Combine(_root, "crashed-worktree-commit");
        var site = CreateSite(root, extraIgnore: "/reports/\n");
        await InitializeRepositoryAsync(root);
        Directory.CreateDirectory(Path.Combine(root, "dist"));
        await File.WriteAllTextAsync(Path.Combine(root, "dist", "old.txt"), "old");
        var staging = Path.Combine(root, ".dist.bukit-staging-12345678901234567890123456789012");
        if (phase is "commit" or "conflict" or "cache" or "cross" or "metrics" or "metrics-cross")
        {
            Directory.CreateDirectory(staging);
            await File.WriteAllTextAsync(Path.Combine(staging, "old.txt"), "new");
        }
        if (phase is "cache" or "metrics" or "metrics-cross")
        {
            Directory.CreateDirectory(Path.Combine(root, ".cache"));
            await File.WriteAllTextAsync(Path.Combine(root, ".cache", "old.txt"), "cache-old");
            var cacheStage = Path.Combine(root, "..cache.bukit-staging-12345678901234567890123456789012");
            Directory.CreateDirectory(cacheStage);
            await File.WriteAllTextAsync(Path.Combine(cacheStage, "old.txt"), "cache-new");
        }
        var metrics = Path.Combine(root, "reports", "stats.json");
        if (phase is "metrics" or "metrics-cross")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(metrics)!);
            await File.WriteAllTextAsync(metrics, "metrics-old");
            var repository = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
            var metricsStage = Path.Combine(WorktreeBuildPaths.RunsRoot(repository), "12345678901234567890123456789012", "metrics", "stats.json");
            Directory.CreateDirectory(Path.GetDirectoryName(metricsStage)!);
            await File.WriteAllTextAsync(metricsStage, "metrics-new");
        }
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(WorktreeBuildCoordinatorTests).Assembly.Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName~WorktreeCommitCrashChildProbe");
        start.Environment["BUKIT_TEST_WORKTREE_CRASH_ROOT"] = root;
        start.Environment["BUKIT_TEST_WORKTREE_CRASH_PHASE"] = phase;
        using var child = Process.Start(start)!;
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch { child.Kill(true); throw; }
        Assert.True(child.ExitCode != 0, await stdout + await stderr);
        if (phase is "commit" or "conflict" or "cross") Assert.False(Directory.Exists(Path.Combine(root, "dist")));

        if (phase == "conflict")
        {
            var config = ConfigLoader.Load(site);
            var source = Assert.Single(config.Content.Sources!);
            config = config with
            {
                Content = config.Content with { Sources = [source with { Markdown = source.Markdown! with { Dir = "dist" } }] }
            };
            var failure = await Record.ExceptionAsync(() => WorktreeBuildRun.StartAsync(config,
                new ResolvedConfigPath(site, root), new ConfigOverrides(), "text", new ConsoleLogger(LogLevel.Error), CancellationToken.None));
            Assert.NotNull(failure);
            Assert.False(Directory.Exists(Path.Combine(root, "dist")));
            Assert.Contains("content source", failure.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(root,
                ".dist.bukit-backup-12345678901234567890123456789012", "old.txt")));
            return;
        }
        var pendingJournal = phase == "journal"
            ? Assert.Single(Directory.EnumerateFiles(root, ".bukit-transaction-*.json.pending-*.tmp", SearchOption.AllDirectories))
            : null;

        if (phase is "cross" or "metrics-cross")
        {
            var config = ConfigLoader.Load(site);
            var single = config with { Site = config.Site with { Languages = [] } };
            await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(single, root,
                new ConfigOverrides { MetricsPath = phase == "metrics-cross" ? metrics : null });
            Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(root, "dist", "old.txt")));
            if (pendingJournal is not null) Assert.Equal("partial", await File.ReadAllTextAsync(pendingJournal));
            return;
        }

        WorktreeBuildRun? run = null;
        try
        {
            var failure = await Record.ExceptionAsync(async () => run = await WorktreeBuildRun.StartAsync(
                ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
                new ConfigOverrides { MetricsPath = phase == "metrics" ? metrics : null }, "text",
                new ConsoleLogger(LogLevel.Error), CancellationToken.None));
            Assert.True(failure is null, failure?.ToString());
            Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(root, "dist", "old.txt")));
            if (phase is "cache" or "metrics") Assert.Equal("cache-old", await File.ReadAllTextAsync(Path.Combine(root, ".cache", "old.txt")));
            if (phase == "metrics") Assert.Equal("metrics-old", await File.ReadAllTextAsync(metrics));
        }
        finally
        {
            if (run is not null) await run.CleanupAsync();
        }
    }

    [Fact]
    public async Task WorktreeCommitCrashChildProbe()
    {
        var root = Environment.GetEnvironmentVariable("BUKIT_TEST_WORKTREE_CRASH_ROOT");
        if (root is null) return;
        var crashPhase = Environment.GetEnvironmentVariable("BUKIT_TEST_WORKTREE_CRASH_PHASE");
        if (crashPhase is "prepare" or "journal")
        {
            var site = Path.Combine(root, "site.yaml");
            var run = await WorktreeBuildRun.StartAsync(ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
                new ConfigOverrides(), "text", crashPhase == "prepare" ? new FailFastOnContentLoadLogger() : new ConsoleLogger(LogLevel.Error), CancellationToken.None);
            await run.PrepareAsync(CancellationToken.None);
            if (crashPhase == "journal")
            {
                var gitDirResult = await RunProcessAsync("git", ["rev-parse", "--absolute-git-dir"], root);
                Assert.Equal(0, gitDirResult.ExitCode);
                var journal = Assert.Single(Directory.EnumerateFiles(gitDirResult.Output.Trim(), ".bukit-transaction-*.json", SearchOption.AllDirectories));
                File.WriteAllText(journal + ".pending-12345678901234567890123456789012.tmp", "partial");
                Environment.FailFast("test interruption during journal update");
            }
            throw new InvalidOperationException("Expected the child process to terminate during preparation.");
        }
        var final = Path.Combine(root, "dist");
        var staging = Path.Combine(root, ".dist.bukit-staging-12345678901234567890123456789012");
        var backup = Path.Combine(root, ".dist.bukit-backup-12345678901234567890123456789012");
        var phase = Environment.GetEnvironmentVariable("BUKIT_TEST_WORKTREE_CRASH_PHASE");
        var directories = new List<(string Staging, string Final, string Backup)> { (staging, final, backup) };
        if (phase is "cache" or "metrics" or "metrics-cross")
            directories.Add((Path.Combine(root, "..cache.bukit-staging-12345678901234567890123456789012"),
                Path.Combine(root, ".cache"), Path.Combine(root, "..cache.bukit-backup-12345678901234567890123456789012")));
        var files = new List<(string Staging, string Final, string Backup)>();
        if (phase is "metrics" or "metrics-cross")
        {
            var repository = PathUtils.GetCanonicalFullPath(root);
            var metricsStage = Path.Combine(WorktreeBuildPaths.RunsRoot(repository), "12345678901234567890123456789012", "metrics", "stats.json");
            files.Add((metricsStage, Path.Combine(root, "reports", "stats.json"), Path.Combine(root, "reports", ".stats.json.bukit-backup")));
        }
        WorktreeBuildTransaction.CommitDirectories(directories, files,
            (target, _, _) =>
            {
                if (phase is "commit" or "conflict" or "cross" || phase == "cache" && target.EndsWith(Path.DirectorySeparatorChar + ".cache", StringComparison.Ordinal) ||
                    phase is "metrics" or "metrics-cross" && target.EndsWith(Path.DirectorySeparatorChar + "stats.json", StringComparison.Ordinal))
                    Environment.FailFast("test interruption after worktree backup move");
            });
    }

    [Fact]
    public async Task Prepare_RejectsMediaChangedAfterCacheWasCopied()
    {
        var root = Path.Combine(_root, "media-cache-copy-race");
        var site = CreateSite(root);
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace("content:\n  sources:",
            "content:\n  media:\n    downloadToLocal: true\n    downloadDir: .cache/media\n  sources:", StringComparison.Ordinal));
        var media = Path.Combine(root, ".cache", "media");
        Directory.CreateDirectory(media);
        var image = Path.Combine(media, "photo.png");
        await File.WriteAllTextAsync(image, "original");
        await InitializeRepositoryAsync(root);
        var run = await WorktreeBuildRun.StartAsync(ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
            new ConfigOverrides(), "text", new ConsoleLogger(LogLevel.Error), CancellationToken.None);
        var repository = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
        using var watcher = new FileSystemWatcher(WorktreeBuildPaths.RunsRoot(repository), "owned-worktrees.txt")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.Created += (_, _) =>
        {
            try { File.WriteAllText(image, "user change"); changed.TrySetResult(); }
            catch (Exception ex) { changed.TrySetException(ex); }
        };
        try
        {
            var failure = await Record.ExceptionAsync(() => run.PrepareAsync(CancellationToken.None));
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsType<IOException>(failure);
            Assert.Contains("Media directory changed", failure.Message, StringComparison.Ordinal);
            Assert.Equal("user change", await File.ReadAllTextAsync(image));
        }
        finally
        {
            await run.CleanupAsync();
        }
    }

    [Fact]
    public async Task Prepare_CustomMediaInsideSymlinkedCache_UsesStagedCopy()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_root, "media-cache-alias");
        var site = CreateSite(root, extraIgnore: "/real-cache/\n/cache-link\n");
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace("content:\n  sources:",
            "content:\n  media:\n    downloadToLocal: true\n    downloadDir: real-cache/media\n  sources:", StringComparison.Ordinal));
        var media = Path.Combine(root, "real-cache", "media");
        Directory.CreateDirectory(media);
        await File.WriteAllTextAsync(Path.Combine(media, "photo.png"), "original");
        var cacheLink = Path.Combine(root, "cache-link");
        Directory.CreateSymbolicLink(cacheLink, Path.Combine(root, "real-cache"));
        await InitializeRepositoryAsync(root);
        var run = await WorktreeBuildRun.StartAsync(ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
            new ConfigOverrides { CacheDir = cacheLink }, "text", new ConsoleLogger(LogLevel.Error), CancellationToken.None);
        try
        {
            await run.PrepareAsync(CancellationToken.None);
            var repository = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
            var request = Assert.Single(Directory.GetFiles(WorktreeBuildPaths.RunsRoot(repository), "request.v1.json", SearchOption.AllDirectories),
                path => path.Contains(Path.DirectorySeparatorChar + "en" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(request));
            var staged = json.RootElement.GetProperty("mediaDownloadDir").GetString()!;
            Assert.NotEqual(PathUtils.GetCanonicalFullPath(media), PathUtils.GetCanonicalFullPath(staged));
            await File.WriteAllTextAsync(Path.Combine(media, "photo.png"), "changed");
            Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(staged, "photo.png")));
        }
        finally
        {
            await run.CleanupAsync();
        }
        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site, "--cache-dir", cacheLink], root);
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.NotNull(new DirectoryInfo(cacheLink).LinkTarget);
        Assert.Equal("changed", await File.ReadAllTextAsync(Path.Combine(media, "photo.png")));
    }

    [Fact]
    public void OutputCommit_ValidationAfterBackup_RestoresOriginalOnConflict()
    {
        var final = Path.Combine(_root, "media-final");
        var staging = Path.Combine(_root, "media-staging");
        var backup = Path.Combine(_root, "media-backup");
        Directory.CreateDirectory(final);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(final, "photo"), "user change");
        File.WriteAllText(Path.Combine(staging, "photo"), "snapshot");

        Assert.Throws<IOException>(() => WorktreeBuildTransaction.CommitDirectories(
            [(staging, final, backup)], [],
            (_, movedBackup, _) =>
            {
                Assert.Equal("user change", File.ReadAllText(Path.Combine(movedBackup, "photo")));
                throw new IOException("media changed");
            }));
        Assert.Equal("user change", File.ReadAllText(Path.Combine(final, "photo")));
        Assert.Equal("snapshot", File.ReadAllText(Path.Combine(staging, "photo")));
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_RejectsChangedSymlinkedMediaInsideRepository()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_root, "shared-media");
        var siteRoot = Path.Combine(root, "site");
        var site = CreateSite(siteRoot);
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace("content:\n  sources:", "content:\n  media:\n    downloadDir: media\n  sources:", StringComparison.Ordinal));
        var media = Path.Combine(root, "shared", "media");
        Directory.CreateDirectory(media);
        Directory.CreateSymbolicLink(Path.Combine(siteRoot, "media"), "../shared/media");
        var image = Path.Combine(media, "photo.png");
        await File.WriteAllTextAsync(image, "old");
        ConfigValidator.Validate(ConfigLoader.Load(site));
        await InitializeRepositoryAsync(root);
        await File.WriteAllTextAsync(image, "new");

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(() => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-dirty", error.Code);
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_RejectsSymlinkedMediaOutsideRepository()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_root, "external-media-site");
        var site = CreateSite(root);
        var media = Path.Combine(_root, "external-media");
        Directory.CreateDirectory(media);
        Directory.CreateSymbolicLink(Path.Combine(root, "media"), media);
        var yaml = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, yaml.Replace("content:\n  sources:", "content:\n  media:\n    downloadDir: media\n  sources:", StringComparison.Ordinal));
        ConfigValidator.Validate(ConfigLoader.Load(site));
        await InitializeRepositoryAsync(root);

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(() => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-input-untracked", error.Code);
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_RejectsChangedMediaInsideSite()
    {
        var root = Path.Combine(_root, "changed-media");
        var siteRoot = Path.Combine(root, "site");
        var site = CreateSite(siteRoot);
        var config = await File.ReadAllTextAsync(site);
        await File.WriteAllTextAsync(site, config.Replace("content:\n  sources:",
            "content:\n  media:\n    downloadDir: media\n  sources:", StringComparison.Ordinal));
        var mediaDir = Path.Combine(siteRoot, "media");
        Directory.CreateDirectory(mediaDir);
        var image = Path.Combine(mediaDir, "photo.png");
        await File.WriteAllTextAsync(image, "old image");
        await InitializeRepositoryAsync(root);
        await File.WriteAllTextAsync(image, "changed image");

        var error = await Assert.ThrowsAsync<WorktreeBuildException>(() => BuildCommand.RunAsync(Command(site)));

        Assert.Equal("i18n-worktree-dirty", error.Code);
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

    [Theory]
    [InlineData("direct")]
    [InlineData("chain")]
    [InlineData("ancestor")]
    public async Task ExplicitMultilingualBuild_RejectsAbsoluteInputSymlink(string kind)
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_root, "absolute-input-link");
        var site = CreateSite(root);
        var content = Path.Combine(root, "content");
        var real = Path.Combine(root, "real-content");
        Directory.Move(content, real);
        if (kind == "ancestor")
        {
            var nested = Path.Combine(real, "nested");
            Directory.CreateDirectory(nested);
            Directory.Move(Path.Combine(real, "en"), Path.Combine(nested, "en"));
            Directory.Move(Path.Combine(real, "zh"), Path.Combine(nested, "zh"));
            var yaml = await File.ReadAllTextAsync(site);
            await File.WriteAllTextAsync(site, yaml.Replace("dir: content", "dir: content/nested", StringComparison.Ordinal));
        }
        if (kind == "chain")
        {
            Directory.CreateSymbolicLink(Path.Combine(root, "content-alias"), real);
            Directory.CreateSymbolicLink(content, "content-alias");
        }
        else Directory.CreateSymbolicLink(content, real);
        await InitializeRepositoryAsync(root);
        WorktreeBuildRun? run = null;
        try
        {
            var failure = await Record.ExceptionAsync(async () => run = await WorktreeBuildRun.StartAsync(
                ConfigLoader.Load(site), new ResolvedConfigPath(site, root), new ConfigOverrides(), "text",
                new ConsoleLogger(LogLevel.Error), CancellationToken.None));
            var error = Assert.IsType<WorktreeBuildException>(failure);
            Assert.Equal("i18n-worktree-input-untracked", error.Code);
        }
        finally
        {
            if (run is not null) await run.CleanupAsync();
        }
    }

    [Fact]
    public async Task WorktreePreflight_AllowsRelativeInputSymlink()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_root, "relative-input-link");
        var site = CreateSite(root);
        Directory.Move(Path.Combine(root, "content"), Path.Combine(root, "real-content"));
        Directory.CreateSymbolicLink(Path.Combine(root, "content"), "real-content");
        await InitializeRepositoryAsync(root);

        var run = await WorktreeBuildRun.StartAsync(ConfigLoader.Load(site), new ResolvedConfigPath(site, root),
            new ConfigOverrides(), "text", new ConsoleLogger(LogLevel.Error), CancellationToken.None);
        await run.CleanupAsync();
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
    public async Task InProcessBuild_RootAndLanguageDirectoryCollision_BuildsBothPages()
    {
        var root = Path.Combine(_root, "same-language-slugs");
        var site = CreateSite(root);
        await File.WriteAllTextAsync(Path.Combine(root, "content", "index.md"), "---\ntitle: Root\nlanguage: en\n---\nRoot body");

        var exitCode = await BuildCommand.RunInProcessAsync(Command(site));

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(root, "dist", "en", "index", "index.html")));
        Assert.True(File.Exists(Path.Combine(root, "dist", "en", "en", "index", "index.html")));
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
    public async Task WorktreeBuild_MatchesInProcessOutputs_ForRootDefaultLanguage()
    {
        var root = Path.Combine(_root, "root-layout-equivalence");
        var site = CreateSite(root);
        var path = Path.Combine(root, "site.yaml");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("  defaultLanguage: en\n", "  defaultLanguage: en\n  defaultLanguageInSubdir: false\n", StringComparison.Ordinal)
            .Replace("  sitemapMode: merged\n", "  sitemapMode: split\n", StringComparison.Ordinal)
            .Replace("  search:\n    mode: merged\n", "  search:\n    mode: split\n", StringComparison.Ordinal));
        await InitializeRepositoryAsync(root);

        Assert.Equal(0, await BuildCommand.RunInProcessAsync(Command(site)));
        var expected = SnapshotPublishedOutputs(Path.Combine(root, "dist"));
        Assert.True(File.Exists(Path.Combine(root, "dist", "index.html")));
        Assert.True(File.Exists(Path.Combine(root, "dist", "en", "index.html")));
        Assert.Contains("url=https://example.test/\"", File.ReadAllText(Path.Combine(root, "dist", "en", "index.html")), StringComparison.Ordinal);
        TestCleanup.DeleteDirectory(Path.Combine(root, "dist"), recursive: true);
        TestCleanup.DeleteDirectory(Path.Combine(root, ".cache"), recursive: true);

        var worktree = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
        Assert.True(worktree.ExitCode == 0, worktree.Output);
        Assert.Equal(expected, SnapshotPublishedOutputs(Path.Combine(root, "dist")));
        Assert.True(File.Exists(Path.Combine(root, "dist", "index.html")));
        Assert.True(File.Exists(Path.Combine(root, "dist", "zh-CN", "index.html")));
    }

    [Fact]
    public async Task WorktreeBuild_RootLayoutWithoutCacheIsNotALayoutSwitch()
    {
        var root = Path.Combine(_root, "root-layout-no-cache");
        var site = CreateSite(root);
        var path = Path.Combine(root, "site.yaml");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("  defaultLanguage: en\n", "  defaultLanguage: en\n  defaultLanguageInSubdir: false\n", StringComparison.Ordinal)
            .Replace("  sitemapMode: merged\n", "  sitemapMode: split\n", StringComparison.Ordinal)
            .Replace("  search:\n    mode: merged\n", "  search:\n    mode: split\n", StringComparison.Ordinal));
        await InitializeRepositoryAsync(root);
        Directory.CreateDirectory(Path.Combine(root, "dist"));
        File.WriteAllText(Path.Combine(root, "dist", "keep.txt"), "user file");

        var build = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);

        Assert.True(build.ExitCode == 0, build.Output);
        Assert.Equal("user file", File.ReadAllText(Path.Combine(root, "dist", "keep.txt")));
        Assert.True(File.Exists(Path.Combine(root, "dist", "index.html")));
    }

    [Fact]
    public async Task WorktreeBuild_MarkerWithoutManifestCleansPreviousOutput()
    {
        var root = Path.Combine(_root, "root-layout-marker-only");
        var site = CreateSite(root);
        var path = Path.Combine(root, "site.yaml");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("  defaultLanguage: en\n", "  defaultLanguage: en\n  defaultLanguageInSubdir: false\n", StringComparison.Ordinal)
            .Replace("  sitemapMode: merged\n", "  sitemapMode: split\n", StringComparison.Ordinal)
            .Replace("  search:\n    mode: merged\n", "  search:\n    mode: split\n", StringComparison.Ordinal));
        await InitializeRepositoryAsync(root);
        Directory.CreateDirectory(Path.Combine(root, "dist", "en", "stale"));
        File.WriteAllText(Path.Combine(root, "dist", ".bukit-output-marker"), string.Empty);
        File.WriteAllText(Path.Combine(root, "dist", "en", "stale", "index.html"), "old prefixed page");

        var build = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);

        Assert.True(build.ExitCode == 0, build.Output);
        Assert.True(File.Exists(Path.Combine(root, "dist", "index.html")));
        Assert.False(File.Exists(Path.Combine(root, "dist", "en", "stale", "index.html")));
    }

    [Fact]
    public async Task WorktreeBuild_SwitchesLayoutAndCleansPreviousOutput()
    {
        var root = Path.Combine(_root, "layout-switch");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);

        var prefixed = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
        Assert.True(prefixed.ExitCode == 0, prefixed.Output);
        Assert.True(File.Exists(Path.Combine(root, "dist", "en", "index.html")));

        var path = Path.Combine(root, "site.yaml");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("  defaultLanguage: en\n", "  defaultLanguage: en\n  defaultLanguageInSubdir: false\n", StringComparison.Ordinal)
            .Replace("  sitemapMode: merged\n", "  sitemapMode: split\n", StringComparison.Ordinal)
            .Replace("  search:\n    mode: merged\n", "  search:\n    mode: split\n", StringComparison.Ordinal));
        Assert.Equal(0, (await RunProcessAsync("git", ["add", "site.yaml"], root)).ExitCode);
        Assert.Equal(0, (await RunProcessAsync("git", ["commit", "-m", "root layout"], root)).ExitCode);
        var switched = await RunProcessAsync("dotnet", [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);
        Assert.True(switched.ExitCode == 0, switched.Output);

        Assert.True(File.Exists(Path.Combine(root, "dist", "index.html")));
        Assert.Equal(string.Empty, File.ReadAllText(Path.Combine(root, "dist", "zh-CN", "index.html")));
        Assert.True(File.Exists(Path.Combine(root, "dist", "en", "index.html")));
        Assert.Contains("url=https://example.test/\"", File.ReadAllText(Path.Combine(root, "dist", "en", "index.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteManifestRoots_UsesLayoutPrefixForRootLanguage()
    {
        var root = Path.Combine(_root, "manifest-rewrite");
        Directory.CreateDirectory(root);
        var site = new SiteConfig
        {
            Name = "manifest",
            Title = "Manifest",
            Languages = ["en", "zh-CN"],
            DefaultLanguage = "en",
            DefaultLanguageInSubdir = false
        };
        var oldRoot = Path.Combine(root, "old");
        var newRoot = Path.Combine(root, "new");
        void WriteManifest(string name, string outputRoot) =>
            File.WriteAllText(Path.Combine(root, name),
                $$"""{"version":3,"outputRoot":{{System.Text.Json.JsonSerializer.Serialize(Path.GetFullPath(outputRoot))}},"ownedOutputs":[]}""");

        WriteManifest("build-manifest.json", oldRoot);
        WriteManifest("build-manifest.en.json", oldRoot);
        WriteManifest("build-manifest.zh-CN.json", Path.Combine(oldRoot, "zh-CN"));

        WorktreeBuildPaths.RewriteManifestRoots(root, oldRoot, newRoot, ["en", "zh-CN"], site);

        static string OutputRoot(string path)
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.GetProperty("outputRoot").GetString()!;
        }

        Assert.Equal(Path.GetFullPath(newRoot), OutputRoot(Path.Combine(root, "build-manifest.en.json")));
        Assert.Equal(Path.GetFullPath(Path.Combine(newRoot, "zh-CN")), OutputRoot(Path.Combine(root, "build-manifest.zh-CN.json")));
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

        Assert.Throws<DirectoryNotFoundException>(() => WorktreeBuildTransaction.CommitDirectories(
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

        WorktreeBuildPaths.PrepareStaging(source, staging, copyExisting: true);
        WorktreeBuildTransaction.CommitDirectories([(staging, source, Path.Combine(_root, "backup"))], []);

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
        WorktreeBuildTransaction.CleanupBackup(backup, isDirectory: false);

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

        WorktreeBuildTransaction.CommitDirectories([], [(staging, final, backup)]);

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
    public async Task PrepareCancellation_CleansOwnedWorktree_AndPreservesUnrelatedWorktreeAndPriorOutputCache()
    {
        var root = Path.Combine(_root, "prepare-cancellation");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var unrelated = Path.Combine(_root, "unrelated-worktree");
        Assert.Equal(0, (await RunProcessAsync("git", ["worktree", "add", "--detach", unrelated, "HEAD"], root)).ExitCode);
        var unrelatedSentinel = Path.Combine(unrelated, "sentinel.bin");
        byte[] oldUnrelated = [3, 0, 255];
        await File.WriteAllBytesAsync(unrelatedSentinel, oldUnrelated);
        var registrationBefore = (await RunProcessAsync("git", ["worktree", "list", "--porcelain"], root)).Output;
        var outputSentinel = Path.Combine(root, "dist", "sentinel.bin");
        var cacheSentinel = Path.Combine(root, ".cache", "sentinel.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(outputSentinel)!);
        Directory.CreateDirectory(Path.GetDirectoryName(cacheSentinel)!);
        byte[] oldOutput = [0, 1, 255];
        byte[] oldCache = [255, 2, 0];
        await File.WriteAllBytesAsync(outputSentinel, oldOutput);
        await File.WriteAllBytesAsync(cacheSentinel, oldCache);
        var repository = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
        var runsRoot = WorktreeBuildPaths.RunsRoot(repository);
        using var cancellation = new CancellationTokenSource();
        var logger = new CancelOnContentLoadLogger(cancellation);
        var run = await WorktreeBuildRun.StartAsync(
            ConfigLoader.Load(site), new ResolvedConfigPath(site, root), new ConfigOverrides(), "text", logger,
            cancellation.Token);
        string? ownedWorktree = null;
        string? registry = null;
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.PrepareAsync(cancellation.Token));
            Assert.True(logger.Triggered);
            registry = Assert.Single(Directory.EnumerateFiles(runsRoot, "owned-worktrees.txt", SearchOption.AllDirectories));
            ownedWorktree = Assert.Single(await File.ReadAllLinesAsync(registry));
            Assert.True(Directory.Exists(ownedWorktree));
            var registered = (await RunProcessAsync("git", ["worktree", "list", "--porcelain"], root)).Output;
            Assert.Contains(registered.Split('\n'), line =>
                line.StartsWith("worktree ", StringComparison.Ordinal) &&
                string.Equals(PathUtils.GetCanonicalFullPath(line["worktree ".Length..].TrimEnd('\r')),
                    PathUtils.GetCanonicalFullPath(ownedWorktree), PlatformPathHelper.PathComparison));
        }
        finally
        {
            await run.CleanupAsync();
        }

        Assert.NotNull(ownedWorktree);
        Assert.False(Directory.Exists(ownedWorktree));
        Assert.False(File.Exists(registry));
        Assert.Equal(registrationBefore, (await RunProcessAsync("git", ["worktree", "list", "--porcelain"], root)).Output);
        Assert.Equal(oldUnrelated, await File.ReadAllBytesAsync(unrelatedSentinel));
        Assert.Equal(oldOutput, await File.ReadAllBytesAsync(outputSentinel));
        Assert.Equal(oldCache, await File.ReadAllBytesAsync(cacheSentinel));
    }

    [Fact]
    public async Task NextBuild_ProtectsActiveRun_ThenRecoversOnlyRegisteredResidualWorktree()
    {
        var root = Path.Combine(_root, "residual-recovery");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var repository = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
        var runRoot = Path.Combine(WorktreeBuildPaths.RunsRoot(repository), "interrupted-run");
        var residualWorktree = Path.Combine(runRoot, "workers", "en");
        Directory.CreateDirectory(runRoot);
        Assert.Equal(0, (await RunProcessAsync("git", ["worktree", "add", "--detach", residualWorktree, "HEAD"], root)).ExitCode);
        await File.WriteAllTextAsync(Path.Combine(runRoot, "owned-worktrees.txt"), residualWorktree + Environment.NewLine);

        using (WorktreeBuildPaths.AcquireRunLock(repository))
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

    [Fact]
    public async Task ExplicitMultilingualBuild_CliRejectsOriginalSourceEqualToOutputWithoutMarker()
    {
        var root = Path.Combine(_root, "source-equals-output");
        var site = CreateSite(root);
        await File.WriteAllTextAsync(site, (await File.ReadAllTextAsync(site)).Replace(
            "output: dist", "output: content", StringComparison.Ordinal));
        await InitializeRepositoryAsync(root);
        var contentDir = Path.Combine(root, "content");
        var before = SnapshotHashes(contentDir);
        var repositoryRoot = (await RunProcessAsync("git", ["rev-parse", "--show-toplevel"], root)).Output.Trim();
        var runsRoot = WorktreeBuildPaths.RunsRoot(repositoryRoot);

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("content source", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, SnapshotHashes(contentDir));
        Assert.False(File.Exists(Path.Combine(contentDir, ".bukit-output-marker")));
        Assert.False(Directory.Exists(runsRoot));
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_CliRejectsSourceEqualToOutputWithMarkerAndClean()
    {
        var root = Path.Combine(_root, "source-marker-output");
        var site = CreateSite(root);
        await File.WriteAllTextAsync(site, (await File.ReadAllTextAsync(site))
            .Replace("output: dist", "output: content", StringComparison.Ordinal)
            .Replace("clean: false", "clean: true", StringComparison.Ordinal));
        await File.WriteAllTextAsync(Path.Combine(root, "content", ".bukit-output-marker"), "bukit-output\n");
        await InitializeRepositoryAsync(root);
        var contentDir = Path.Combine(root, "content");
        var before = SnapshotHashes(contentDir);

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("content source", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, SnapshotHashes(contentDir));
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_CliRejectsMediaConflictAlone()
    {
        var root = Path.Combine(_root, "media-conflict");
        var site = CreateSite(root);
        var original = (await File.ReadAllTextAsync(site)).ReplaceLineEndings("\n");
        var configured = original.Replace(
            "content:\n  sources:",
            "content:\n  media:\n    downloadToLocal: true\n    downloadDir: content/media\n  sources:",
            StringComparison.Ordinal);
        Assert.NotEqual(original, configured);
        await File.WriteAllTextAsync(site, configured);
        await InitializeRepositoryAsync(root);
        var contentDir = Path.Combine(root, "content");
        var before = SnapshotHashes(contentDir);

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site], root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("content source", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, SnapshotHashes(contentDir));
        Assert.False(Directory.Exists(Path.Combine(contentDir, "media")));
        Assert.False(Directory.Exists(Path.Combine(root, "dist")));
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_CliRejectsCacheConflictAlone()
    {
        var root = Path.Combine(_root, "cache-conflict");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var contentDir = Path.Combine(root, "content");
        var before = SnapshotHashes(contentDir);

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site,
                "--cache-dir", Path.Combine(contentDir, "cache")], root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("content source", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, SnapshotHashes(contentDir));
        Assert.False(Directory.Exists(Path.Combine(contentDir, "cache")));
        Assert.False(Directory.Exists(Path.Combine(root, "dist")));
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_CliRejectsMetricsConflictAlone()
    {
        var root = Path.Combine(_root, "metrics-conflict");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);
        var contentDir = Path.Combine(root, "content");
        var before = SnapshotHashes(contentDir);

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site,
                "--metrics", Path.Combine(contentDir, "stats.json")], root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("content source", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, SnapshotHashes(contentDir));
        Assert.False(File.Exists(Path.Combine(contentDir, "stats.json")));
        Assert.False(File.Exists(Path.Combine(contentDir, "stats.html")));
        Assert.False(Directory.Exists(Path.Combine(root, "dist")));
    }

    [Fact]
    public async Task ExplicitMultilingualBuild_CliSafeTargetsWithMetricsComplete()
    {
        var root = Path.Combine(_root, "independent-targets");
        var site = CreateSite(root);
        await InitializeRepositoryAsync(root);

        var result = await RunProcessAsync("dotnet",
            [typeof(BuildCommand).Assembly.Location, "build", "--config", site,
                "--metrics", Path.Combine(root, "reports", "stats.json")], root);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(root, "dist", "en", "index.html")));
        Assert.True(File.Exists(Path.Combine(root, "reports", "stats.json")));
        Assert.True(File.Exists(Path.Combine(root, "reports", "stats.html")));
    }

    private static string SnapshotHashes(string directory)
        => string.Join("\n", Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(directory, path) + ":" +
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));

    private sealed class CancelOnContentLoadLogger(CancellationTokenSource cancellation) : ILogger
    {
        public bool Triggered { get; private set; }
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }

        public void Info(string message)
        {
            if (message.StartsWith("event=content.stage stage=ContentLoad ", StringComparison.Ordinal))
            {
                Triggered = true;
                cancellation.Cancel();
            }
        }
    }

    private sealed class FailFastOnContentLoadLogger : ILogger
    {
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Info(string message)
        {
            if (message.StartsWith("event=content.stage stage=ContentLoad ", StringComparison.Ordinal))
                Environment.FailFast("test interruption during worktree preparation");
        }
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
              # Serialize workers: concurrent instrumented child processes would
              # contend on coverlet's per-module hits file during coverage runs.
              languageJobs: 1
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
