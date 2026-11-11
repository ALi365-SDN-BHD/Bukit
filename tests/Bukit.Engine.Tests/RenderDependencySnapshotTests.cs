using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

// add-per-page-render-dependencies: per-route hashes cover the base and only the collections read.
public sealed class RenderDependencySnapshotTests
{
    private const string All = "*";
    private static readonly AppConfig s_config = new()
    {
        Site = new SiteConfig { Name = "s", Title = "S" },
        Content = TestContent.Markdown() with { Media = new MediaConfig { DownloadToLocal = false } },
        Build = new BuildConfig { Output = "dist" },
        Theme = new ThemeConfig { Layouts = "layouts" }
    };

    [Fact]
    public void Combine_CoversOnlyCollectionsRead()
    {
        var before = Snapshot(("menu", 1), ("pages_by_id", 1));
        var after = Snapshot(("menu", 1), ("pages_by_id", 2));

        Assert.Equal(before.BaseHash, after.BaseHash);
        Assert.Equal(before.Combine("r", ["site.data.menu"]), after.Combine("r", ["site.data.menu"]));
        Assert.Equal(before.Combine("r", []), after.Combine("r", []));
        Assert.NotEqual(before.Combine("r", ["site.data.pages_by_id"]), after.Combine("r", ["site.data.pages_by_id"]));
        Assert.NotEqual(before.Combine("r", ["site.data.*"]), after.Combine("r", ["site.data.*"]));
        Assert.NotEqual(before.Combine("r", null), after.Combine("r", null));
        Assert.NotEqual(before.Combine("r", [All]), after.Combine("r", [All]));
    }

    [Fact]
    public void Combine_CollectionAppearingOrDisappearingChangesTheHash()
    {
        var without = Snapshot(("menu", 1));
        var with = Snapshot(("menu", 1), ("footer", 1));

        Assert.NotEqual(without.Combine("r", ["site.data.footer"]), with.Combine("r", ["site.data.footer"]));
        Assert.NotEqual(without.Combine("r", ["site.data.*"]), with.Combine("r", ["site.data.*"]));
        Assert.Equal(without.Combine("r", ["site.data.menu"]), with.Combine("r", ["site.data.menu"]));
    }

    [Fact]
    public void Combine_DependsOnRouteBaseAndReadSet()
    {
        var snapshot = Snapshot(("menu", 1));

        Assert.NotEqual(snapshot.Combine("a", []), snapshot.Combine("b", []));
        Assert.NotEqual(snapshot.Combine("a", []), snapshot.Combine("a", ["site.data.menu"]));
        Assert.Equal(snapshot.Combine("a", ["pages", "site.data.menu"]), snapshot.Combine("a", ["site.data.menu", "pages"]));
    }

    [Fact]
    public void ComputeSnapshot_KeysAreLowerCaseAndPagesIsLazy()
    {
        var pagesComputed = 0;
        var site = new SiteModel
        {
            Name = "s",
            Title = "S",
            BaseUrl = "/",
            Language = "en",
            Data = new Dictionary<string, object> { ["Menu"] = 1, ["__related_pages"] = 2, ["__internal"] = 3 },
            DataIndex = new Dictionary<string, object> { ["tags"] = 4 }
        };
        var snapshot = RenderDependencyHasher.ComputeSnapshot(s_config, site, pagesFingerprint: () =>
        {
            pagesComputed++;
            return "pages";
        });

        Assert.Equal(["pages", "site.data.menu", "site.data_index.tags", "site.related_pages"], snapshot.CollectionKeys);
        snapshot.Combine("r", ["site.data.menu"]);
        Assert.Equal(0, pagesComputed);
        snapshot.Combine("r", ["pages"]);
        snapshot.Combine("r", null);
        Assert.Equal(1, pagesComputed);
    }

    [Fact]
    public async Task DispatchAsync_CollectionChangeWithSameBase_IsDataDependencyChanged()
    {
        var document = ContentDocument.Create("id-1", "Hello", "hello", DateTimeOffset.UnixEpoch, null, bodyKey: "body-1");
        var route = new RouteInfo("/hello/", "hello/index.html", "page.html");
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(document);
        const string html = "<p>body</p>";
        var before = Snapshot(("menu", 1));
        var after = Snapshot(("menu", 2));
        string[] reads = ["site.data.menu"];

        foreach (var (previousBase, previousReads, expected) in new (string, string[]?, string)[]
        {
            ("base", reads, "data_dependency_changed"),
            ("old", reads, "render_dependency_changed"),
            // No read record: nothing says a collection changed, so the reason stays site-wide.
            ("base", null, "render_dependency_changed")
        })
        {
            var entries = new ConcurrentDictionary<string, BuildManifestEntry>
            {
                [route.OutputPath] = new BuildManifestEntry
                {
                    OutputPath = route.OutputPath,
                    TemplateHash = "template",
                    MetadataHash = metadataHash,
                    RouteHash = IncrementalBuildEngine.ComputeRouteHash(route),
                    RenderDependencyHash = previousReads is null ? "unrecorded" : before.Combine(previousBase, previousReads),
                    RenderBaseHash = previousBase,
                    RenderReads = previousReads,
                    ContentHash = IncrementalBuildEngine.ComputeContentHash(document, metadataHash, html)
                }
            };
            var outputDir = Path.Combine(Path.GetTempPath(), "bukit-reads-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(outputDir, "hello"));
            try
            {
                File.WriteAllText(Path.Combine(outputDir, route.OutputPath), "previous output");
                var result = await PageRenderDispatcher.DispatchAsync(
                    [RenderEntry.ForPage(document, route)], new FixedBodyStore(html), new ContentRenderer(),
                    new SiteModel { Name = "site", Title = "site", BaseUrl = "/", Language = "en" },
                    outputDir, "template", "base", true, new BuildManifest(), entries,
                    new ConcurrentDictionary<string, byte>(), 1, new ConsoleLogger(LogLevel.Error), CancellationToken.None,
                    dependencySnapshot: after);

                Assert.Equal(new KeyValuePair<string, int>(expected, 1), Assert.Single(result.RenderReasons));
                var written = entries[route.OutputPath];
                // A renderer that does not report templates reads every collection.
                Assert.Equal([All], written.RenderReads);
                Assert.Equal("base", written.RenderBaseHash);
                Assert.Equal(after.Combine("base", [All]), written.RenderDependencyHash);
            }
            finally
            {
                TestCleanup.DeleteDirectory(outputDir, recursive: true);
            }
        }
    }

    private static RenderDependencySnapshot Snapshot(params (string Key, int Value)[] data) =>
        RenderDependencyHasher.ComputeSnapshot(s_config, new SiteModel
        {
            Name = "s",
            Title = "S",
            BaseUrl = "/",
            Language = "en",
            Data = data.ToDictionary(item => item.Key, item => (object)item.Value)
        });

    private sealed class FixedBodyStore(string html) : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ContentBody(html));
    }

    private sealed class ContentRenderer : ITemplateRenderer
    {
        public string RenderPage(string templatePath, PageModel model) => model.Page.Content;

        public string RenderList(string templatePath, ListPageModel model) => string.Empty;
    }
}
