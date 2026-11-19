using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn.MiniProgram;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MiniProgramPluginTests : IDisposable
{
    private static readonly DateTimeOffset Day = new(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(8));
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), $"bukit-mp-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Disabled_WritesNothing()
    {
        await RunAsync(Config(enabled: false), [Article("a1", "First")], []);

        Assert.False(Directory.Exists(Path.Combine(_outputDir, "mp")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://localhost:4000")]
    public async Task RequiresHttpsSiteUrl(string? siteUrl)
    {
        var ex = await Assert.ThrowsAsync<ConfigException>(() => RunAsync(Config(siteUrl: siteUrl), [], []));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
    }

    [Fact]
    public async Task Collection_WritesListDetailsAndCatalog()
    {
        var context = await RunAsync(Config(), [Article("a1", "First", internalNote: "secret"), Article("a2", "Second", day: 2)], []);

        var catalog = ReadCatalog();
        Assert.Equal("1.0", (string)catalog["schemaVersion"]!);
        Assert.Equal("zh-CN", (string)catalog["language"]!);
        Assert.StartsWith("sha256:", (string)catalog["catalogHash"]!);
        var articles = catalog["datasets"]!["articles"]!;
        Assert.Equal("collection", (string)articles["kind"]!);
        Assert.StartsWith("https://example.com/mp/articles.list.", (string)articles["url"]!);

        var list = ReadByUrl((string)articles["url"]!)["items"]!.AsArray();
        Assert.Equal(["second", "first"], list.Select(item => (string)item!["id"]!));
        Assert.Equal("Second", (string)list[0]!["title"]!);
        Assert.Null(list[0]!["internalNote"]);
        Assert.Null(list[0]!["body"]);

        var details = ReadByUrl((string)articles["detailCatalog"]!["url"]!)["items"]!.AsArray();
        Assert.Equal(["first", "second"], details.Select(item => (string)item!["id"]!));
        var first = ReadByUrl((string)details[0]!["url"]!);
        Assert.Equal("First", (string)first["title"]!);
        Assert.Equal("https://example.com/first/", (string)first["url"]!);
        Assert.Contains("<p>First body</p>", (string)first["body"]!, StringComparison.Ordinal);

        var outputs = context.Get(BuildDataKeys.PluginOutputs);
        Assert.Contains(outputs, output => output.Path == "mp/catalog.json" && output.Plugin == "miniprogram");
        Assert.Equal(Directory.GetFiles(_outputDir, "*", SearchOption.AllDirectories).Length, outputs.Count);
    }

    [Fact]
    public async Task Files_AreDeterministicLfWithoutBom()
    {
        await RunAsync(Config(), [Article("a1", "First"), Article("a2", "Second")], [Banner("b1", "One", 1), Banner("b2", "Two", 2)]);
        var first = Snapshot();
        Directory.Delete(_outputDir, recursive: true);
        await RunAsync(Config(), [Article("a2", "Second"), Article("a1", "First")], [Banner("b2", "Two", 2), Banner("b1", "One", 1)]);

        Assert.Equal(first, Snapshot());
        foreach (var bytes in first.Values)
        {
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal((byte)'\n', bytes[^1]);
            Assert.NotEqual((byte)'\n', bytes[^2]);
        }
    }

    [Fact]
    public async Task ChangingOneBanner_ChangesOnlyThatDataset()
    {
        await RunAsync(Config(), [Article("a1", "First")], [Banner("b1", "One", 1)]);
        var before = ReadCatalog();
        Directory.Delete(_outputDir, recursive: true);
        await RunAsync(Config(), [Article("a1", "First")], [Banner("b1", "Uno", 1)]);
        var after = ReadCatalog();

        Assert.NotEqual(Hash(before, "banners"), Hash(after, "banners"));
        Assert.NotEqual((string)before["catalogHash"]!, (string)after["catalogHash"]!);
        Assert.Equal(Hash(before, "articles"), Hash(after, "articles"));
        Assert.Equal(Hash(before, "site"), Hash(after, "site"));
    }

    [Fact]
    public async Task ChangingArticleBody_ChangesDetailButNotList()
    {
        await RunAsync(Config(), [Article("a1", "First")], []);
        var before = ReadCatalog();
        Directory.Delete(_outputDir, recursive: true);
        await RunAsync(Config(), [Article("a1", "First", body: "<p>Edited</p>")], []);
        var after = ReadCatalog();

        Assert.Equal(Hash(before, "articles"), Hash(after, "articles"));
        Assert.NotEqual((string)before["datasets"]!["articles"]!["detailCatalog"]!["hash"]!, (string)after["datasets"]!["articles"]!["detailCatalog"]!["hash"]!);
        Assert.NotEqual((string)before["catalogHash"]!, (string)after["catalogHash"]!);
    }

    [Fact]
    public async Task Module_SortsByDeclaredFieldAndSkipsDisabledRecords()
    {
        await RunAsync(Config(), [], [Banner("b3", "Third", 3), Banner("b1", "First", 1), Banner("b2", "Hidden", 2, enabled: false), Faq("f1")]);

        var items = ReadDataset("banners")["items"]!.AsArray();
        Assert.Equal(["First", "Third"], items.Select(item => (string)item!["title"]!));
        Assert.Equal("https://example.com/assets/media/b1.png", (string)items[0]!["image"]!);
        Assert.Equal(1, items[0]!["order"]!.GetValue<double>());
        Assert.DoesNotContain("b1", (string)items[0]!["id"]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedImageUrl_FailsWithRecordAndField()
    {
        var banner = Banner("b1", "One", 1, image: "https://prod-files-secure.s3.us-west-2.amazonaws.com/x.png?X-Amz-Signature=abc");

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [banner]));

        Assert.Equal(DiagnosticCode.SchemaValidationFailed, ex.Code);
        Assert.Contains("dataset 'banners'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("field 'image'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongType_FailsWithRecordAndField()
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Banner("b1", "One", "abc")]));

        Assert.Equal(DiagnosticCode.SchemaValidationFailed, ex.Code);
        Assert.Contains("field 'order' is not a number", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingRequiredField_Fails()
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Banner("b1", "One", 1, image: null)]));

        Assert.Contains("field 'image' is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Body_IsReducedToSafeSubset()
    {
        const string body = """
            <div class="notion-callout"><p onclick="x()">Hi <a href="/second/#top">next</a></p></div>
            <script>alert(1)</script><iframe src="https://video.example/v1"></iframe>
            <details><summary>More</summary><p>Hidden</p></details>
            <img src="/assets/media/a.png" alt="A" class="lazy" loading="lazy"><a href="javascript:alert(1)">bad</a>
            """;
        await RunAsync(Config(), [Article("a1", "First", body: body), Article("a2", "Second")], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details.First(item => (string)item!["id"]! == "first")!["url"]!)["body"]!;
        Assert.Contains("<p>Hi <a href=\"https://example.com/second/#top\" data-bukit-id=\"second\">next</a></p>", html, StringComparison.Ordinal);
        Assert.Contains("<p><a href=\"https://video.example/v1\">https://video.example/v1</a></p>", html, StringComparison.Ordinal);
        Assert.Contains("<p><strong>More</strong></p><p>Hidden</p>", html, StringComparison.Ordinal);
        Assert.Contains("<img src=\"https://example.com/assets/media/a.png\" alt=\"A\">", html, StringComparison.Ordinal);
        Assert.Contains("<a>bad</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("script", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("iframe", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_KeepsOrderNestingAndAbsoluteUrls()
    {
        await RunAsync(Config(), [], []);

        var items = ReadDataset("nav")["items"]!.AsArray();
        Assert.Equal(["Home", "Docs"], items.Select(item => (string)item!["title"]!));
        Assert.Equal("https://example.com/", (string)items[0]!["url"]!);
        var child = items[1]!["children"]!.AsArray().Single()!;
        Assert.Equal("Guide", (string)child["title"]!);
        Assert.Equal("https://example.com/docs/guide/", (string)child["url"]!);
    }

    [Fact]
    public async Task DataIndexAndTaxonomy_AreWritten()
    {
        var dataIndex = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["app"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["title"] = "Shop", ["phone"] = "123" }
            }
        };
        await RunAsync(Config(), [Article("a1", "First", tags: ["x", "y"]), Article("a2", "Second", tags: ["x"])], [], dataIndex);

        var values = ReadDataset("settings")["values"]!.AsObject();
        Assert.Equal(["phone", "title"], values.Select(pair => pair.Key));
        var tags = ReadDataset("tags")["items"]!.AsArray();
        Assert.Equal([("x", 2), ("y", 1)], tags.Select(item => ((string)item!["name"]!, (int)item["count"]!)));
    }

    [Fact]
    public async Task LinkToPageOutsideCollections_HasNoBukitId()
    {
        await RunAsync(Config(), [Article("a1", "First", body: "<p><a href=\"/about/\">about</a></p>"), Article("p1", "About", source: "pages")], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        Assert.Equal(["first"], details.Select(item => (string)item!["id"]!));
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.Contains("<a href=\"https://example.com/about/\">about</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StringTypeOnList_Fails()
    {
        var config = Config(articleListFields: ["title", "tags"], articleTypes: new Dictionary<string, string> { ["tags"] = "string" });

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(config, [Article("a1", "First", tags: ["x", "y"])], []));

        Assert.Equal(DiagnosticCode.SchemaValidationFailed, ex.Code);
        Assert.Contains("field 'tags' is not a string", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UntypedMapField_Fails()
    {
        var config = Config(articleListFields: ["title", "extra"]);
        var map = new Dictionary<string, object> { ["lat"] = 1.5 };

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(config, [Article("a1", "First", extra: map)], []));

        Assert.Contains("field 'extra' has a value that is not a scalar", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyOptionalList_IsWrittenAsMissing()
    {
        await RunAsync(Config(articleListFields: ["title", "extra"]), [Article("a1", "First", extra: new List<object>())], []);

        var item = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["url"]!)["items"]![0]!;
        Assert.Null(item["extra"]);
    }

    [Fact]
    public async Task NonIndexablePages_AreExcluded()
    {
        await RunAsync(Config(), [Article("a1", "First"), Article("a2", "Second")], [], nonIndexable: "/second/");

        var list = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["url"]!)["items"]!.AsArray();
        Assert.Equal(["first"], list.Select(item => (string)item!["id"]!));
    }

    private async Task<BuildContext> RunAsync(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> pages,
        IReadOnlyList<ContentDocument> data,
        IReadOnlyDictionary<string, object>? dataIndex = null,
        string? nonIndexable = null)
    {
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        if (nonIndexable is not null)
        {
            var page = pages.Single(p => p.Route.Url == nonIndexable);
            seoIndex[page.Route.OutputPath] = new SeoIndexEntry(page.Route, "https://example.com" + page.Route.Url, "noindex", Indexable: false, Day, null, null);
        }

        var context = new BuildContext
        {
            RootDir = _outputDir,
            OutputDir = _outputDir,
            BaseUrl = "/",
            LayoutsDir = _outputDir,
            RoutedDocuments = pages,
            SeoIndex = seoIndex,
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        context.Set(BuildDataKeys.DataModules, new DataModuleResult(data, null, null, dataIndex, null));
        await new MiniProgramPlugin(config).AfterBuildAsync(context);
        return context;
    }

    private static AppConfig Config(
        bool enabled = true,
        string? siteUrl = "https://example.com",
        string[]? articleListFields = null,
        Dictionary<string, string>? articleTypes = null) => new()
    {
        Site = new SiteConfig
        {
            Name = "shop",
            Title = "Shop",
            Url = siteUrl,
            Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(StringComparer.OrdinalIgnoreCase)
            {
                ["main"] =
                [
                    new MenuConfig { Identifier = "home", Name = "Home", Url = "/" },
                    new MenuConfig
                    {
                        Identifier = "docs",
                        Name = "Docs",
                        Url = "/docs/",
                        Children = [new MenuConfig { Identifier = "guide", Name = "Guide", Url = "/docs/guide/" }]
                    }
                ]
            }
        },
        Content = TestContent.Markdown(),
        MiniProgram = new MiniProgramConfig
        {
            Enabled = enabled,
            Datasets =
            [
                new MiniProgramDatasetConfig
                {
                    Name = "articles",
                    Kind = "collection",
                    Source = "blog",
                    ListFields = articleListFields ?? ["title", "summary"],
                    DetailFields = ["title", "url", "body", "publishedAt"],
                    Types = articleTypes
                },
                new MiniProgramDatasetConfig
                {
                    Name = "banners",
                    Kind = "module",
                    Source = "modules",
                    Module = "banner",
                    Fields = ["title", "image", "order"],
                    Sort = new MiniProgramSortConfig("order", Descending: false),
                    Required = ["title", "image"],
                    Types = new Dictionary<string, string> { ["image"] = "image", ["order"] = "number" }
                },
                new MiniProgramDatasetConfig { Name = "settings", Kind = "dataIndex", Source = "settings", Scope = "app" },
                new MiniProgramDatasetConfig { Name = "tags", Kind = "taxonomy", Taxonomy = "tags" },
                new MiniProgramDatasetConfig { Name = "nav", Kind = "menu", Menu = "main" }
            ]
        }
    };

    private static RoutedContentDocument Article(
        string id,
        string title,
        int day = 1,
        string? body = null,
        string? internalNote = null,
        string[]? tags = null,
        string source = "blog",
        object? extra = null)
    {
        var slug = title.ToLowerInvariant();
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = source,
            ["sourceMode"] = "content",
            ["summary"] = title + " summary"
        };
        if (internalNote is not null) fields["internalNote"] = internalNote;
        if (tags is not null) fields["tags"] = tags;
        if (extra is not null) fields["extra"] = extra;
        var document = ContentDocument.Create($"blog:{id}", title, slug, Day.AddDays(day), body ?? $"<p>{title} body</p>", ContentFieldReader.ToFieldMap(fields));
        return new RoutedContentDocument(document, new RouteInfo($"/{slug}/", $"{slug}/index.html", "pages/post.html"));
    }

    private static ContentDocument Banner(string id, string title, object order, bool enabled = true, string? image = "/assets/media/{id}.png")
    {
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "modules",
            ["sourceMode"] = "data",
            ["type"] = "banner",
            ["order"] = order,
            ["enabled"] = enabled
        };
        if (image is not null) fields["image"] = image.Replace("{id}", id, StringComparison.Ordinal);
        return ContentDocument.Create($"modules:{id}", title, id, Day, null, ContentFieldReader.ToFieldMap(fields));
    }

    private static ContentDocument Faq(string id)
        => ContentDocument.Create($"modules:{id}", "Question", id, Day, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "modules",
            ["sourceMode"] = "data",
            ["type"] = "faq"
        }));

    private JsonNode ReadCatalog() => JsonNode.Parse(File.ReadAllText(Path.Combine(_outputDir, "mp", "catalog.json")))!;

    private JsonNode ReadDataset(string name) => ReadByUrl((string)ReadCatalog()["datasets"]![name]!["url"]!);

    private JsonNode ReadByUrl(string url)
    {
        var relative = new Uri(url).AbsolutePath.TrimStart('/');
        return JsonNode.Parse(File.ReadAllText(Path.Combine(_outputDir, relative)))!;
    }

    private static string Hash(JsonNode catalog, string dataset) => (string)catalog["datasets"]![dataset]!["hash"]!;

    private SortedDictionary<string, byte[]> Snapshot()
        => new(Directory.GetFiles(_outputDir, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(_outputDir, path).Replace('\\', '/'), File.ReadAllBytes), StringComparer.Ordinal);
}
