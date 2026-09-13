using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-transaction-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _logger = new ConsoleLogger(LogLevel.Error);
    private readonly string? _originalNotionToken = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);
    private AppConfig Config => new() { Site = new() { Name = "test", Title = "Test" }, Content = new() { Sources = [new() { Type = "markdown", Markdown = new() }] }, Build = new() { Output = "public", Clean = false } };
    public BuildTransactionTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "public"));
        File.WriteAllText(Path.Combine(_root, "public", "old.txt"), "old");
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, "transaction-test-token");
    }

    [Fact]
    public void FailureAndCancellationLeaveTargetsUnchanged()
    {
        var metrics = Path.Combine(_root, "stats.json");
        File.WriteAllText(metrics, "old-json"); File.WriteAllText(Path.ChangeExtension(metrics, ".html"), "old-html");
        using (var transaction = BuildTransaction.Begin(Config, _root, new() { MetricsPath = metrics }, _logger))
        {
            File.WriteAllText(Path.Combine(transaction.OutputDir, "old.txt"), "new");
            File.WriteAllText(BuildTransaction.Physical(metrics), "new-json");
            File.WriteAllText(BuildTransaction.Physical(Path.ChangeExtension(metrics, ".html")), "new-html");
            Assert.Throws<OperationCanceledException>(() => transaction.Commit(new CancellationToken(true)));
        }
        Assert.Equal("old", File.ReadAllText(Path.Combine(_root, "public", "old.txt")));
        Assert.Equal("old-json", File.ReadAllText(metrics)); Assert.Equal("old-html", File.ReadAllText(Path.ChangeExtension(metrics, ".html")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root, ".bukit-txn-*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwapFailureRollsBackAndPreservesBackupWhenRecoveryFails(bool recoveryFails)
    {
        Directory.CreateDirectory(Path.Combine(_root, ".cache")); File.WriteAllText(Path.Combine(_root, ".cache", "old"), "cache-old");
        void Move(string from, string to, bool directory)
        {
            if (from.Contains("-stage") && to.EndsWith(".cache") || recoveryFails && from.Contains("-backup")) throw new IOException("injected swap failure");
            if (directory) Directory.Move(from, to); else File.Move(from, to);
        }
        using (var transaction = BuildTransaction.Begin(Config, _root, new(), _logger, move: Move))
        {
            File.WriteAllText(Path.Combine(transaction.CacheDir, "old"), "new");
            File.WriteAllText(Path.Combine(transaction.OutputDir, "old.txt"), "output-new");
            Assert.Throws<AggregateException>(() => transaction.Commit(default));
        }
        if (recoveryFails)
        {
            var outputBackup = Assert.Single(Directory.EnumerateDirectories(_root, ".bukit-txn-*-backup"), path => File.Exists(Path.Combine(path, "old.txt")));
            Assert.Equal("old", File.ReadAllText(Path.Combine(outputBackup, "old.txt")));
        }
        else { Assert.Equal("old", File.ReadAllText(Path.Combine(_root, "public", "old.txt"))); Assert.Equal("cache-old", File.ReadAllText(Path.Combine(_root, ".cache", "old"))); }
    }

    [Fact]
    public void CommitPreservesAbsoluteLinksAcrossTargetsAndWithinOutput()
    {
        var cacheFile = Path.Combine(_root, ".cache", "manual.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        File.WriteAllText(cacheFile, "cache-manual");
        var output = Path.Combine(_root, "public");
        File.CreateSymbolicLink(Path.Combine(output, "cache-link"), cacheFile);
        File.CreateSymbolicLink(Path.Combine(output, "output-link"), Path.Combine(output, "old.txt"));
        using (var transaction = BuildTransaction.Begin(Config, _root, new(), _logger))
        {
            File.WriteAllText(Path.Combine(transaction.OutputDir, "old.txt"), "output-new");
            transaction.Commit(default);
        }
        Assert.Equal("cache-manual", File.ReadAllText(Path.Combine(output, "cache-link")));
        Assert.Equal("output-new", File.ReadAllText(Path.Combine(output, "output-link")));
        Assert.Equal(cacheFile, new FileInfo(Path.Combine(output, "cache-link")).LinkTarget);
        Assert.Equal("old.txt", new FileInfo(Path.Combine(output, "output-link")).LinkTarget);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root, ".bukit-txn-*"));
    }

    [Fact]
    public void CleanupFailureDoesNotUndoCommit()
    {
        void Delete(string path, bool directory) { if (path.EndsWith("-backup")) throw new IOException("cleanup failure"); if (directory) Directory.Delete(path, true); else File.Delete(path); }
        using (var transaction = BuildTransaction.Begin(Config, _root, new(), _logger, delete: Delete))
        {
            File.WriteAllText(Path.Combine(transaction.OutputDir, "old.txt"), "new"); transaction.Commit(default);
        }
        Assert.Equal("new", File.ReadAllText(Path.Combine(_root, "public", "old.txt")));
    }

    [Theory]
    [InlineData("public", true, "public/en", true)]
    [InlineData(".cache/notion", true, ".cache", true)]
    [InlineData("public", true, "public/stats.html", false)]
    [InlineData("stats.html", false, "stats.html", false)]
    public void HierarchicalConflictAndRelease(string first, bool firstDir, string second, bool secondDir)
    {
        var resource = new BuildResourceLease.Resource(BuildResourceLease.Canonical(Path.Combine(_root, first)), firstDir, true);
        var other = new BuildResourceLease.Resource(BuildResourceLease.Canonical(Path.Combine(_root, second)), secondDir, true);
        using (BuildResourceLease.Acquire([resource])) Assert.Throws<IOException>(() => BuildResourceLease.Acquire([other]));
        using var released = BuildResourceLease.Acquire([other]);
    }

    [Fact]
    public void ReadOnlyReadersCoexistButWritersConflictInBothDirections()
    {
        var parent = new BuildResourceLease.Resource(BuildResourceLease.Canonical(Path.Combine(_root, "cache")), true, false);
        var child = new BuildResourceLease.Resource(Path.Combine(parent.Path, "notion"), true, false);
        using var first = BuildResourceLease.Acquire([parent]);
        using var second = BuildResourceLease.Acquire([child]);
        Assert.Throws<IOException>(() => BuildResourceLease.Acquire([child with { Write = true }]));
        Assert.Throws<IOException>(() => BuildResourceLease.Acquire([parent with { Write = true }]));
        using var independent = BuildResourceLease.Acquire([new(Path.Combine(_root, "independent"), true, true)]);
    }

    [Fact]
    public void AliasWithMissingSuffixConflicts()
    {
        var real = Path.Combine(_root, "real"); Directory.CreateDirectory(real);
        Directory.CreateSymbolicLink(Path.Combine(_root, "alias"), real);
        var path = BuildResourceLease.Canonical(Path.Combine(real, "missing"));
        var alias = BuildResourceLease.Canonical(Path.Combine(_root, "alias", "missing", "child"));
        using var first = BuildResourceLease.Acquire([new(path, true, true)]);
        Assert.Throws<IOException>(() => BuildResourceLease.Acquire([new(alias, true, true)]));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void NotionAndMediaWriteSetCommitsTogetherOrPreservesOldBytes(bool customNotion, bool commit, bool independentEngineCache)
    {
        var notion = Path.Combine(_root, customNotion ? "custom-notion" : ".cache/notion");
        var media = Path.Combine(_root, "downloads");
        Directory.CreateDirectory(notion); Directory.CreateDirectory(media);
        File.WriteAllText(Path.Combine(notion, "old"), "notion-old"); File.WriteAllText(Path.Combine(media, "old"), "media-old");
        var config = Config with { Content = Config.Content with { Sources = [new() { Type = "notion", Notion = new() { DatabaseId = "db", CacheMode = "readwrite", CacheDir = customNotion ? notion : null } }], Media = Config.Content.Media with { DownloadDir = "downloads" } } };
        var cache = Path.Combine(_root, ".cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "unrelated"), "keep-me");
        var cacheInstalls = 0;
        void Move(string from, string to, bool directory)
        {
            if (from.EndsWith("-stage") && BuildResourceLease.Same(to, BuildResourceLease.Canonical(cache))) cacheInstalls++;
            if (directory) Directory.Move(from, to); else File.Move(from, to);
        }
        using (var transaction = BuildTransaction.Begin(config, _root, new() { CacheDir = independentEngineCache ? Path.Combine(_root, "engine-cache") : null }, _logger, move: Move))
        {
            File.WriteAllText(Path.Combine(BuildTransaction.Physical(notion), "old"), "notion-new");
            var effective = ContentProviderFactory.BuildEffectiveMediaConfig(config.Content.Media, _root, transaction.MediaCacheDir);
            Assert.NotEqual(media, effective.DownloadDir);
            File.WriteAllText(Path.Combine(effective.DownloadDir, "old"), "media-new");
            Assert.Equal("notion-old", File.ReadAllText(Path.Combine(notion, "old")));
            Assert.Equal("media-old", File.ReadAllText(Path.Combine(media, "old")));
            if (commit) transaction.Commit(default);
        }
        Assert.Equal(commit ? "notion-new" : "notion-old", File.ReadAllText(Path.Combine(notion, "old")));
        Assert.Equal(commit ? "media-new" : "media-old", File.ReadAllText(Path.Combine(media, "old")));
        Assert.Equal("keep-me", File.ReadAllText(Path.Combine(cache, "unrelated")));
        Assert.Equal(commit && !independentEngineCache ? 1 : 0, cacheInstalls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void ReadonlyCacheRemainsMissingOrPreservesNestedSubtree(bool nested, bool populated, bool corruptStaging)
    {
        var path = Path.Combine(_root, nested ? ".cache/notion" : "readonly");
        if (populated) { Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, "cached.json"), "readonly-old"); }
        var config = Config with { Content = Config.Content with { Sources = [new() { Type = "notion", Notion = new() { DatabaseId = "db", CacheMode = "readonly", CacheDir = path } }] } };
        using (var transaction = BuildTransaction.Begin(config, _root, new(), _logger))
        {
            var mapped = BuildTransaction.Physical(path);
            Assert.Equal(populated, Directory.Exists(mapped));
            if (populated) Assert.Equal("readonly-old", File.ReadAllText(Path.Combine(mapped, "cached.json")));
            if (corruptStaging)
            {
                Assert.NotEqual(path, mapped);
                File.WriteAllText(Path.Combine(mapped, "cached.json"), "forbidden-change");
                Assert.Contains("Read-only cache was modified", Assert.Throws<IOException>(() => transaction.Commit(default)).Message);
            }
            else transaction.Commit(default);
        }
        Assert.Equal(populated, Directory.Exists(path));
        if (populated) Assert.Equal("readonly-old", File.ReadAllText(Path.Combine(path, "cached.json")));
    }

    [Theory]
    [InlineData("public", true, "public/en", true)]
    [InlineData(".cache/notion", true, ".cache", true)]
    [InlineData("public", true, "public/stats.html", false)]
    [InlineData("stats.html", false, "stats.html", false)]
    public async Task SeparateProcessCannotClaimOverlappingResource(string first, bool directory, string second, bool otherDirectory)
    {
        using var held = BuildResourceLease.Acquire([new(BuildResourceLease.Canonical(Path.Combine(_root, first)), directory, true)]);
        var result = Path.Combine(_root, "child-result");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("vstest"); start.ArgumentList.Add(typeof(BuildTransactionTests).Assembly.Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName~ResourceLeaseChildProbe");
        start.Environment["BUKIT_TEST_LEASE_PATH"] = Path.Combine(_root, second);
        start.Environment["BUKIT_TEST_LEASE_DIRECTORY"] = otherDirectory.ToString();
        start.Environment["BUKIT_TEST_LEASE_RESULT"] = result;
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await stdout + await stderr);
        Assert.Equal("busy", File.ReadAllText(result));
    }

    [Fact]
    public void ResourceLeaseChildProbe()
    {
        var path = Environment.GetEnvironmentVariable("BUKIT_TEST_LEASE_PATH");
        if (path is null) return;
        var result = Environment.GetEnvironmentVariable("BUKIT_TEST_LEASE_RESULT")!;
        try
        {
            using var lease = BuildResourceLease.Acquire([new(BuildResourceLease.Canonical(path), bool.Parse(Environment.GetEnvironmentVariable("BUKIT_TEST_LEASE_DIRECTORY")!), true)]);
            File.WriteAllText(result, "acquired");
        }
        catch (IOException) { File.WriteAllText(result, "busy"); }
    }

    [Fact]
    public void FailureDoesNotCreateMissingCacheOrMetricsAncestors()
    {
        var parent = Path.Combine(_root, "new");
        using (BuildTransaction.Begin(Config, _root, new() { CacheDir = Path.Combine(parent, "cache"), MetricsPath = Path.Combine(parent, "reports", "stats.json") }, _logger))
            Assert.False(Directory.Exists(parent));
        Assert.False(Directory.Exists(parent));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root, ".bukit-txn-*"));
    }

    [Fact]
    public void FileAncestorConflictIsRejectedBeforeCreatingAnyTarget()
    {
        var parent = Path.Combine(_root, "new");
        Assert.Throws<IOException>(() => BuildTransaction.Begin(Config, _root, new() { CacheDir = Path.Combine(parent, "cache"), MetricsPath = parent }, _logger));
        Assert.False(Directory.Exists(parent));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root, ".bukit-txn-*"));
    }

    [Fact]
    public void NewOutputAndCacheParentCommitsOnceWithFormalOutputIdentity()
    {
        var parent = Path.Combine(_root, "new");
        var output = Path.Combine(parent, "public");
        using (var transaction = BuildTransaction.Begin(Config with { Build = Config.Build with { Output = "new/public" } }, _root, new() { CacheDir = Path.Combine(parent, "cache") }, _logger))
        {
            Assert.Equal(output, BuildTransaction.Logical(transaction.OutputDir));
            Assert.False(Directory.Exists(parent));
            Directory.CreateDirectory(transaction.OutputDir); Directory.CreateDirectory(transaction.CacheDir);
            File.WriteAllText(Path.Combine(transaction.OutputDir, "page"), "output");
            File.WriteAllText(Path.Combine(transaction.CacheDir, "manifest"), "cache");
            transaction.Commit(default);
        }
        Assert.Equal("output", File.ReadAllText(Path.Combine(output, "page")));
        Assert.Equal("cache", File.ReadAllText(Path.Combine(parent, "cache", "manifest")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root, ".bukit-txn-*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedBuildCannotReclassifySharedFormalResourceAsParentStaging(bool pagesCache)
    {
        var config = Config with { Content = Config.Content with { Media = Config.Content.Media with { DownloadDir = "downloads", DownloadToLocal = !pagesCache } } };
        if (pagesCache) config = config with
        {
            Content = config.Content with { Sources = [new() { Type = "notion", Notion = new() { DatabaseId = "db", CacheMode = "off" } }] },
            Theme = config.Theme with { Params = new Dictionary<string, object> { ["pages_index"] = new Dictionary<string, object> { ["resolve_notion"] = new Dictionary<string, object> { ["enabled"] = true, ["field_keys"] = new[] { "Related" }, ["cache_mode"] = "readwrite", ["cache_path"] = "pages-cache.json" } } } }
        };
        using var outer = BuildTransaction.Begin(config, _root, new() { CacheDir = Path.Combine(_root, "outer-cache") }, _logger);
        var exception = Record.Exception(() =>
        {
            using var inner = BuildTransaction.Begin(config with { Build = config.Build with { Output = "other" } }, _root, new() { CacheDir = Path.Combine(_root, "inner-cache") }, _logger);
        });
        Assert.IsType<IOException>(exception);
        Assert.Contains("busy", exception.Message);
    }

    [Fact]
    public void NestedDisjointBuildRetainsItsOwnMappingAndRestoresParentScope()
    {
        using var outer = BuildTransaction.Begin(Config with { Content = Config.Content with { Media = Config.Content.Media with { DownloadDir = "outer-downloads" } } }, _root, new() { CacheDir = Path.Combine(_root, "outer-cache") }, _logger);
        using (var inner = BuildTransaction.Begin(Config with { Build = Config.Build with { Output = "other" }, Content = Config.Content with { Media = Config.Content.Media with { DownloadDir = "inner-downloads" } } }, _root, new() { CacheDir = Path.Combine(_root, "inner-cache") }, _logger))
        {
            Assert.Equal(Path.Combine(_root, "other"), BuildTransaction.Logical(inner.OutputDir));
            Assert.NotEqual(outer.OutputDir, inner.OutputDir);
        }
        Assert.Equal(outer.OutputDir, BuildTransaction.Physical(Path.Combine(_root, "public")));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, _originalNotionToken);
        Directory.Delete(_root, true);
    }
}

public sealed partial class SiteEngineIntegrationTests
{
    private static string[] TransactionHashes(string root)
        => new[] { "dist", ".cache" }.SelectMany(name => Directory.Exists(Path.Combine(root, name))
            ? Directory.EnumerateFiles(Path.Combine(root, name), "*", SearchOption.AllDirectories) : [])
            .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transaction_IncrementalIdentityAndReportsRemainFormal(bool multiLanguage)
    {
        var (root, config) = CreateBuildReportHealthSite(multiLanguage);
        config = LifecycleConfig(config);
        try
        {
            var documents = new[] { LifecycleDocument("news", "acme"), LifecycleDocument("companies", "acme", multiLanguage ? "zh" : "en") };
            await BuildLifecycleAsync(root, config, documents);
            await BuildLifecycleAsync(root, config, documents);
            using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dist", ".bukit", "incremental-manifest.json")));
            Assert.True(report.RootElement.GetProperty("cacheHitCount").GetInt32() > 0);
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, ".cache"), "build-manifest*.json"))
            {
                var text = File.ReadAllText(path); Assert.DoesNotContain(".bukit-txn-", text);
                Assert.Contains(Path.Combine(root, "dist"), text);
            }
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "dist"), "*.json", SearchOption.AllDirectories))
                Assert.DoesNotContain(".bukit-txn-", File.ReadAllText(path));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root, ".bukit-txn-*"));
        }
        finally { CleanupDir(root); }
    }
}
