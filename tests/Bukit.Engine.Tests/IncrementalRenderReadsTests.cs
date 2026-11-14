using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

// add-per-page-render-dependencies: a route depends only on the shared collections it read.
public sealed class IncrementalRenderReadsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditingOneArticle_RendersItAndPagesThatReadThePageList(bool multilingual)
    {
        using var site = CreateSite();
        await BuildAsync(site, multilingual);
        var unchanged = await BuildAsync(site, multilingual);
        Assert.Equal(0, unchanged.Incremental.CacheMissCount);

        site.Sandbox.Write("content/b.md", Article("b", "B renamed"));
        var edited = await BuildAsync(site, multilingual);

        // b itself (metadata), the page listing every page (pages changed) and the home list that
        // lists b; a and m skip. Content has no language, so only the default language has pages.
        Assert.Equal(3, edited.Incremental.CacheMissCount);
        var reasons = RenderReasons(site);
        Assert.Equal(1, reasons["metadata_changed"]);
        Assert.Equal(1, reasons["data_dependency_changed"]);
        Assert.Equal(1, reasons["list_render"]);
        Assert.Equal(2, reasons["unchanged"]);
        Assert.False(reasons.ContainsKey("render_dependency_changed"));
        Assert.Contains("B renamed", File.ReadAllText(OutputFor(site, multilingual, "l")));
    }

    [Fact]
    public async Task RootLayout_EditingOneArticleDoesNotRerenderTheLegacyHomeRedirect()
    {
        using var site = CreateSite();
        AppConfig Root(AppConfig config) => config with
        {
            Site = config.Site with { Languages = ["en", "zh"], DefaultLanguage = "en", DefaultLanguageInSubdir = false }
        };
        await BuildAsync(site, configure: Root);
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "index.html")));
        Assert.Equal(0, (await BuildAsync(site, configure: Root)).Incremental.CacheMissCount);

        site.Sandbox.Write("content/b.md", Article("b", "B renamed"));
        var edited = await BuildAsync(site, configure: Root);

        // The same three pages as in the prefixed layout; the redirect page en/index.html reads nothing.
        Assert.Equal(3, edited.Incremental.CacheMissCount);
        Assert.False(RenderReasons(site).ContainsKey("render_dependency_changed"));
    }

    [Fact]
    public async Task DataFileChange_RendersOnlyPagesThatReadIt()
    {
        using var site = CreateSite();
        await BuildAsync(site);
        site.Sandbox.Write("data/menu.json", "{\"label\":\"Changed\"}");

        var result = await BuildAsync(site);

        Assert.Equal(1, result.Incremental.CacheMissCount);
        Assert.Equal(1, RenderReasons(site)["data_dependency_changed"]);
        Assert.Contains("Changed", File.ReadAllText(OutputFor(site, false, "m")));
        var entries = ManifestEntries(site);
        Assert.Equal(["site.data_files"], Reads(entries, "m/index.html"));
        Assert.Equal(["pages"], Reads(entries, "l/index.html"));
        Assert.Empty(Reads(entries, "a/index.html"));
    }

    [Fact]
    public async Task ReadRecordFollowsTheTemplatesARenderUsed()
    {
        using var site = CreateSite();
        await BuildAsync(site);

        // a starts including the menu: its own field changed, and its new record covers the menu.
        site.Sandbox.Write("content/a.md", Article("a", "A", "menu: true"));
        await BuildAsync(site);
        Assert.Equal(["site.data_files"], Reads(ManifestEntries(site), "a/index.html"));

        site.Sandbox.Write("data/menu.json", "{\"label\":\"Changed\"}");
        var result = await BuildAsync(site);
        Assert.Equal(2, result.Incremental.CacheMissCount);
    }

    [Fact]
    public async Task SiteWideInputChange_RendersEveryPage()
    {
        using var site = CreateSite();
        var first = await BuildAsync(site);
        site.Sandbox.Write("site.yaml", File.ReadAllText(site.Sandbox.PathFor("site.yaml")).Replace("title: Phase 0", "title: Renamed", StringComparison.Ordinal));

        var result = await BuildAsync(site);

        Assert.Equal(first.Incremental.CacheMissCount, result.Incremental.CacheMissCount);
        Assert.Equal(4, RenderReasons(site)["render_dependency_changed"]);
    }

    [Fact]
    public async Task PluginOptionChange_RendersEveryPageAsRenderDependencyChanged()
    {
        using var site = CreateSite();
        AppConfig WithOption(AppConfig config, string value) => config with
        {
            Site = config.Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["path-report"] = new() { Enabled = true, Options = new Dictionary<string, object> { ["mode"] = value } }
                }
            }
        };

        var first = await BuildAsync(site, configure: config => WithOption(config, "a"));
        var same = await BuildAsync(site, configure: config => WithOption(config, "a"));
        Assert.Equal(0, same.Incremental.CacheMissCount);

        var changed = await BuildAsync(site, configure: config => WithOption(config, "b"));

        Assert.Equal(first.Incremental.CacheMissCount, changed.Incremental.CacheMissCount);
        Assert.Equal(4, RenderReasons(site)["render_dependency_changed"]);
    }

    [Fact]
    public async Task PageList_DoesNotShowTheBodyFingerprintOfOtherPages()
    {
        using var site = CreateSite();
        site.Sandbox.Write("layouts/partials/list.html", "<ul>{{ for p in pages }}<li>[{{ p.fields.bodyFingerprint }}]{{ p.fields.list }}</li>{{ end }}</ul>");
        await BuildAsync(site);

        var html = File.ReadAllText(OutputFor(site, multilingual: false, "l"));
        Assert.Contains("<li>[]{type: \"bool\", value: true}</li>", html, StringComparison.Ordinal);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(html, @"<li>\[\]").Count);
    }

    [Fact]
    public async Task ManifestWithoutReadRecords_DependsOnEveryCollectionOnce()
    {
        using var site = CreateSite();
        var first = await BuildAsync(site);
        var manifestPath = Path.Combine(site.Cache, "build-manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        foreach (var (_, entry) in manifest["entries"]!.AsObject())
        {
            entry!.AsObject().Remove("renderReads");
            entry.AsObject().Remove("renderBaseHash");
        }

        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var upgraded = await BuildAsync(site);
        Assert.Equal(first.Incremental.CacheMissCount, upgraded.Incremental.CacheMissCount);
        Assert.Equal(4, RenderReasons(site)["render_dependency_changed"]);
        Assert.NotNull(Reads(ManifestEntries(site), "a/index.html"));

        var settled = await BuildAsync(site);
        Assert.Equal(0, settled.Incremental.CacheMissCount);
    }

    private static SiteBuilder CreateSite()
    {
        var site = new SiteBuilder();
        site.Sandbox.Write("layouts/pages/page.html", """
            <html><head><title>{{ page.title }}</title></head><body>
            {{ if page.fields.menu }}{{ include "partials/menu.html" }}{{ end }}
            {{ if page.fields.list }}{{ include "partials/list.html" }}{{ end }}
            {{ page.title }}{{ page.content }}</body></html>
            """);
        site.Sandbox.Write("layouts/partials/menu.html", "<nav>{{ site.data_files.menu.label }}</nav>");
        site.Sandbox.Write("layouts/partials/list.html", "<ul>{{ for p in pages }}<li>{{ p.title }}</li>{{ end }}</ul>");
        site.Sandbox.Write("data/menu.json", "{\"label\":\"Home\"}");
        site.Sandbox.Write("content/a.md", Article("a", "A"));
        site.Sandbox.Write("content/b.md", Article("b", "B"));
        site.Sandbox.Write("content/m.md", Article("m", "M", "menu: true"));
        site.Sandbox.Write("content/l.md", Article("l", "L", "list: true"));
        return site;
    }

    private static string Article(string slug, string title, string? extra = null) =>
        $"---\nslug: {slug}\ntitle: {title}\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\n{(extra is null ? string.Empty : extra + "\n")}---\nBody {slug}\n";

    private static async Task<BuildResult> BuildAsync(SiteBuilder site, bool multilingual = false, Func<AppConfig, AppConfig>? configure = null)
    {
        var config = configure is null ? site.Config : configure(site.Config);
        if (multilingual)
        {
            config = config with
            {
                // The prefixed layout; RootLayout_EditingOneArticleDoesNotRerenderTheLegacyHomeRedirect covers the root layout.
                Site = config.Site with { Languages = ["en", "zh"], DefaultLanguage = "en", DefaultLanguageInSubdir = true },
                Build = config.Build with { LanguageJobs = 2 }
            };
        }

        return await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(
            config, site.Root, new ConfigOverrides { Clean = false, Incremental = true });
    }

    private static string OutputFor(SiteBuilder site, bool multilingual, string slug) =>
        multilingual
            ? Path.Combine(site.Output, "en", slug, "index.html")
            : Path.Combine(site.Output, slug, "index.html");

    private static Dictionary<string, int> RenderReasons(SiteBuilder site)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(site.Output, ".bukit", "incremental-manifest.json")));
        return report.RootElement.GetProperty("renderReasons").EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
    }

    private static JsonObject ManifestEntries(SiteBuilder site) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(site.Cache, "build-manifest.json")))!["entries"]!.AsObject();

    private static string[] Reads(JsonObject entries, string key) =>
        entries[key]!["renderReads"]!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
}
