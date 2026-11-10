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

        var upgraded = await BuildAsync(config, site.Root, "2.0.12+bbb");
        Assert.Equal(0, upgraded.Incremental.CacheHitCount);
        Assert.Equal(first.Incremental.CacheMissCount, upgraded.Incremental.CacheMissCount);
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
        site.Markdown("hello", "Hello", "Body");
        site.Markdown("world", "World", "Body two");
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
                  kind: taxonomy-index
              taxonomyTerm:
                template: pages/taxonomy-term.html
                accepts:
                  kind: taxonomy-term
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
