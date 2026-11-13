using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

/// <summary>
/// Ownership of a single-language site's aggregate outputs: without <c>site.languages</c>
/// every sitemap and feed mode writes them in the variant, so the variant owns them.
/// </summary>
public sealed class SingleLanguageAggregateOwnershipTests
{
    [Fact]
    public async Task SingleLanguageMergedModes_OwnTheirSitemapAndFeed()
    {
        using var site = new SiteBuilder("basic-markdown-site");
        var config = site.Config with
        {
            Site = site.Config.Site with { SitemapMode = "merged", Feed = site.Config.Site.Feed with { Mode = "merged" } }
        };
        Assert.False(PublicOutputLifecycle.IsRootOwnedAggregate(config, "sitemap", config.Site.Language));
        Assert.False(PublicOutputLifecycle.IsRootOwnedAggregate(config, "feed", config.Site.Language));
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));

        await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "sitemap.xml")));
        Assert.True(File.Exists(Path.Combine(site.Output, "rss.xml")));
        var owned = BuildManifest.Load(Path.Combine(site.Root, ".cache", "build-manifest.json")).OwnedOutputs;
        Assert.Contains("sitemap.xml", owned);
        Assert.Contains("rss.xml", owned);

        // Owned outputs are removed once the feature that wrote them is off.
        await engine.BuildAsync(config with { Site = config.Site with { Url = null } }, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.False(File.Exists(Path.Combine(site.Output, "sitemap.xml")));
        Assert.False(File.Exists(Path.Combine(site.Output, "rss.xml")));
    }

    [Fact]
    public async Task AliasRedirect_IsNotIndexedOrListedInLlms()
    {
        using var site = new SiteBuilder("basic-markdown-site");
        var contentPath = site.Sandbox.PathFor("content/index.md");
        File.WriteAllText(contentPath, File.ReadAllText(contentPath)
            .Replace("collection: page\n", "collection: page\naliases:\n  - /legacy/\n", StringComparison.Ordinal));
        var config = site.Config with
        {
            Site = site.Config.Site with
            {
                SearchIncludeDerived = true,
                Seo = site.Config.Site.Seo with { Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true } }
            }
        };

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "legacy", "index.html")));
        Assert.DoesNotContain("/legacy/", File.ReadAllText(Path.Combine(site.Output, "llms.txt")), StringComparison.Ordinal);
        Assert.DoesNotContain("/legacy/", File.ReadAllText(Path.Combine(site.Output, "search.json")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merged")]
    [InlineData("index")]
    public async Task SingleLanguageSitemap_StaticFileCollidesInEveryMode(string mode)
    {
        using var site = new SiteBuilder("basic-markdown-site");
        site.Sandbox.Write("static/sitemap.xml", "<urlset/>");
        var config = site.Config with { Site = site.Config.Site with { SitemapMode = mode } };

        var ex = await Assert.ThrowsAnyAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, ex.Code);
    }
}
