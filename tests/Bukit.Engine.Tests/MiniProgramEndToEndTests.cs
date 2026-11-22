using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Plugins.BuiltIn.MiniProgram;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

/// <summary>Builds a real multilingual site, so the data-module wiring and the output lifecycle are covered end to end.</summary>
public sealed class MiniProgramEndToEndTests : IDisposable
{
    private readonly SiteSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public async Task MultilingualBuild_WritesEachLanguageAndRemovesOutputWhenDisabled()
    {
        WriteSite(module: "banner", enabled: true);

        await BuildAsync();

        // The English banner row has a locale, so only the English variant lists it.
        Assert.Equal(["Sale"], Titles(Dataset("dist/mp", "banners")));
        Assert.Empty(Titles(Dataset("dist/zh-CN/mp", "banners")));
        Assert.Equal(["Hello"], Titles(Dataset("dist/mp", "articles")));
        Assert.Equal("zh-CN", (string)Catalog("dist/zh-CN/mp")["language"]!);

        WriteSite(module: "banner", enabled: false);
        await BuildAsync();

        Assert.False(Directory.Exists(_sandbox.PathFor("dist/mp")));
        Assert.False(Directory.Exists(_sandbox.PathFor("dist/zh-CN/mp")));
    }

    [Fact]
    public async Task MultilingualBuild_ModuleMatchingNoLanguage_Fails()
    {
        WriteSite(module: "banners", enabled: true);

        var ex = await Assert.ThrowsAnyAsync<Exception>(BuildAsync);

        Assert.Contains("'banners' matches no record of source 'modules'", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rebuild_KeepsBytesAndRemovesStaleFiles()
    {
        WriteSite(module: "banner", enabled: true);
        await BuildAsync();
        var first = Snapshot();
        var oldBanners = DatasetPath("dist/mp", "banners");
        var articles = DatasetPath("dist/mp", "articles");

        await BuildAsync();
        Assert.Equal(first, Snapshot());

        // The manifest owns mp/, so without a clean the replaced banners file is still removed.
        _sandbox.Write("data/sale.md", "---\ntype: banner\ntitle: Big Sale\nslug: sale\nlocale: en\norder: 1\n---\n");
        await BuildAsync();

        Assert.False(File.Exists(oldBanners));
        Assert.Equal(["Big Sale"], Titles(Dataset("dist/mp", "banners")));
        Assert.Equal(articles, DatasetPath("dist/mp", "articles"));
        Assert.True(File.Exists(articles));
    }

    [Fact]
    public async Task StaticFileAtMiniProgramPath_FailsTheBuild()
    {
        WriteSite(module: "banner", enabled: true);
        _sandbox.Write("static/mp/catalog.json", "{}");

        var ex = await Assert.ThrowsAnyAsync<Exception>(BuildAsync);

        Assert.Contains("Asset output collision at 'mp/catalog.json'", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncrementalBuild_WritesTheSameFilesAsAFullBuild()
    {
        WriteSite(module: "banner", enabled: true);
        await BuildAsync();
        var before = Snapshot();

        _sandbox.Write("content/hello.md", "---\nslug: hello\ntitle: Hello\nlanguage: en\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\n---\nHello again\n");
        await BuildAsync(incremental: true);
        var incremental = Snapshot();
        await BuildAsync(incremental: false);

        Assert.NotEqual(before, incremental);
        Assert.Equal(Snapshot(), incremental);
    }

    [Fact]
    public async Task Catalogs_MatchThePublishedSchema()
    {
        WriteSite(module: "banner", enabled: true);
        await BuildAsync();
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(SiteSandbox.RepositoryRoot, "docs", "schemas", "miniprogram-catalog.v1.schema.json")));
        Assert.Equal(MiniProgramPlugin.CatalogSchema, schema.RootElement.GetProperty("$id").GetString());

        foreach (var directory in new[] { "dist/mp", "dist/zh-CN/mp" })
        {
            var catalog = Catalog(directory).AsObject();
            LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(catalog), schema.RootElement);

            var invalid = catalog.DeepClone().AsObject();
            invalid["datasets"]!["banners"]!["hash"] = "sha256:short";
            var error = Assert.Throws<InvalidDataException>(() => LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(invalid), schema.RootElement));
            Assert.Contains("hash", error.Message, StringComparison.Ordinal);
        }
    }

    // Clean is off, so stale files are removed by the build manifest, not by wiping the output.
    // Nested YAML nulls reach mini-program JSON as null from content and data Markdown sources alike;
    // a quoted "null" stays text.
    [Fact]
    public async Task ObjectFields_KeepNestedYamlNullsFromContentAndDataSources()
    {
        WriteSite(module: "banner", enabled: true);
        const string types = "types: { days: { type: 'object[]', fields: { title: string, meals: string, note: string, sights: 'string[]' } } }";
        var yaml = File.ReadAllText(_sandbox.PathFor("site.yaml"))
            .Replace("listFields: [title]", "listFields: [title, days]\n      " + types, StringComparison.Ordinal)
            .Replace("fields: [title, order]", "fields: [title, order, days]\n      " + types, StringComparison.Ordinal);
        _sandbox.Write("site.yaml", yaml);
        const string days = "days: [{\"title\": \"D1\", \"meals\": null, \"note\": \"null\", \"sights\": []}, {title: D2, meals: ~}]\n";
        _sandbox.Write("content/hello.md", "---\nslug: hello\ntitle: Hello\nlanguage: en\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\n" + days + "---\nHello body\n");
        _sandbox.Write("data/sale.md", "---\ntype: banner\ntitle: Sale\nslug: sale\nlocale: en\norder: 1\n" + days + "---\n");

        await BuildAsync();

        foreach (var name in new[] { "articles", "banners" })
        {
            var dayList = Dataset("dist/mp", name)["items"]![0]!["days"]!.AsArray();
            Assert.Equal("""{"meals":null,"note":"null","sights":null,"title":"D1"}""", dayList[0]!.ToJsonString());
            Assert.Null(dayList[1]!["meals"]);
        }
    }

    // An empty "- " item is a YAML null; a string[] field drops it, at the top level and inside objects,
    // and a list of only nulls is empty.
    [Fact]
    public async Task StringListFields_DropEmptyYamlItems()
    {
        WriteSite(module: "banner", enabled: true);
        const string types = "types: { highlights: 'string[]', days: { type: 'object[]', fields: { sights: 'string[]' } } }";
        var yaml = File.ReadAllText(_sandbox.PathFor("site.yaml"))
            .Replace("listFields: [title]", "listFields: [title, highlights, days]\n      " + types, StringComparison.Ordinal);
        _sandbox.Write("site.yaml", yaml);
        _sandbox.Write("content/hello.md", "---\nslug: hello\ntitle: Hello\nlanguage: en\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\nhighlights:\n  - a\n  -\ndays:\n  - sights:\n      - b\n      - ~\n  - sights:\n      -\n---\nHello body\n");
        _sandbox.Write("content/empty.md", "---\nslug: empty\ntitle: Empty\nlanguage: en\npublishAt: 2024-05-01T00:00:00Z\ncollection: page\nhighlights:\n  -\n---\nEmpty body\n");

        await BuildAsync();

        var items = Dataset("dist/mp", "articles")["items"]!.AsArray();
        var item = items.Single(entry => (string?)entry!["title"] == "Hello")!;
        Assert.Equal("""["a"]""", item["highlights"]!.ToJsonString());
        Assert.Equal("""[{"sights":["b"]},{"sights":null}]""", item["days"]!.ToJsonString());
        // A list of only nulls is absent, like a top-level null.
        Assert.Null(items.Single(entry => (string?)entry!["title"] == "Empty")!["highlights"]);
    }

    private Task BuildAsync() => BuildAsync(incremental: false);

    private Task BuildAsync(bool incremental)
        => new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(
            ConfigLoader.Load(_sandbox.PathFor("site.yaml")), _sandbox.Root, new ConfigOverrides { Incremental = incremental, Clean = false });

    /// <summary>Every mini-program file of both languages, by path, so two builds compare byte for byte.</summary>
    private SortedDictionary<string, string> Snapshot()
        => new(new[] { "dist/mp", "dist/zh-CN/mp" }
            .SelectMany(directory => Directory.GetFiles(_sandbox.PathFor(directory), "*", SearchOption.AllDirectories))
            .ToDictionary(
                path => Path.GetRelativePath(_sandbox.Root, path).Replace('\\', '/'),
                path => Convert.ToHexString(File.ReadAllBytes(path))),
            StringComparer.Ordinal);

    private string DatasetPath(string directory, string name)
        => _sandbox.PathFor("dist" + new Uri((string)Catalog(directory)["datasets"]![name]!["url"]!).AbsolutePath);

    private void WriteSite(string module, bool enabled)
    {
        _sandbox.Write("site.yaml", $"""
            site:
              name: shop
              title: Shop
              url: https://example.test
              languages: [en, zh-CN]
              defaultLanguage: en
              collections:
                page:
                  permalink: /{"{slug}"}/
                  template: pages/page.html
            content:
              media:
                downloadToLocal: false
              sources:
                - type: markdown
                  name: pages
                  collection: page
                  markdown:
                    dir: content
                - type: markdown
                  name: modules
                  mode: data
                  markdown:
                    dir: data
            build:
              output: dist
              languageJobs: 1
            theme:
              layouts: layouts
            miniprogram:
              enabled: {(enabled ? "true" : "false")}
              datasets:
                - name: articles
                  kind: collection
                  source: pages
                  listFields: [title]
                  detailFields: [title, body]
                - name: banners
                  kind: module
                  source: modules
                  module: {module}
                  fields: [title, order]
                  sort: {"{ field: order }"}
            """);
        _sandbox.Write("content/hello.md", "---\nslug: hello\ntitle: Hello\nlanguage: en\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\n---\nHello body\n");
        _sandbox.Write("content/ni-hao.md", "---\nslug: ni-hao\ntitle: Ni Hao\nlanguage: zh-CN\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\n---\nBody\n");
        _sandbox.Write("data/sale.md", "---\ntype: banner\ntitle: Sale\nslug: sale\nlocale: en\norder: 1\n---\n");
        _sandbox.Write("layouts/pages/index.html", "<html><head><title>{{ site.title }}</title></head><body>{{ site.title }}</body></html>");
        _sandbox.Write("layouts/pages/page.html", "<html><head><title>{{ page.title }}</title></head><body>{{ page.content }}</body></html>");
    }

    private JsonNode Catalog(string directory) => JsonNode.Parse(File.ReadAllText(_sandbox.PathFor($"{directory}/catalog.json")))!;

    private JsonNode Dataset(string directory, string name) => JsonNode.Parse(File.ReadAllText(DatasetPath(directory, name)))!;

    private static string[] Titles(JsonNode dataset)
        => [.. dataset["items"]!.AsArray().Select(item => (string)item!["title"]!)];
}
