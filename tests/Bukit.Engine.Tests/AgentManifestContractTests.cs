using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class AgentManifestContractTests
{
    private static readonly DateTimeOffset PublishedAt = DateTimeOffset.Parse("2026-07-13T00:00:00+00:00");
    private static readonly DateTimeOffset UpdatedAt = DateTimeOffset.Parse("2026-08-01T09:30:00+00:00");

    [Fact]
    public void Manifest_PublishedAtIsThePublishDate_UpdatedAtOnlyWhenSet_GeneratedAtIsTheLatestChange()
    {
        using var sandbox = new SiteSandbox();
        var edited = Document("edited", UpdatedAt);
        var untouched = Document("untouched", null);
        var context = Context(sandbox.Root, [edited, untouched]);

        var entries = DefaultContentProjectionWriter.BuildAgentManifestEntries(context);
        var path = new AgentManifestProjection().Project(sandbox.Root, entries, []);

        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        var root = manifest.RootElement;
        Assert.Equal("1.1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("2026-08-01T09:30:00+00:00", root.GetProperty("generatedAt").GetString());
        Assert.Empty(root.GetProperty("lists").EnumerateArray());
        var documents = root.GetProperty("documents").EnumerateArray().ToArray();
        var editedRow = Assert.Single(documents, x => x.GetProperty("route").GetString() == "/edited/");
        Assert.Equal("2026-07-13T00:00:00+00:00", editedRow.GetProperty("publishedAt").GetString());
        Assert.Equal("2026-08-01T09:30:00+00:00", editedRow.GetProperty("updatedAt").GetString());
        var untouchedRow = Assert.Single(documents, x => x.GetProperty("route").GetString() == "/untouched/");
        Assert.Equal("2026-07-13T00:00:00+00:00", untouchedRow.GetProperty("publishedAt").GetString());
        Assert.False(untouchedRow.TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public void Manifest_WithoutEdits_GeneratedAtIsTheLatestPublishDate()
    {
        using var sandbox = new SiteSandbox();
        var entries = DefaultContentProjectionWriter.BuildAgentManifestEntries(Context(sandbox.Root, [Document("untouched", null)]));

        var path = new AgentManifestProjection().Project(sandbox.Root, entries, []);

        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("2026-07-13T00:00:00+00:00", manifest.RootElement.GetProperty("generatedAt").GetString());
    }

    [Theory]
    [InlineData(false, "/", "/posts/", "/tags/a/")]
    [InlineData(true, "/repo/", "/repo/posts/", "/repo/tags/a/")]
    public void ListEntries_UseTheManifestRouteSpace_AndDropNoindexAndDocumentRoutes(bool prefixRoutes, string home, string posts, string tag)
    {
        using var sandbox = new SiteSandbox();
        sandbox.Write("archive/index.html", "<html><head><meta name=\"robots\" content=\"noindex\"></head></html>");
        var page = Document("page", null);
        var shared = new RoutedContentDocument(Document("shared", null).Document, new RouteInfo("/shared/", "shared/index.html", "page.html"));
        var seoIndex = Index(
            List("/", "list"),
            List("/tags/a/", "taxonomy"),
            List("/posts/", "list"),
            List("/drafts/", "list") with { Indexable = false, Robots = "noindex,follow" },
            List("/archive/", "list"),
            new SeoIndexEntry(page.Route, Canonical("/page/"), null, true, PublishedAt, "page", "page"),
            List("/shared/", "list"));

        var lists = Build(seoIndex, [page, shared], sandbox.Root, prefixRoutes: prefixRoutes);

        Assert.Equal([home, posts, tag], lists.Select(x => x.Route));
        Assert.Equal(["home", "list", "taxonomy"], lists.Select(x => x.Kind));
        Assert.Equal(["/repo/", "/repo/posts/", "/repo/tags/a/"], lists.Select(x => Assert.Single(x.Representations, r => r.Kind == "html").Url));
        Assert.All(lists, x => Assert.Equal("en", x.Language));
        Assert.All(lists, x => Assert.Equal(UpdatedAt, x.LastModified));
    }

    [Fact]
    public void ListEntries_DropCollectionsWithoutSitemap_OutOfScopeCanonicals_AndCanonicalsADocumentTook()
    {
        var config = Config() with
        {
            Site = Config().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["hidden"] = new() { Permalink = "/hidden/{slug}/", Output = new CollectionOutputConfig { Sitemap = false } }
                }
            }
        };
        var graph = ListRouteGraph.Create(
        [
            new ListRoutePlan
            {
                RouteId = "hidden", Kind = ListRouteKind.CollectionList, Url = "/hidden/", OutputPath = "hidden/index.html",
                Template = "list.html", TotalItems = 1, CanonicalUrl = "/hidden/", Collection = "hidden"
            }
        ]);
        // "/a-doc/" sorts before "/b-list/", so the sitemap gives their shared canonical to the document.
        var document = Document("a-doc", null);
        var seoIndex = Index(
            List("/hidden/", "list"),
            List("/foreign/", "list") with { Canonical = "https://other.test/foreign/" },
            new SeoIndexEntry(document.Route, Canonical("/b-list/"), null, true, PublishedAt, "a-doc", "page"),
            List("/b-list/", "list"),
            List("/kept/", "list"));

        var lists = AgentManifestListEntries.Build(
            config, seoIndex, graph, [document], string.Empty, "/repo", "en", prefixRoutes: false,
            AgentManifestListEntries.CreateLocFilter(config, "/repo"), null);

        Assert.Equal(["/kept/"], lists.Select(x => x.Route));
    }

    [Fact]
    public void ListEntries_WithoutSiteUrl_AreEmptyLikeTheSitemap()
    {
        var config = Config() with { Site = Config().Site with { Url = null } };

        Assert.Null(AgentManifestListEntries.CreateLocFilter(config, "/"));
        Assert.Empty(AgentManifestListEntries.Build(
            config, Index(List("/", "list")), null, [], string.Empty, "/", "en", prefixRoutes: false, locFilter: null, null));
    }

    [Fact]
    public void ListEntries_MergedRootScopeKeepsOtherLanguageCanonicals_ALanguageScopeDoesNot()
    {
        var config = Config();
        var seoIndex = Index(List("/tags/x/", "taxonomy") with { Canonical = "https://example.test/zh-CN/tags/x/" });

        var merged = AgentManifestListEntries.Build(
            config, seoIndex, null, [], string.Empty, "/en", "en", prefixRoutes: true, AgentManifestListEntries.CreateLocFilter(config, "/"), null);
        var split = AgentManifestListEntries.Build(
            config, seoIndex, null, [], string.Empty, "/en", "en", prefixRoutes: true, AgentManifestListEntries.CreateLocFilter(config, "/en"), null);

        Assert.Equal(["/en/tags/x/"], merged.Select(x => x.Route));
        Assert.Empty(split);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task I18nBuild_VariantManifestsListTheirHomeRelative_AndTheRootManifestListsBothLanguagesPrefixed()
    {
        using var site = new SiteBuilder("i18n-site");

        await site.BuildAsync();

        foreach (var language in new[] { "en", "zh-CN" })
        {
            var list = Assert.Single(Lists(Path.Combine(site.Output, language, "agent-manifest.json")));
            Assert.Equal(("/", language, "home", $"/{language}/"), list);
        }

        Assert.Equal(
            [("/en/", "en", "home", "/en/"), ("/zh-CN/", "zh-CN", "home", "/zh-CN/")],
            Lists(Path.Combine(site.Output, "agent-manifest.json")));
    }

    private static (string?, string?, string?, string?)[] Lists(string path)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("1.1", manifest.RootElement.GetProperty("schemaVersion").GetString());
        return manifest.RootElement.GetProperty("lists").EnumerateArray()
            .Select(x => (
                x.GetProperty("route").GetString(),
                x.GetProperty("language").GetString(),
                x.GetProperty("kind").GetString(),
                x.GetProperty("representations").EnumerateArray().Single(r => r.GetProperty("kind").GetString() == "html").GetProperty("url").GetString()))
            .ToArray();
    }

    private static IReadOnlyList<AgentManifestListEntry> Build(
        Dictionary<string, SeoIndexEntry> seoIndex, IReadOnlyList<RoutedContentDocument> documents, string outputDir, bool prefixRoutes)
        => AgentManifestListEntries.Build(
            Config(), seoIndex, null, documents, outputDir, "/repo", "en", prefixRoutes,
            AgentManifestListEntries.CreateLocFilter(Config(), "/repo"), null);

    private static Dictionary<string, SeoIndexEntry> Index(params SeoIndexEntry[] entries)
        => entries.ToDictionary(x => x.Route.OutputPath, StringComparer.OrdinalIgnoreCase);

    private static string Canonical(string url) => "https://example.test/repo" + url;

    private static SeoIndexEntry List(string url, string type)
        => new(
            new RouteInfo(url, url == "/" ? "index.html" : url.Trim('/') + "/index.html", "list.html"),
            Canonical(url),
            null,
            true,
            UpdatedAt,
            SourceItemId: null,
            ContentType: type,
            IsDerived: true);

    private static RoutedContentDocument Document(string slug, DateTimeOffset? updatedAt)
    {
        var document = ContentDocument.Create(slug, slug, slug, PublishedAt, "<p>body</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["language"] = "en", ["collection"] = "post" }));
        document = document with
        {
            Record = document.Record with { Lifecycle = document.Record.Lifecycle with { UpdatedAt = updatedAt } }
        };
        return new RoutedContentDocument(document, new RouteInfo($"/{slug}/", $"{slug}/index.html", "post.html"));
    }

    private static AppConfig Config()
        => new() { Site = new SiteConfig { Name = "test", Title = "Test", Url = "https://example.test" }, Content = TestContent.Markdown() };

    private static PublishProjectionContext Context(string outputDir, IReadOnlyList<RoutedContentDocument> documents)
        => new(
            Config(),
            outputDir,
            new CanonicalContentGraph(documents.Select(x => x.Document.Record).ToArray(), [], [], []),
            new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
            documents);
}
