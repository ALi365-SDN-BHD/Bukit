using Bukit.Config;
using Bukit.Content.Markdown;
using Bukit.Engine.Incremental;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;
using System.Text.Json;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class LibraryDiagnosticOutputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-library-logs-" + Guid.NewGuid().ToString("N"));

    public LibraryDiagnosticOutputTests() => Directory.CreateDirectory(_root);
    public void Dispose() => TestCleanup.DeleteDirectory(_root);

    public static IEnumerable<object[]> WarningCases()
    {
        string[] sites = ["CopyFile", "CopyDirectory", "SyncFile", "SyncOutsideFile", "SyncDirectory",
            "SyncOutsideDirectory", "SyncFiles", "SyncFilesRecursive", "EnumerateFile", "EnumerateUnsafeFile",
            "EnumerateDirectory", "EnumerateUnsafeDirectory", "EnumerateRecursiveDirectory", "HashFileLimit",
            "HashSizeLimit", "Manifest", "CacheLoad", "CacheSave", "Notion", "Config", "Markdown", "Security", "ExternalTermination"];
        foreach (string site in sites)
        {
            yield return [site, "text", LogLevel.Warn];
            yield return [site, "json", LogLevel.Warn];
            yield return [site, "text", LogLevel.Error];
        }
    }

    [Theory]
    [MemberData(nameof(WarningCases))]
    public async Task ActualWarningSites_PreservePayloadTextJsonAndFiltering(string site, string format, LogLevel level)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var logger = new WarningLogger(new ConsoleLogger(level, format));
        string[] expected;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            expected = await EmitAsync(site, logger);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
        Assert.Equal(expected, logger.Messages);
        Assert.All(logger.Messages, message => Assert.DoesNotContain("[warn]", message, StringComparison.Ordinal));
        Assert.Empty(stdout.ToString());
        if (level == LogLevel.Error) Assert.Empty(stderr.ToString());
        else if (format == "text")
            Assert.Equal(string.Concat(expected.Select(message => "[warn] " + message + Environment.NewLine)), stderr.ToString());
        else
        {
            Assert.EndsWith(Environment.NewLine, stderr.ToString());
            var lines = stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(expected.Length, lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                using var json = JsonDocument.Parse(lines[i]);
                Assert.Equal("Warn", json.RootElement.GetProperty("level").GetString());
                Assert.Equal(expected[i], json.RootElement.GetProperty("msg").GetString());
            }
        }
    }

    private async Task<string[]> EmitAsync(string site, ILogger logger)
    {
        string source = Path.Combine(_root, "source");
        string destination = Path.Combine(_root, "destination");
        Directory.CreateDirectory(source);
        if (site.StartsWith("Copy", StringComparison.Ordinal) || site.StartsWith("Sync", StringComparison.Ordinal) ||
            site.StartsWith("Enumerate", StringComparison.Ordinal))
        {
            bool directory = site.EndsWith("Directory", StringComparison.Ordinal);
            string target = Path.Combine(_root, "outside");
            if (directory) Directory.CreateDirectory(target);
            else File.WriteAllText(target, "target");
            string link = Path.Combine(source, directory ? "link-dir" : "link.txt");
            if (directory) Directory.CreateSymbolicLink(link, site == "EnumerateRecursiveDirectory" ? source : target);
            else File.CreateSymbolicLink(link, target);
            var follow = new DirectoryCopyOptions { FollowSymlinks = true };
            switch (site)
            {
                case "CopyFile": case "CopyDirectory": DirectoryCopy.Copy(source, destination, logger); break;
                case "SyncFile": case "SyncDirectory": DirectoryCopy.Sync(source, destination, logger); break;
                case "SyncOutsideFile": case "SyncOutsideDirectory": DirectoryCopy.Sync(source, destination, follow, logger); break;
                case "SyncFiles": DirectoryCopy.SyncFiles(source, destination, logger); break;
                // A safe enumeration can become a symlink before the per-file race check.
                case "SyncFilesRecursive": DirectoryCopy.SyncFilesRecursive(source, destination, logger, () => [link]); break;
                default:
                    Assert.Empty(DirectoryCopy.EnumerateFilesForSync(source,
                        site is "EnumerateFile" or "EnumerateDirectory" ? new DirectoryCopyOptions() : follow, logger));
                    break;
            }
            string prefix = site switch
            {
                "SyncOutsideFile" => "Skipping symlink outside source directory: ",
                "SyncOutsideDirectory" => "Skipping symlink directory outside source directory: ",
                "EnumerateUnsafeFile" => "Skipping unsafe symlink target: ",
                "EnumerateUnsafeDirectory" => "Skipping unsafe symlink directory target: ",
                "EnumerateRecursiveDirectory" => "Skipping recursive symlink directory: ",
                _ => directory ? "Skipping symlink directory: " : "Skipping symlink: "
            };
            return [prefix + link];
        }
        switch (site)
        {
            case "HashFileLimit":
            case "HashSizeLimit":
                for (int i = 0; i < 3; i++) File.WriteAllText(Path.Combine(source, $"{i}.txt"), "xx");
                Assert.ThrowsAny<InvalidOperationException>(() => HashUtil.Sha256HexForDirectory(source, logger,
                    maxFiles: site == "HashFileLimit" ? 1 : 10, maxTotalSize: site == "HashSizeLimit" ? 1 : 100));
                return site == "HashFileLimit"
                    ? [$"Directory '{source}' has 3 files, exceeding limit of 1. A complete directory hash cannot be computed."]
                    : [$"Directory '{source}' total size exceeds limit of 1 bytes. A complete directory hash cannot be computed."];
            case "Manifest":
            case "CacheLoad":
                string corrupt = Path.Combine(_root, "bad.json");
                File.WriteAllText(corrupt, "{");
                string jsonError = Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse("{")).Message;
                if (site == "Manifest")
                {
                    Assert.Empty(BuildManifest.Load(corrupt, logger).Entries);
                    return [$"Failed to load build manifest '{corrupt}': {jsonError}"];
                }
                Assert.Null(PagesIndexCacheHelper.TryLoadCache(corrupt, logger));
                return [$"pages-index: failed to load cache '{corrupt}': {jsonError}"];
            case "CacheSave":
                string blocked = Path.Combine(_root, "blocked");
                File.WriteAllText(blocked, "file");
                string cache = Path.Combine(blocked, "cache.json");
                string saveError = Assert.ThrowsAny<IOException>(() => Directory.CreateDirectory(blocked)).Message;
                PagesIndexCacheHelper.TrySaveCache(cache, [], logger);
                return [$"pages-index: failed to save cache '{cache}': {saveError}"];
            case "Notion":
                Assert.Null(await new DefaultNotionPageFetcher().FetchAsync(null!, "page-id", logger, CancellationToken.None));
                return [$"pages-index: failed to fetch Notion page 'page-id': {new ArgumentNullException("client").Message}"];
            case "Config":
                string config = Path.Combine(_root, "site.yaml");
                File.WriteAllText(config, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      markdown:\n        dir: content\nbuild:\n  followSymlinks: true\n");
                Assert.True(ConfigLoader.Load(config, logger).Build.FollowSymlinks);
                return ["build.followSymlinks is enabled. Symlinks may point outside the project directory. Ensure all symlinks are trusted."];
            case "Markdown":
                const string yaml = "title: [";
                string yamlError = Record.Exception(() => new YamlStream().Load(new StringReader(yaml)))!.Message;
                Assert.Throws<ContentException>(() => MarkdownFrontMatterParser.ParseFrontMatter(yaml, logger));
                return [$"Failed to parse front matter: {yamlError}"];
            case "Security":
                var app = new AppConfig
                {
                    Site = new SiteConfig { Name = "test", Title = "Test" }, Content = new ContentConfig(),
                    Build = new BuildConfig { Report = new BuildReportConfig { SecurityFailMode = "warn" } }
                };
                BuildReporter.EnforceSecurityGate(app,
                    new SecurityReportData("failed", "passed", "passed", "passed", "passed", [], ["failed"]), false, logger);
                return ["BKT-BUILD-SECURITY-0001: security-report.json contains failed checks (mode=warn)."];
            case "ExternalTermination":
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    Assert.False(await ExternalToolProcessRunner.WaitForTerminationAndWarnAsync(
                        completion.Task, TimeSpan.FromMilliseconds(1), "test-tool", logger));
                }
                finally { completion.SetResult(); }
                return ["External tool termination did not complete within 2 seconds: test-tool"];
            default: throw new InvalidOperationException(site);
        }
    }

    [Fact]
    public void CompatibilityEntries_KeepDefaultWarnTextAndNullGuard()
    {
        using var stderr = new StringWriter();
        var original = Console.Error;
        string path = Path.Combine(_root, "manifest.json");
        File.WriteAllText(path, "{");
        try
        {
            Console.SetError(stderr);
            Assert.Empty(BuildManifest.Load(path).Entries);
        }
        finally { Console.SetError(original); }
        string error = Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse("{")).Message;
        Assert.Equal($"[warn] Failed to load build manifest '{path}': {error}" + Environment.NewLine, stderr.ToString());
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(() => BuildManifest.Load(path, null!)).ParamName);
    }

    [Fact]
    public void LoggerAwarePublicOverloads_AreAdditive()
    {
        Assert.NotNull(typeof(ConfigLoader).GetMethod("Load", [typeof(string)]));
        Assert.NotNull(typeof(ConfigLoader).GetMethod("Load", [typeof(string), typeof(ILogger)]));
        Assert.NotNull(typeof(BuildManifest).GetMethod("Load", [typeof(string)]));
        Assert.NotNull(typeof(BuildManifest).GetMethod("Load", [typeof(string), typeof(ILogger)]));
    }

    [Fact]
    public async Task ContentFactory_ForwardsCallerLoggerToMalformedMarkdown()
    {
        Directory.CreateDirectory(Path.Combine(_root, "content"));
        File.WriteAllText(Path.Combine(_root, "content", "page.md"), "---\ntitle: [\n---\n# Fallback\n");
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = TestContent.Markdown() with { Media = new MediaConfig { DownloadToLocal = false } }
        };
        var logger = new WarningLogger(new ConsoleLogger(LogLevel.Error));
        await Assert.ThrowsAsync<ContentException>(() =>
            ContentProviderFactory.Create(config, _root, false, logger).LoadRawAsync());
        Assert.StartsWith("Failed to parse front matter: ", Assert.Single(logger.Messages));
    }

    [Theory]
    [InlineData("Config")]
    [InlineData("Markdown")]
    public async Task CompatibilityEntries_ConfigAndProviderKeepWarnText(string entry)
    {
        using var stderr = new StringWriter();
        var original = Console.Error;
        string expected;
        try
        {
            Console.SetError(stderr);
            if (entry == "Config")
            {
                string path = Path.Combine(_root, "site.yaml");
                File.WriteAllText(path, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      markdown:\n        dir: content\nbuild:\n  followSymlinks: true\n");
                Assert.True(ConfigLoader.Load(path).Build.FollowSymlinks);
                expected = "build.followSymlinks is enabled. Symlinks may point outside the project directory. Ensure all symlinks are trusted.";
            }
            else
            {
                File.WriteAllText(Path.Combine(_root, "page.md"), "---\ntitle: [\n---\n# Fallback\n");
                var provider = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(_root));
                await Assert.ThrowsAsync<ContentException>(() => provider.LoadRawAsync());
                expected = "Failed to parse front matter: " + Record.Exception(() => new YamlStream().Load(new StringReader("title: [")))!.Message;
            }
        }
        finally { Console.SetError(original); }
        Assert.Equal("[warn] " + expected + Environment.NewLine, stderr.ToString());
    }

    private sealed class WarningLogger(ILogger inner) : ILogger
    {
        internal List<string> Messages { get; } = [];
        public void Debug(string message) => Assert.Fail("Unexpected Debug");
        public void Info(string message) => Assert.Fail("Unexpected Info");
        public void Warn(string message) { Messages.Add(message); inner.Warn(message); }
        public void Error(string message) => Assert.Fail("Unexpected Error");
    }
}
