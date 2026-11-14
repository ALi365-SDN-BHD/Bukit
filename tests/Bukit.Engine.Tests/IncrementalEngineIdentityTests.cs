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

    [Fact]
    public async Task DefaultLanguageLayout_OnlyContributesToHashWhenEnabledOnAMultilingualSite()
    {
        using var site = CreateSite();
        var config = Configure(site.Config, true);
        await BuildAsync(config, site.Root, "2.0.12+layout");

        // Writing the defaults explicitly (root layout, redirect on) leaves the hash unchanged.
        var explicitDefaults = config with
        {
            Site = config.Site with { DefaultLanguageInSubdir = false, DefaultLanguageRedirect = true }
        };
        var same = await BuildAsync(explicitDefaults, site.Root, "2.0.12+layout");
        Assert.Equal(0, same.Incremental.CacheMissCount);

        var subdirTrue = explicitDefaults with { Site = explicitDefaults.Site with { DefaultLanguageInSubdir = true } };
        var subdirChanged = await BuildAsync(subdirTrue, site.Root, "2.0.12+layout");
        Assert.Equal(0, subdirChanged.Incremental.CacheHitCount);
        Assert.True(subdirChanged.Incremental.CacheMissCount > 0);

        var redirectFalse = subdirTrue with { Site = subdirTrue.Site with { DefaultLanguageRedirect = false } };
        var redirectChanged = await BuildAsync(redirectFalse, site.Root, "2.0.12+layout");
        Assert.Equal(0, redirectChanged.Incremental.CacheHitCount);
        Assert.True(redirectChanged.Incremental.CacheMissCount > 0);
    }

    [Fact]
    public async Task DefaultLanguageLayout_DoesNotContributeToTheHashOfASingleLanguageSite()
    {
        using var site = CreateSite();
        var config = Configure(site.Config, false);
        await BuildAsync(config, site.Root, "2.0.12+layout");

        // The setting is ignored without at least two languages, so it must not invalidate the cache.
        var subdirTrue = config with { Site = config.Site with { DefaultLanguageInSubdir = true } };
        var same = await BuildAsync(subdirTrue, site.Root, "2.0.12+layout");
        Assert.Equal(0, same.Incremental.CacheMissCount);

        var single = config with { Site = config.Site with { Languages = ["en"] } };
        await BuildAsync(single, site.Root, "2.0.12+layout");
        var singleTrue = single with { Site = single.Site with { DefaultLanguageInSubdir = true } };
        var singleSame = await BuildAsync(singleTrue, site.Root, "2.0.12+layout");
        Assert.Equal(0, singleSame.Incremental.CacheMissCount);
    }

    [Fact]
    public async Task SiteLanguages_SwitchingBetweenOneAndMoreEntriesRebuildsEverything()
    {
        using var site = CreateSite();
        var legacy = Configure(site.Config, false);
        var first = await BuildAsync(legacy, site.Root, "2.0.12+languages");
        Assert.True(first.Incremental.CacheMissCount > 0);

        // site.languages is hashed as written, so writing the same one language still rebuilds once.
        var single = legacy with { Site = legacy.Site with { Languages = ["en"] } };
        var toSingle = await BuildAsync(single, site.Root, "2.0.12+languages");
        Assert.Equal(0, toSingle.Incremental.CacheHitCount);
        Assert.True(toSingle.Incremental.CacheMissCount > 0);

        var same = await BuildAsync(single, site.Root, "2.0.12+languages");
        Assert.Equal(0, same.Incremental.CacheMissCount);

        var otherSingle = single with { Site = single.Site with { Languages = ["fr"] } };
        var toOtherSingle = await BuildAsync(otherSingle, site.Root, "2.0.12+languages");
        Assert.Equal(0, toOtherSingle.Incremental.CacheHitCount);
        Assert.True(toOtherSingle.Incremental.CacheMissCount > 0);

        var multiple = Configure(site.Config, true);
        await BuildAsync(single, site.Root, "2.0.12+languages");
        var toMultiple = await BuildAsync(multiple, site.Root, "2.0.12+languages");
        Assert.Equal(0, toMultiple.Incremental.CacheHitCount);
        Assert.True(toMultiple.Incremental.CacheMissCount > 0);

        var backToSingle = await BuildAsync(single, site.Root, "2.0.12+languages");
        Assert.Equal(0, backToSingle.Incremental.CacheHitCount);
        Assert.True(backToSingle.Incremental.CacheMissCount > 0);
    }

    [Fact]
    public async Task LegacyRedirects_DoNotContributeToTheSiteHash()
    {
        using var site = CreateSite();
        var config = Configure(site.Config, true) with
        {
            Site = Configure(site.Config, true).Site with { DefaultLanguageInSubdir = false }
        };
        var first = await BuildAsync(config, site.Root, "2.0.12+legacy");

        // Every page of the first build stays cached, including the legacy home redirect, which
        // reads no shared collection; a site hash change would render them all.
        var enabled = config with { Site = config.Site with { DefaultLanguageLegacyRedirects = true } };
        var toggled = await BuildAsync(enabled, site.Root, "2.0.12+legacy");
        Assert.Equal(first.Incremental.CacheMissCount, toggled.Incremental.CacheHitCount);

        var same = await BuildAsync(enabled, site.Root, "2.0.12+legacy");
        Assert.Equal(0, same.Incremental.CacheMissCount);
        var disabled = await BuildAsync(config, site.Root, "2.0.12+legacy");
        Assert.Equal(0, disabled.Incremental.CacheMissCount);
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
