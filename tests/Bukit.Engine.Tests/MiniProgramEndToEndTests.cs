using System.Text.Json.Nodes;
using Bukit.Config;
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

    private Task BuildAsync()
        => new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(
            ConfigLoader.Load(_sandbox.PathFor("site.yaml")), _sandbox.Root, new ConfigOverrides { Incremental = false });

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

    private JsonNode Dataset(string directory, string name)
    {
        var url = new Uri((string)Catalog(directory)["datasets"]![name]!["url"]!);
        return JsonNode.Parse(File.ReadAllText(_sandbox.PathFor("dist" + url.AbsolutePath)))!;
    }

    private static string[] Titles(JsonNode dataset)
        => [.. dataset["items"]!.AsArray().Select(item => (string)item!["title"]!)];
}
