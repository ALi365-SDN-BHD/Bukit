using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyPinningTests
{
    [Fact]
    public void DerivePages_WithPinnedItemAcrossSources_PinnedFirstInCategory()
    {
        var layoutsDir = CreateTaxonomyLayoutsDir();
        var pinned = ContentDocument.Create(
            id: "s1:p1",
            title: "Pinned",
            slug: "pinned",
            publishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true)
            });

        var normal = ContentDocument.Create(
            id: "s2:p2",
            title: "Normal Newer",
            slug: "normal-newer",
            publishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = "s2",
                ["categories"] = new List<object> { "Cat One" }
            }));

        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (pinned, new RouteInfo("/pinned/", "pinned/index.html", "pages/page.html")),
            (normal, new RouteInfo("/normal-newer/", "normal-newer/index.html", "pages/page.html"))
        };
        var (ctx, config) = CreateContext(layoutsDir, routed);

        var plugin = new TaxonomyPlugin(config);
        var derived = plugin.DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/categories/cat-one/");
        Assert.NotNull(term.Document.CustomFields);
        var fields = term.Document.CustomFields!;
        var list = Assert.IsType<List<object>>(fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(list[0]);
        Assert.Equal("Pinned", first["title"]);
    }

    [Fact]
    public void DerivePages_WithPinFieldBySource_UsesSourceSpecificField()
    {
        var layoutsDir = CreateTaxonomyLayoutsDir();
        var pinned = ContentDocument.Create(
            id: "s1:p1",
            title: "Pinned",
            slug: "pinned",
            publishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["sticky"] = new ContentField("boolean", true)
            });

        var normal = ContentDocument.Create(
            id: "s2:p2",
            title: "Normal Newer",
            slug: "normal-newer",
            publishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = "s2",
                ["categories"] = new List<object> { "Cat One" }
            }));

        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (pinned, new RouteInfo("/pinned/", "pinned/index.html", "pages/page.html")),
            (normal, new RouteInfo("/normal-newer/", "normal-newer/index.html", "pages/page.html"))
        };
        var (ctx, config) = CreateContext(
            layoutsDir,
            routed,
            new TaxonomyConfig
            {
                PinField = "pinned",
                PinFieldBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["s1"] = "sticky"
                }
            });

        var plugin = new TaxonomyPlugin(config);
        var derived = plugin.DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/categories/cat-one/");
        Assert.NotNull(term.Document.CustomFields);
        var fields = term.Document.CustomFields!;
        var list = Assert.IsType<List<object>>(fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(list[0]);
        Assert.Equal("Pinned", first["title"]);
    }

    [Fact]
    public void DerivePages_WithPinOrderField_SortsPinnedByOrder()
    {
        var layoutsDir = CreateTaxonomyLayoutsDir();
        var pinned2 = ContentDocument.Create(
            id: "s1:p2",
            title: "Pinned 2",
            slug: "pinned-2",
            publishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["pinOrder"] = new ContentField("number", 2)
            });

        var pinned1 = ContentDocument.Create(
            id: "s1:p1",
            title: "Pinned 1",
            slug: "pinned-1",
            publishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["pinOrder"] = new ContentField("number", 1)
            });

        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (pinned2, new RouteInfo("/pinned-2/", "pinned-2/index.html", "pages/page.html")),
            (pinned1, new RouteInfo("/pinned-1/", "pinned-1/index.html", "pages/page.html"))
        };
        var (ctx, config) = CreateContext(
            layoutsDir,
            routed,
            new TaxonomyConfig
            {
                PinField = "pinned",
                PinOrderField = "pinOrder"
            });

        var plugin = new TaxonomyPlugin(config);
        var derived = plugin.DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/categories/cat-one/");
        Assert.NotNull(term.Document.CustomFields);
        var fields = term.Document.CustomFields!;
        var list = Assert.IsType<List<object>>(fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(list[0]);
        var second = Assert.IsType<Dictionary<string, object>>(list[1]);
        Assert.Equal("Pinned 1", first["title"]);
        Assert.Equal("Pinned 2", second["title"]);
    }

    [Fact]
    public void GetOrBuildIndex_SameContextAndDifferentTaxonomyConfig_RebuildsSorting()
    {
        var first = ContentDocument.Create(
            id: "s1:first",
            title: "First",
            slug: "first",
            publishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["orderA"] = new ContentField("number", 1),
                ["orderB"] = new ContentField("number", 2)
            });
        var second = ContentDocument.Create(
            id: "s1:second",
            title: "Second",
            slug: "second",
            publishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: string.Empty,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["orderA"] = new ContentField("number", 2),
                ["orderB"] = new ContentField("number", 1)
            });
        var (context, _) = CreateContext(
            CreateTaxonomyLayoutsDir(),
            [
                (first, new RouteInfo("/first/", "first/index.html", "pages/page.html")),
                (second, new RouteInfo("/second/", "second/index.html", "pages/page.html"))
            ]);
        var configA = new TaxonomyConfig { PinField = "pinned", PinOrderField = "orderA" };
        var configB = new TaxonomyConfig { PinField = "pinned", PinOrderField = "orderB" };
        TaxonomyPlugin.ResetBuildIndexCountForTests();

        var termsA = TaxonomyIndexBuilder.GetOrBuildIndex(context, "categories", [], configA);
        var termsB = TaxonomyIndexBuilder.GetOrBuildIndex(context, "categories", [], configB);

        Assert.Equal("First", termsA["cat-one"].Pages[0].Title);
        Assert.Equal("Second", termsB["cat-one"].Pages[0].Title);
        Assert.Equal(2, TaxonomyPlugin.BuildIndexCountForTests);
    }

    private static string ResolveTemplateKind(string kind)
        => kind.Trim().ToLowerInvariant() switch
        {
            "taxonomy_index" => "pages/taxonomy-index.html",
            "taxonomy_term" => "pages/taxonomy-term.html",
            _ => throw new ConfigException($"Unexpected template kind: {kind}")
        };

    private static (BuildContext Context, AppConfig Config) CreateContext(
        string layoutsDir,
        IReadOnlyList<(ContentDocument Item, RouteInfo Route)> routed,
        TaxonomyConfig? taxonomy = null)
    {
        var routedDocuments = routed.ToRoutedDocuments();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "t", Title = "t" },
            Content = TestContent.Markdown(),
            Taxonomy = taxonomy ?? new TaxonomyConfig()
        };
        var context = new BuildContext
        {
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            RoutedDocuments = routedDocuments,
            ContentGraph = new CanonicalContentGraph(
                routedDocuments.Select(x => x.Document.Record).ToArray(),
                routedDocuments.SelectMany(x => x.Document.Record.Entities).ToArray()),
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        return (context, config);
    }

    private static string CreateTaxonomyLayoutsDir()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-taxonomy-pinning-tests-" + Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-term.html"), "{{ page.content }}");
        return layoutsDir;
    }
}
