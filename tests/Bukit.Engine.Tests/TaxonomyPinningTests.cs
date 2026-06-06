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
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            Routed = routed,
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var pinned = new ContentItem(
            Id: "s1:p1",
            Title: "Pinned",
            Slug: "pinned",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true)
            });

        var normal = new ContentItem(
            Id: "s2:p2",
            Title: "Normal Newer",
            Slug: "normal-newer",
            PublishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = "s2",
                ["categories"] = new List<object> { "Cat One" }
            }));

        routed.Add((pinned, new RouteInfo("/pinned/", "pinned/index.html", "pages/page.html")));
        routed.Add((normal, new RouteInfo("/normal-newer/", "normal-newer/index.html", "pages/page.html")));

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
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" },
                Taxonomy = new TaxonomyConfig
                {
                    PinField = "pinned",
                    PinFieldBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["s1"] = "sticky"
                    }
                }
            },
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            Routed = routed,
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var pinned = new ContentItem(
            Id: "s1:p1",
            Title: "Pinned",
            Slug: "pinned",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["sticky"] = new ContentField("boolean", true)
            });

        var normal = new ContentItem(
            Id: "s2:p2",
            Title: "Normal Newer",
            Slug: "normal-newer",
            PublishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = "s2",
                ["categories"] = new List<object> { "Cat One" }
            }));

        routed.Add((pinned, new RouteInfo("/pinned/", "pinned/index.html", "pages/page.html")));
        routed.Add((normal, new RouteInfo("/normal-newer/", "normal-newer/index.html", "pages/page.html")));

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
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" },
                Taxonomy = new TaxonomyConfig
                {
                    PinField = "pinned",
                    PinOrderField = "pinOrder"
                }
            },
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            Routed = routed,
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var pinned2 = new ContentItem(
            Id: "s1:p2",
            Title: "Pinned 2",
            Slug: "pinned-2",
            PublishAt: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["pinOrder"] = new ContentField("number", 2)
            });

        var pinned1 = new ContentItem(
            Id: "s1:p1",
            Title: "Pinned 1",
            Slug: "pinned-1",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: string.Empty,
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = new ContentField("text", "s1"),
                ["categories"] = new ContentField("list", new List<object> { "Cat One" }),
                ["pinned"] = new ContentField("boolean", true),
                ["pinOrder"] = new ContentField("number", 1)
            });

        routed.Add((pinned2, new RouteInfo("/pinned-2/", "pinned-2/index.html", "pages/page.html")));
        routed.Add((pinned1, new RouteInfo("/pinned-1/", "pinned-1/index.html", "pages/page.html")));

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
}
