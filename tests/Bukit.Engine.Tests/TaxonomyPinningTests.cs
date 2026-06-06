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
        var routed = new List<(ContentItem Item, RouteInfo Route)>();
        var layoutsDir = CreateTaxonomyLayoutsDir();

        var pinned = new ContentItem(
            Id: "s1:p1",
            Title: "Pinned",
            Slug: "pinned",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new[] { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true)
            });

        var normal = new ContentItem(
            Id: "s2:p2",
            Title: "Normal Newer",
            Slug: "normal-newer",
            PublishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s2"),
                ["categories"] = new ContentField("list", new[] { "Cat One" })
            });

        routed.Add((pinned, new RouteInfo("/pinned/", "pinned/index.html", "pages/page.html")));
        routed.Add((normal, new RouteInfo("/normal-newer/", "normal-newer/index.html", "pages/page.html")));
        var ctx = CreateContext(routed, layoutsDir);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/categories/cat-one/");
        Assert.NotNull(term.Item.Fields);
        var fields = term.Item.Fields!;
        var list = Assert.IsType<List<object>>(fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(list[0]);
        Assert.Equal("Pinned", first["title"]);
    }

    [Fact]
    public void DerivePages_WithPinFieldBySource_UsesSourceSpecificField()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>();
        var layoutsDir = CreateTaxonomyLayoutsDir();

        var pinned = new ContentItem(
            Id: "s1:p1",
            Title: "Pinned",
            Slug: "pinned",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new[] { "Cat One" }),
                ["sticky"] = new ContentField("boolean", true)
            });

        var normal = new ContentItem(
            Id: "s2:p2",
            Title: "Normal Newer",
            Slug: "normal-newer",
            PublishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s2"),
                ["categories"] = new ContentField("list", new[] { "Cat One" })
            });

        routed.Add((pinned, new RouteInfo("/pinned/", "pinned/index.html", "pages/page.html")));
        routed.Add((normal, new RouteInfo("/normal-newer/", "normal-newer/index.html", "pages/page.html")));
        var ctx = CreateContext(
            routed,
            layoutsDir,
            new TaxonomyConfig
            {
                PinField = "pinned",
                PinFieldBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["s1"] = "sticky"
                }
            });

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/categories/cat-one/");
        Assert.NotNull(term.Item.Fields);
        var fields = term.Item.Fields!;
        var list = Assert.IsType<List<object>>(fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(list[0]);
        Assert.Equal("Pinned", first["title"]);
    }

    [Fact]
    public void DerivePages_WithPinOrderField_SortsPinnedByOrder()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>();
        var layoutsDir = CreateTaxonomyLayoutsDir();

        var pinned2 = new ContentItem(
            Id: "s1:p2",
            Title: "Pinned 2",
            Slug: "pinned-2",
            PublishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new[] { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["pinOrder"] = new ContentField("number", 2)
            });

        var pinned1 = new ContentItem(
            Id: "s1:p1",
            Title: "Pinned 1",
            Slug: "pinned-1",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new[] { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["pinOrder"] = new ContentField("number", 1)
            });

        routed.Add((pinned2, new RouteInfo("/pinned-2/", "pinned-2/index.html", "pages/page.html")));
        routed.Add((pinned1, new RouteInfo("/pinned-1/", "pinned-1/index.html", "pages/page.html")));
        var ctx = CreateContext(
            routed,
            layoutsDir,
            new TaxonomyConfig
            {
                PinField = "pinned",
                PinOrderField = "pinOrder"
            });

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        var term = Assert.Single(derived, x => x.Route.Url == "/categories/cat-one/");
        Assert.NotNull(term.Item.Fields);
        var fields = term.Item.Fields!;
        var list = Assert.IsType<List<object>>(fields!["items"].Value);
        var first = Assert.IsType<Dictionary<string, object>>(list[0]);
        var second = Assert.IsType<Dictionary<string, object>>(list[1]);
        Assert.Equal("Pinned 1", first["title"]);
        Assert.Equal("Pinned 2", second["title"]);
    }

    private static string ResolveTemplateKind(string kind)
        => kind.Trim().ToLowerInvariant() switch
        {
            "taxonomy_index" => "pages/taxonomy-index.html",
            "taxonomy_term" => "pages/taxonomy-term.html",
            _ => throw new ConfigException($"Unexpected template kind: {kind}")
        };

    private static string CreateTaxonomyLayoutsDir()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-taxonomy-pinning-tests-" + Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-term.html"), "{{ page.content }}");
        return layoutsDir;
    }

    private static BuildContext CreateContext(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        string layoutsDir,
        TaxonomyConfig? taxonomy = null)
        => new()
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" },
                Taxonomy = taxonomy ?? new TaxonomyConfig()
            },
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            Routed = routed,
            RoutedDocuments = routed.Select(x => (Document: ToDocument(x.Item), x.Route)).ToList(),
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

    private static ContentDocument ToDocument(ContentItem item)
    {
        var fields = item.Fields is null
            ? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ContentField>(item.Fields, StringComparer.OrdinalIgnoreCase);
        var categories = fields.TryGetValue("categories", out var categoriesField)
            ? ToStringList(categoriesField.Value)
            : [];
        var record = new ContentRecord(
            new ContentIdentity(item.Id, item.Slug, item.Id, "post", "published"),
            new ContentPresentation(item.Title, null, item.ContentHtml, "en", []),
            new ContentClassification("post", "post", categories, []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(item.PublishAt, null, null, null),
            new ProvenanceRecord(fields.TryGetValue("sourceKey", out var source) ? source.Value?.ToString() : null, null, [], [], null),
            new TrustMetadata(null, "published", []),
            [],
            [],
            []);

        return new ContentDocument(
            record,
            new ContentBodyRef(item.ContentHtml, null, null, null),
            new ContentRoutePolicy(null, null, null, null, "post"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            fields,
            []);
    }

    private static IReadOnlyList<string> ToStringList(object? value)
        => value switch
        {
            IEnumerable<string> strings => strings.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            IEnumerable<object> values => values.Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            string text when !string.IsNullOrWhiteSpace(text) => [text],
            _ => []
        };
}
