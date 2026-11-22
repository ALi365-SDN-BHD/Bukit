using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

// An incremental build that skips unchanged pages and lists logs the warnings their last render logged.
public sealed class IncrementalRenderWarningReplayTests
{
    private const string ThemeHeadJsonLd = """<script type="application/ld+json">{"@type":"ProbeThing"}</script>""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncrementalBuild_ReportsTheSameRenderWarningsAsTheFullBuild(bool multilingual)
    {
        using var site = CreateSite();
        var config = Configure(site.Config, multilingual);

        // The first build has no manifest yet, so it renders every page and list.
        var full = await BuildAsync(config, site.Root);
        var incremental = await BuildAsync(config, site.Root);

        Assert.Equal(0, full.Result.Incremental.CacheHitCount);
        Assert.True(incremental.Result.Incremental.CacheHitCount > 0);
        Assert.Equal(0, incremental.Result.Incremental.CacheMissCount);
        var replaced = full.Warnings.Where(w => w.Contains("seo.theme_json_ld_replaced", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(replaced);
        Assert.Equal(full.Warnings.Count, incremental.Warnings.Count);
        // The build log shows five warnings of a kind and collapses the rest into a summary line,
        // and pages render in parallel, so compare kinds rather than which routes were shown.
        Assert.Equal(Kinds(full.Warnings), Kinds(incremental.Warnings));
    }

    [Fact]
    public async Task IncrementalBuild_RendersAgainInsteadOfReplayingWhenDiagnosticsAreTurnedOff()
    {
        using var site = CreateSite();
        var config = Configure(site.Config, multilingual: false);
        await BuildAsync(config, site.Root);

        var off = config with { Site = config.Site with { Seo = config.Site.Seo with { Diagnostics = "off" } } };
        var silenced = await BuildAsync(off, site.Root);

        Assert.Equal(0, silenced.Result.Incremental.CacheHitCount);
        // Render-time SEO checks are off; the publish audit (seo.audit) still runs and is not checked here.
        Assert.DoesNotContain(silenced.Warnings, w => w.Contains("seo.theme_json_ld_replaced", StringComparison.Ordinal));
    }

    private static string[] Kinds(IEnumerable<string> warnings)
        => warnings.Select(w => w.Split(' ', 2)[0]).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task IncrementalBuild_DropsTheWarningOnceTheRerenderedPageNoLongerCausesIt()
    {
        using var site = CreateSite();
        var config = Configure(site.Config, multilingual: false);
        var first = await BuildAsync(config, site.Root);
        Assert.Contains(first.Warnings, w => w.Contains("seo.theme_json_ld_replaced", StringComparison.Ordinal));

        foreach (var name in TemplateNames)
            site.Sandbox.Write($"layouts/pages/{name}.html", Page(string.Empty));
        var fixedBuild = await BuildAsync(config, site.Root);
        var skipped = await BuildAsync(config, site.Root);

        Assert.DoesNotContain(fixedBuild.Warnings, w => w.Contains("seo.theme_json_ld_replaced", StringComparison.Ordinal));
        Assert.Equal(0, skipped.Result.Incremental.CacheMissCount);
        Assert.DoesNotContain(skipped.Warnings, w => w.Contains("seo.theme_json_ld_replaced", StringComparison.Ordinal));
    }

    private static readonly string[] TemplateNames = ["page", "list", "taxonomy-index", "taxonomy-term", "pagination", "search"];

    private static string Page(string head)
        => $"<html><head><title>{{{{ page.title }}}}</title>{head}</head><body>{{{{ page.title }}}}{{{{ page.content }}}}</body></html>";

    private static async Task<(BuildResult Result, List<string> Warnings)> BuildAsync(AppConfig config, string root)
    {
        var logger = new RecordingLogger();
        var result = await new SiteEngine(logger).BuildAsync(config, root,
            new ConfigOverrides { Clean = false, Incremental = true });
        return (result, logger.Warnings);
    }

    private static SiteBuilder CreateSite()
    {
        var site = new SiteBuilder();
        site.Sandbox.Write("content/hello.md", "---\nslug: hello\ntitle: Hello\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\ntags: [news]\n---\nBody\n");
        site.Sandbox.Write("content/world.md", "---\nslug: world\ntitle: World\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\ntags: [news]\n---\nBody two\n");
        site.Sandbox.Write("layouts/theme.yaml", """
            name: warning-replay-test
            version: 1.0.0
            engine: bukit
            templates:
              home:
                template: pages/index.html
                required: true
              page:
                template: pages/page.html
                accepts:
                  type: page
                  collection: page
              list:
                template: pages/list.html
                accepts:
                  kind: list
              taxonomyIndex:
                template: pages/taxonomy-index.html
                accepts:
                  kind: taxonomy_index
              taxonomyTerm:
                template: pages/taxonomy-term.html
                accepts:
                  kind: taxonomy_term
              pagination:
                template: pages/pagination.html
                accepts:
                  kind: pagination
              search:
                template: pages/search.html
                accepts:
                  kind: search
            """);
        foreach (var name in TemplateNames)
            site.Sandbox.Write($"layouts/pages/{name}.html", Page(ThemeHeadJsonLd));
        return site;
    }

    private static AppConfig Configure(AppConfig config, bool multilingual) => config with
    {
        Site = config.Site with
        {
            Url = "https://example.com",
            Seo = config.Site.Seo with { Enabled = true, RenderMode = "inject", Diagnostics = "warn" },
            Languages = multilingual ? ["en", "zh"] : null,
            DefaultLanguage = multilingual ? "en" : null
        },
        Build = config.Build with { LanguageJobs = multilingual ? 2 : 1, Clean = false }
    };

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { lock (Warnings) Warnings.Add(message); }
        public void Error(string message) { }
    }
}
