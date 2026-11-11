using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

// harden-incremental-invalidation: a different engine build re-renders every page.
public sealed class IncrementalEngineIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EngineIdentity_SameSkipsEverything_ChangedRerendersEverything(bool multilingual)
    {
        using var site = CreateSite();
        var config = Configure(site.Config, multilingual);

        var first = await BuildAsync(config, site.Root, "2.0.12+aaa");
        Assert.True(first.Incremental.CacheMissCount > 0);

        var same = await BuildAsync(config, site.Root, "2.0.12+aaa");
        Assert.Equal(0, same.Incremental.CacheMissCount);
        Assert.Equal(first.Incremental.CacheMissCount, same.Incremental.CacheHitCount);

        Assert.True(ListReasons(site, "list_unchanged") > 0);
        Assert.Equal(0, ListReasons(site, "list_render"));

        var upgraded = await BuildAsync(config, site.Root, "2.0.12+bbb");
        Assert.Equal(0, upgraded.Incremental.CacheHitCount);
        Assert.Equal(first.Incremental.CacheMissCount, upgraded.Incremental.CacheMissCount);
        // Special lists (taxonomy and pagination pages of the tagged content) render again too.
        Assert.True(ListReasons(site, "list_render") > 0);
        Assert.Equal(0, ListReasons(site, "list_unchanged"));
    }

    private static int ListReasons(SiteBuilder site, string reason)
    {
        var total = 0;
        foreach (var path in Directory.EnumerateFiles(site.Output, "incremental-manifest.json", SearchOption.AllDirectories))
        {
            using var report = JsonDocument.Parse(File.ReadAllText(path));
            if (report.RootElement.GetProperty("renderReasons").TryGetProperty(reason, out var count))
            {
                total += count.GetInt32();
            }
        }

        return total;
    }

    private static async Task<BuildResult> BuildAsync(AppConfig config, string root, string engineIdentity)
    {
        VariantManifestPlanner.EngineIdentityOverride.Value = engineIdentity;
        try
        {
            return await new SiteEngine(new NullLogger()).BuildAsync(config, root,
                new ConfigOverrides { Clean = false, Incremental = true });
        }
        finally
        {
            VariantManifestPlanner.EngineIdentityOverride.Value = null;
        }
    }

    private static SiteBuilder CreateSite()
    {
        var site = new SiteBuilder();
        site.Sandbox.Write("content/hello.md", "---\nslug: hello\ntitle: Hello\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\ntags: [news]\n---\nBody\n");
        site.Sandbox.Write("content/world.md", "---\nslug: world\ntitle: World\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\ntags: [news]\n---\nBody two\n");
        site.Sandbox.Write("layouts/theme.yaml", """
            name: engine-identity-test
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
        foreach (var name in new[] { "list", "taxonomy-index", "taxonomy-term", "pagination", "search" })
            site.Sandbox.Write($"layouts/pages/{name}.html", "<html><head><title>Test</title></head><body>Test</body></html>");
        return site;
    }

    private static AppConfig Configure(AppConfig config, bool multilingual) => config with
    {
        Site = config.Site with
        {
            Languages = multilingual ? ["en", "zh"] : null,
            DefaultLanguage = multilingual ? "en" : null
        },
        Build = config.Build with { LanguageJobs = multilingual ? 2 : 1, Clean = false }
    };

    private sealed class NullLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
