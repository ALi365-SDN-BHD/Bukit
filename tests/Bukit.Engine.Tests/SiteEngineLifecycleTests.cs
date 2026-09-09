using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Incremental;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed partial class SiteEngineIntegrationTests
{
    [Fact]
    public async Task PublicLifecycle_SameSlugCollectionsAndWithdrawal()
    {
        var (root, config) = CreateBuildReportHealthSite();
        config = config with { Site = config.Site with { Collections = new Dictionary<string, CollectionConfig>
        {
            ["news"] = new() { Permalink = "/news/{slug}/", Template = "pages/post.html", ListRoute = "/news/", ListTemplate = "pages/list.html" },
            ["companies"] = new() { Permalink = "/companies/{slug}/", Template = "pages/post.html", ListRoute = "/companies/", ListTemplate = "pages/list.html" }
        } } };
        try
        {
            var items = new[] { LifecycleDocument("news", "acme"), LifecycleDocument("companies", "acme") };
            await BuildLifecycleAsync(root, config, items);
            var output = Path.Combine(root, "dist");
            Assert.True(File.Exists(Path.Combine(output, "content/news/acme/index.html.json")));
            Assert.True(File.Exists(Path.Combine(output, "content/companies/acme/index.html.md")));
            await BuildLifecycleAsync(root, config, items.Skip(1).ToArray());
            Assert.False(File.Exists(Path.Combine(output, "news/acme/index.html")));
            Assert.False(File.Exists(Path.Combine(output, "content/news/acme/index.html.json")));
            Assert.False(File.Exists(Path.Combine(output, "content/news/acme/index.html.md")));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task PublicLifecycle_ClockOnlyExpiryMatchesCleanBuild()
    {
        var (root, config) = CreateBuildReportHealthSite();
        config = LifecycleConfig(config);
        var items = new[] { LifecycleDocument("news", "acme", expiresAt: "2026-09-10T00:00:00Z") };
        try
        {
            await BuildLifecycleAsync(root, config, items);
            var output = Path.Combine(root, "dist");
            Assert.Contains("/news/acme/", File.ReadAllText(Path.Combine(output, "sitemap.xml")));
            var afterExpiry = DateTimeOffset.Parse("2026-09-11T00:00:00Z");
            await BuildLifecycleAsync(root, config, items, now: afterExpiry);
            AssertNoAggregateReference(output, "/news/acme/");
            Assert.True(File.Exists(Path.Combine(output, "news/acme/index.html")));
            Assert.True(File.Exists(Path.Combine(output, "content/news/acme/index.html.json")));
            Assert.True(File.Exists(Path.Combine(output, "content/news/acme/index.html.md")));
            var (cleanRoot, _) = CreateBuildReportHealthSite();
            try
            {
                await BuildLifecycleAsync(cleanRoot, config, items, false, afterExpiry);
                Assert.Equal(PublicHashes(Path.Combine(cleanRoot, "dist")), PublicHashes(output));
            }
            finally { CleanupDir(cleanRoot); }
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(false, "merged")]
    [InlineData(true, "merged")]
    [InlineData(true, "index")]
    public async Task PublicLifecycle_AuditAggregateLocationsMatchExecutedOutputs(bool multiLanguage, string searchMode)
    {
        var (root, config) = CreateBuildReportHealthSite(multiLanguage);
        config = LifecycleConfig(config);
        config = config with { Site = config.Site with { Search = config.Site.Search with { Mode = searchMode } } };
        try
        {
            await BuildLifecycleAsync(root, config, [LifecycleDocument("news", "acme"), LifecycleDocument("companies", "acme", multiLanguage ? "zh" : "en")]);
            var output = Path.Combine(root, "dist");
            var reports = Directory.EnumerateFiles(output, "publish-audit-report.json", SearchOption.AllDirectories).ToArray();
            Assert.Equal(multiLanguage ? 3 : 1, reports.Length);
            foreach (var reportPath in reports)
            {
                using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
                var reportOutput = Path.GetDirectoryName(Path.GetDirectoryName(reportPath))!;
                var baseUrl = report.RootElement.GetProperty("baseUrl").GetString()!.TrimEnd('/');
                var representations = report.RootElement.GetProperty("documents").EnumerateArray()
                    .SelectMany(document => document.GetProperty("representations").EnumerateArray()).ToArray();
                Assert.Contains(representations, item => item.GetProperty("kind").GetString() == "atom" && item.GetProperty("generated").GetBoolean());
                foreach (var item in representations.Where(item => item.GetProperty("kind").GetString() is "feed" or "atom" or "jsonfeed" or "sitemap" or "search" or "llms" or "llms-full" or "robots" or "agent-manifest"))
                {
                    var kind = item.GetProperty("kind").GetString();
                    var path = item.GetProperty("path").GetString()!;
                    if (kind == "atom") Assert.Equal("feeds/atom.xml", path);
                    if (kind == "jsonfeed") Assert.Equal("feeds/feed.json", path);
                    if (kind == "search") Assert.Equal(multiLanguage && reportOutput == output && searchMode == "index" ? "search.index.json" : "search.json", path);
                    Assert.Equal(baseUrl + "/" + string.Join("/", path.Split('/').Select(Uri.EscapeDataString)), item.GetProperty("url").GetString());
                    if (item.GetProperty("generated").GetBoolean()) Assert.True(File.Exists(Path.Combine(reportOutput, path)), $"Missing generated {kind}: {path}");
                }
            }
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task PublicLifecycle_ReplayMatchesCleanBuild(bool incremental, bool reports)
    {
        var (root, config) = CreateBuildReportHealthSite(multiLanguage: true);
        config = LifecycleConfig(config) with { Build = config.Build with { Report = config.Build.Report with { Enabled = reports } } };
        var documents = new[] { LifecycleDocument("news", "acme"), LifecycleDocument("companies", "acme"), LifecycleDocument("news", "acme", "zh"), LifecycleDocument("companies", "acme", "zh") };
        try
        {
            for (var step = 0; step < 9; step++)
            {
                switch (step)
                {
                    case 1: documents[0] = LifecycleDocument("news", "acme", title: "Updated company news"); break;
                    case 2: documents[0] = LifecycleDocument("news", "renamed"); break;
                    case 3: documents = documents.Where(x => x.Id != "news-zh").ToArray(); documents[0] = LifecycleDocument("news", "renamed", "zh", id: "news-en"); break;
                    case 4: documents[0] = LifecycleDocument("news", "renamed", draft: true); break;
                    case 5: documents = documents.Where(x => x.Id != "news-en").ToArray(); documents[0] = LifecycleDocument("companies", "acme", expired: true); break;
                    case 6: config = config with { Site = config.Site with { Feed = config.Site.Feed with { Formats = [] }, Search = config.Site.Search with { Ui = "off" }, Seo = config.Site.Seo with { Geo = config.Site.Seo.Geo with { Enabled = false } } } }; break;
                    case 7: config = config with { Site = config.Site with { Languages = ["en"] } }; documents = documents.Where(x => x.Id.EndsWith("-en", StringComparison.Ordinal)).ToArray(); break;
                    case 8: config = config with { Site = config.Site with { Collections = new Dictionary<string, CollectionConfig> { ["companies"] = config.Site.Collections!["companies"] } } }; break;
                }
                await BuildLifecycleAsync(root, config, documents, incremental);
                AssertManifestReferences(Path.Combine(root, "dist"));
                if (step == 0)
                {
                    using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dist", "agent-manifest.json")));
                    Assert.True(manifest.RootElement.GetProperty("documents").GetArrayLength() >= 4);
                }
                if (step >= 2) AssertNoAggregateReference(Path.Combine(root, "dist"), "/en/news/acme/");
                if (step >= 4) AssertNoAggregateReference(Path.Combine(root, "dist"), "/news/renamed/");
                var (cleanRoot, _) = CreateBuildReportHealthSite();
                try
                {
                    await BuildLifecycleAsync(cleanRoot, config, documents, false);
                    var expected = PublicHashes(Path.Combine(cleanRoot, "dist"));
                    var actual = PublicHashes(Path.Combine(root, "dist"));
                    var changed = expected.Except(actual).FirstOrDefault();
                    var changedPath = changed?.Split(' ')[0];
                    Assert.True(expected.SequenceEqual(actual), $"Replay step {step}, changed path {changedPath}: clean={ReadOutput(Path.Combine(cleanRoot, "dist"), changedPath)} incremental={ReadOutput(Path.Combine(root, "dist"), changedPath)}");
                }
                finally { CleanupDir(cleanRoot); }
            }
            Assert.True(File.Exists(Path.Combine(root, "dist", "en/content/companies/acme/index.html.json")));
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData("projection", false)]
    [InlineData("delete", false)]
    [InlineData("aggregate", true)]
    [InlineData("report", false)]
    public async Task PublicLifecycle_FailureDoesNotCommitAndRecovers(string failure, bool multiLanguage)
    {
        var (root, config) = CreateBuildReportHealthSite(multiLanguage);
        config = LifecycleConfig(config);
        var items = new[] { LifecycleDocument("news", "acme"), LifecycleDocument("companies", "acme", multiLanguage ? "zh" : "en") };
        try
        {
            await BuildLifecycleAsync(root, config, items);
            var manifestPath = Path.Combine(root, ".cache", "build-manifest.json");
            var manifest = File.ReadAllBytes(manifestPath);
            var output = Path.Combine(root, "dist");
            var path = Path.Combine(output, failure switch
            {
                "projection" => "content/news/acme/index.html.json",
                "delete" => "news/acme/index.html",
                "aggregate" => "agent-manifest.json",
                _ => ".bukit/build-report.json"
            });
            File.Delete(path);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "obstruction"), "failure injection");
            if (failure == "delete") items = items.Skip(1).ToArray();
            await Assert.ThrowsAnyAsync<Exception>(() => BuildLifecycleAsync(root, config, items));
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(output));
            Assert.Equal(manifest, File.ReadAllBytes(manifestPath));
            Directory.Delete(path, recursive: true);
            await BuildLifecycleAsync(root, config, items);
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(output));
            var (cleanRoot, _) = CreateBuildReportHealthSite();
            try
            {
                await BuildLifecycleAsync(cleanRoot, config, items, false);
                Assert.Equal(PublicHashes(Path.Combine(cleanRoot, "dist")), PublicHashes(output));
            }
            finally { CleanupDir(cleanRoot); }
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task PublicLifecycle_FirstFailureRequiresEmptyDirectoryAndPreservesUntrackedFiles()
    {
        var (root, config) = CreateBuildReportHealthSite();
        config = LifecycleConfig(config);
        try
        {
            var output = Path.Combine(root, "dist");
            Directory.CreateDirectory(Path.Combine(output, "content/news/acme/index.html.json"));
            var sentinel = Path.Combine(output, "user.txt");
            File.WriteAllText(sentinel, "keep");
            var items = new[] { LifecycleDocument("news", "acme") };
            await Assert.ThrowsAnyAsync<Exception>(() => BuildLifecycleAsync(root, config, items));
            Assert.False(File.Exists(Path.Combine(output, ".bukit-output-marker")));
            var error = await Assert.ThrowsAnyAsync<Exception>(() => BuildLifecycleAsync(root, config, items));
            Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("keep", File.ReadAllText(sentinel));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task PublicLifecycle_UntrackedFileSurvivesSuccessfulWithdrawal()
    {
        var (root, config) = CreateBuildReportHealthSite();
        config = LifecycleConfig(config);
        try
        {
            await BuildLifecycleAsync(root, config, [LifecycleDocument("news", "acme")], false);
            var sentinel = Path.Combine(root, "dist", "user.txt");
            File.WriteAllText(sentinel, "keep");
            await BuildLifecycleAsync(root, config, [], false);
            Assert.Equal("keep", File.ReadAllText(sentinel));
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicLifecycle_UntrackedRobotsIsNeverClaimed(bool multiLanguage)
    {
        var (root, config) = CreateBuildReportHealthSite(multiLanguage);
        config = LifecycleConfig(config);
        try
        {
            var output = Path.Combine(root, "dist");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "robots.txt"), "User policy");
            await BuildLifecycleAsync(root, config, []);
            config = config with { Site = config.Site with { Seo = config.Site.Seo with { RobotsTxt = config.Site.Seo.RobotsTxt with { Enabled = false } } } };
            await BuildLifecycleAsync(root, config, []);
            Assert.Equal("User policy", File.ReadAllText(Path.Combine(output, "robots.txt")));
            Assert.DoesNotContain("robots.txt", BuildManifest.Load(Path.Combine(root, ".cache", "build-manifest.json")).OwnedOutputs);
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicLifecycle_LegacyManifestRequiresMarker(bool marker)
    {
        var (root, config) = CreateBuildReportHealthSite();
        config = LifecycleConfig(config);
        try
        {
            var items = new[] { LifecycleDocument("news", "acme") };
            await BuildLifecycleAsync(root, config, items);
            var output = Path.Combine(root, "dist");
            var legacy = Path.Combine(output, "content", "acme.json");
            File.WriteAllText(legacy, "old projection");
            File.WriteAllText(Path.Combine(root, ".cache", "build-manifest.json"), "{\"version\":2,\"entries\":{}}");
            if (!marker) File.Delete(Path.Combine(output, ".bukit-output-marker"));
            if (marker)
            {
                await BuildLifecycleAsync(root, config, items);
                Assert.False(File.Exists(legacy));
                Assert.Equal(3, BuildManifest.Load(Path.Combine(root, ".cache", "build-manifest.json")).Version);
            }
            else
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => BuildLifecycleAsync(root, config, items));
                Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(legacy));
            }
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task PublicLifecycle_NormalizedProjectionCollisionFails()
    {
        var (root, config) = CreateBuildReportHealthSite();
        config = LifecycleConfig(config);
        try
        {
            var collision = Path.Combine(root, "static", "content", "news", "acme");
            Directory.CreateDirectory(collision);
            File.WriteAllText(Path.Combine(collision, "index.html.json"), "static conflict");
            await Assert.ThrowsAnyAsync<Exception>(() => BuildLifecycleAsync(root, config, [LifecycleDocument("news", "acme")]));
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task PublicLifecycle_DisposeFailureCannotComplete()
    {
        var (root, config) = CreateBuildReportHealthSite();
        try
        {
            var engine = new SiteEngine(new TestLogger(), new StaticContentProviderFactory(new RawContentLoadResult([], new FailingDisposeBodyStore())), new DefaultSearchIndexBuilder());
            await Assert.ThrowsAsync<IOException>(() => engine.BuildAsync(config, root, new ConfigOverrides { Clean = false }));
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
            Assert.False(File.Exists(Path.Combine(root, ".cache", "build-manifest.json")));
        }
        finally { CleanupDir(root); }
    }

    private sealed class FailingDisposeBodyStore : IContentBodyStore, IAsyncDisposable
    {
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default) => Task.FromResult(new ContentBody(""));
        public ValueTask DisposeAsync() => ValueTask.FromException(new IOException("Injected disposal failure"));
    }

    private static void AssertManifestReferences(string output)
    {
        foreach (var path in Directory.EnumerateFiles(output, "agent-manifest.json", SearchOption.AllDirectories))
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var document in manifest.RootElement.GetProperty("documents").EnumerateArray())
            foreach (var representation in document.GetProperty("representations").EnumerateArray())
            {
                var kind = representation.GetProperty("kind").GetString();
                if (kind is not ("html" or "json" or "markdown")) continue;
                var url = representation.GetProperty("url").GetString()!;
                string target;
                if (kind == "html")
                {
                    // Variant HTML routes are local; root routes include the deployment base.
                    target = url.StartsWith("/docs/", StringComparison.Ordinal) ? Path.Combine(output, Uri.UnescapeDataString(url[6..])) : Path.Combine(Path.GetDirectoryName(path)!, Uri.UnescapeDataString(url.TrimStart('/')));
                    if (url.EndsWith('/')) target = Path.Combine(target, "index.html");
                }
                else
                {
                    Assert.StartsWith("/docs/", url);
                    target = Path.Combine(output, Uri.UnescapeDataString(url[6..]));
                }
                Assert.True(File.Exists(target), $"Missing {kind} target: {url} -> {target}");
            }
        }
    }

    private static void AssertNoAggregateReference(string output, string route)
    {
        foreach (var path in EnumeratePublicOutputFiles(output).Where(path =>
                     !path.Contains("content/", StringComparison.Ordinal) &&
                     (path.EndsWith(".xml", StringComparison.Ordinal) || path.EndsWith(".json", StringComparison.Ordinal) || Path.GetFileName(path).StartsWith("llms", StringComparison.Ordinal))))
            Assert.DoesNotContain(route, File.ReadAllText(Path.Combine(output, path)), StringComparison.Ordinal);
    }

    private static AppConfig LifecycleConfig(AppConfig config)
        => config with { Site = config.Site with
        {
            BaseUrl = "/docs/", SitemapMode = "merged",
            Feed = config.Site.Feed with { Mode = "merged", Formats = ["rss", "atom", "json"], Path = "feeds" },
            Search = config.Site.Search with { Mode = "merged", Ui = "builtin" },
            Seo = config.Site.Seo with { Enabled = true, Geo = config.Site.Seo.Geo with { Enabled = true, LlmsTxt = true, LlmsFullTxt = true } },
            Collections = new Dictionary<string, CollectionConfig>
            {
                ["news"] = new() { Permalink = "/news/{slug}/", Template = "pages/post.html", ListRoute = "/news/", ListTemplate = "pages/list.html", Output = new() { Rss = true } },
                ["companies"] = new() { Permalink = "/companies/{slug}/", Template = "pages/post.html", ListRoute = "/companies/", ListTemplate = "pages/list.html", Output = new() { Rss = true } }
            }
        } };

    private static string ReadOutput(string output, string? path)
        => path is not null && File.Exists(Path.Combine(output, path)) ? File.ReadAllText(Path.Combine(output, path)) : "absent";

    private static string[] PublicHashes(string output)
        => EnumeratePublicOutputFiles(output).Select(path => path + " " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, path))))).ToArray();

    private sealed class LifecycleClock(DateTimeOffset? now = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now ?? DateTimeOffset.Parse("2026-09-09T00:00:00Z");
    }

    private static ContentDocument LifecycleDocument(string collection, string slug, string language = "en", string title = "Acme", bool draft = false, bool expired = false, string? id = null, string? expiresAt = null)
        => ContentDocument.Create(id ?? collection + "-" + language, title, slug, DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            "<p>Acme business content</p>", ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["collection"] = collection, ["language"] = language, ["draft"] = draft, ["type"] = "post", ["expires_at"] = expiresAt ?? (expired ? "2026-01-01T00:00:00Z" : "2099-01-01T00:00:00Z"), ["related_to"] = new[] { collection == "news" ? "companies-" + language : "news-" + language }
            }));

    private static Task<BuildResult> BuildLifecycleAsync(string root, AppConfig config, ContentDocument[] items, bool incremental = true, DateTimeOffset? now = null)
        => new SiteEngine(new TestLogger(), new StaticContentProviderFactory(new RawContentLoadResult(ToRawDocuments(items), EmptyContentBodyStore.Instance)), new DefaultSearchIndexBuilder(), null, new LifecycleClock(now))
            .BuildAsync(config, root, new ConfigOverrides { Clean = false, Incremental = incremental });
}
