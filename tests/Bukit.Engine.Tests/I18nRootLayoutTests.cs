using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nRootLayoutTests
{
    private static SiteBuilder RootSite(string extraSite = "")
    {
        var site = new SiteBuilder("i18n-site");
        var path = site.Sandbox.PathFor("site.yaml");
        var yaml = File.ReadAllText(path)
            .Replace("  defaultLanguage: en\n", "  defaultLanguage: en\n  defaultLanguageInSubdir: false\n", StringComparison.Ordinal)
            .Replace("  search:\n    mode: merged\n", "  search:\n    mode: split\n", StringComparison.Ordinal)
            .Replace("  sitemapMode: merged\n", "  sitemapMode: split\n", StringComparison.Ordinal);
        if (extraSite.Length > 0)
        {
            yaml = yaml.Replace("  sitemapMode: split\n", "  sitemapMode: split\n" + extraSite, StringComparison.Ordinal);
        }

        File.WriteAllText(path, yaml);
        return site;
    }

    [Fact]
    public async Task RootLayout_WritesEnglishAtRootAndKeepsOtherLanguages()
    {
        using var site = RootSite();

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));
        Assert.Contains("lang=\"en\"", File.ReadAllText(Path.Combine(site.Output, "index.html")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "index.html")));
        Assert.Contains("lang=\"zh-CN\"", File.ReadAllText(Path.Combine(site.Output, "zh-CN", "index.html")), StringComparison.Ordinal);
        var legacy = Path.Combine(site.Output, "en", "index.html");
        Assert.True(File.Exists(legacy));
        Assert.Contains("content=\"0; url=/\"", File.ReadAllText(legacy), StringComparison.Ordinal);
        Assert.Contains("noindex", File.ReadAllText(legacy), StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(site.Output, ".bukit", "seo-report.json")));
        foreach (var report in new[] { "seo-report.json", "publish-audit-report.json", "seo-route-map.json", "geo-report.json", "analytics-report.json" })
        {
            Assert.True(File.Exists(Path.Combine(site.Output, ".bukit", "variants", "en", report)), report);
        }

        var artifactManifest = File.ReadAllText(Path.Combine(site.Output, ".bukit", "artifact-manifest.json"));
        Assert.Contains(".bukit/variants/en/analytics-report.json", artifactManifest, StringComparison.Ordinal);

        var mergedSeo = File.ReadAllText(Path.Combine(site.Output, ".bukit", "seo-report.json"));
        var englishSeo = File.ReadAllText(Path.Combine(site.Output, ".bukit", "variants", "en", "seo-report.json"));
        Assert.DoesNotContain("seo.output_file_missing", mergedSeo, StringComparison.Ordinal);
        Assert.DoesNotContain("seo.output_file_missing", englishSeo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootLayout_ProjectSiteBaseUrlKeepsPrefix()
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Site = site.Config.Site with { BaseUrl = "/repo" }
        };

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));
        Assert.Contains("\"url\":\"/repo/\"", File.ReadAllText(Path.Combine(site.Output, "search.json")), StringComparison.Ordinal);
        Assert.Contains("\"url\":\"/repo/zh-CN/\"", File.ReadAllText(Path.Combine(site.Output, "zh-CN", "search.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootLayout_MetricsReportsRootOutputDirForDefaultLanguage()
    {
        using var site = RootSite();
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false, MetricsPath = "metrics.json" });

        using var metrics = JsonDocument.Parse(File.ReadAllText(site.Sandbox.PathFor("metrics.json")));
        var english = metrics.RootElement.GetProperty("variants").EnumerateArray()
            .Single(variant => variant.GetProperty("language").GetString() == "en");
        Assert.Equal(Path.GetFullPath(site.Output), english.GetProperty("outputDir").GetString());
    }

    private static AppConfig RootConfig(AppConfig config)
        => config with
        {
            Site = config.Site with
            {
                DefaultLanguageInSubdir = false,
                SitemapMode = "split",
                Search = config.Site.Search with { Mode = "split" }
            }
        };

    private static AppConfig WithRobots(AppConfig config)
        => config with
        {
            Site = config.Site with
            {
                Seo = config.Site.Seo with
                {
                    RobotsTxt = config.Site.Seo.RobotsTxt with { Enabled = true }
                }
            }
        };

    [Fact]
    public async Task RootLayout_OtherLanguagesKeepTheirOwnAggregateFiles()
    {
        using var site = new SiteBuilder("i18n-site");
        var rootConfig = WithRobots(RootConfig(site.Config));
        var config = rootConfig with
        {
            Site = rootConfig.Site with
            {
                Seo = rootConfig.Site.Seo with
                {
                    Geo = rootConfig.Site.Seo.Geo with { Enabled = true, LlmsTxt = true, LlmsFullTxt = true }
                }
            }
        };

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        foreach (var file in new[] { "agent-manifest.json", "robots.txt", "llms.txt", "llms-full.txt" })
        {
            Assert.True(File.Exists(Path.Combine(site.Output, file)), "root " + file);
            Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", file)), "zh-CN " + file);
            Assert.False(File.Exists(Path.Combine(site.Output, "en", file)), "en " + file);
        }
    }

    [Theory]
    [InlineData("split")]
    [InlineData("index")]
    public async Task RootLayout_RobotsReferencesActualSitemaps(string sitemapMode)
    {
        using var site = RootSite();
        var config = WithRobots(site.Config) with
        {
            Site = WithRobots(site.Config).Site with { SitemapMode = sitemapMode }
        };
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        var robots = File.ReadAllText(Path.Combine(site.Output, "robots.txt"));
        Assert.Contains("Sitemap: https://example.test/sitemap.xml", robots, StringComparison.Ordinal);
        if (sitemapMode == "split")
        {
            Assert.Contains("Sitemap: https://example.test/zh-CN/sitemap.xml", robots, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RootLayout_SplitFeedAndSearchStayWithTheirLanguages()
    {
        using var site = RootSite();
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "rss.xml")));
        Assert.True(File.Exists(Path.Combine(site.Output, "search.json")));
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "rss.xml")));
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "search.json")));
    }

    [Fact]
    public async Task RootLayout_IndexSearchKeepsDefaultLanguageSearchAtRoot()
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Site = site.Config.Site with
            {
                Search = site.Config.Site.Search with { Mode = "index" }
            }
        };
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "search.json")));
        Assert.True(File.Exists(Path.Combine(site.Output, "search.index.json")));
        Assert.Contains("zh-CN", File.ReadAllText(Path.Combine(site.Output, "search.index.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaticRobots_OverridesGeneratedRobotsInBothDirections()
    {
        using var site = RootSite();
        var config = WithRobots(site.Config);
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });
        Assert.StartsWith("User-agent:", File.ReadAllText(Path.Combine(site.Output, "robots.txt")), StringComparison.Ordinal);

        site.Sandbox.Write("static/robots.txt", "User-agent: *\nAllow: /static\n");
        await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });
        Assert.Equal("User-agent: *\nAllow: /static\n", File.ReadAllText(Path.Combine(site.Output, "robots.txt")));

        File.Delete(site.Sandbox.PathFor("static/robots.txt"));
        await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });
        var generated = File.ReadAllText(Path.Combine(site.Output, "robots.txt"));
        Assert.Contains("Sitemap: https://example.test/sitemap.xml", generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaticLlms_ConflictsBeforeCopy()
    {
        using var site = RootSite();
        var enabled = WithRobots(site.Config);
        var config = enabled with
        {
            Site = enabled.Site with
            {
                Seo = enabled.Site.Seo with
                {
                    Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true }
                }
            }
        };
        site.Sandbox.Write("static/llms.txt", "static llms");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRootOutputConflict, ex.Code);
        Assert.Contains("static/llms.txt", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RootLayout_ReportDirectoriesAreExcludedFromPublicInventories()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-root-inventories-" + Guid.NewGuid().ToString("N"));
        var rootOutput = Path.Combine(root, "root");
        var prefixedOutput = Path.Combine(root, "prefixed");
        try
        {
            Directory.CreateDirectory(Path.Combine(rootOutput, ".bukit", "variants", "en"));
            Directory.CreateDirectory(Path.Combine(prefixedOutput, "en", ".bukit"));
            File.WriteAllText(Path.Combine(rootOutput, ".bukit", "variants", "en", "analytics-report.json"), "{\"secret\":true}");
            File.WriteAllText(Path.Combine(prefixedOutput, "en", ".bukit", "analytics-report.json"), "{\"secret\":true}");
            File.WriteAllText(Path.Combine(rootOutput, "index.html"), "root");
            File.WriteAllText(Path.Combine(prefixedOutput, "en", "index.html"), "prefixed");

            Assert.DoesNotContain(AssetsReportInventory.Create(rootOutput),
                entry => entry.Path.Contains(".bukit", StringComparison.Ordinal));
            Assert.Contains(AssetsReportInventory.Create(prefixedOutput),
                entry => entry.Path == "en/.bukit/analytics-report.json");
            Assert.DoesNotContain(ReleaseBundleInventory.Create(Path.Combine(rootOutput, ".bukit"), rootOutput),
                entry => entry.Path.Contains(".bukit", StringComparison.Ordinal));
            Assert.Contains(ReleaseBundleInventory.Create(Path.Combine(prefixedOutput, ".bukit"), prefixedOutput),
                entry => entry.Path == "en/.bukit/analytics-report.json");

            var databaseId = "0123456789abcdef0123456789abcdef";
            var content = TestContent.Markdown() with
            {
                Sources =
                [
                    new ContentSourceConfig
                    {
                        Type = "notion",
                        Notion = new NotionConfig { DatabaseId = databaseId }
                    }
                ]
            };
            var config = new AppConfig { Site = ConfigForAlternates().Site, Content = content };
            var privacy = PublicOutputPrivacyCheck.Evaluate(config, rootOutput, []);
            Assert.Equal("passed", privacy.Status);
            Assert.Empty(privacy.Errors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ShrunkPlanFromOldManifest_DoesNotFullRebuildOrDeleteUserFiles()
    {
        using var site = RootSite();
        var baseConfig = site.Config;
        var config = baseConfig with
        {
            Site = baseConfig.Site with
            {
                SitemapMode = "merged",
                Feed = baseConfig.Site.Feed with { Mode = "merged", Formats = ["rss", "atom"] }
            }
        };
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = true, Clean = false });

        var manifestPath = Path.Combine(site.Cache, "build-manifest.json");
        var manifest = BuildManifest.Load(manifestPath);
        manifest.OwnedOutputs.Add("en/sitemap.xml");
        manifest.OwnedOutputs.Add("en/feeds/atom.xml");
        manifest.Save(manifestPath);
        var languageManifestPath = Path.Combine(site.Cache, "build-manifest.en.json");
        var languageManifest = BuildManifest.Load(languageManifestPath);
        Assert.NotEmpty(languageManifest.Entries);
        languageManifest.OwnedOutputs.Add("en/sitemap.xml");
        languageManifest.OwnedOutputs.Add("en/feeds/atom.xml");
        languageManifest.Save(languageManifestPath);
        site.Sandbox.Write("dist/en/custom.txt", "user file");

        var result = await engine.BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = true, Clean = false });

        Assert.True(result.Incremental.CacheHitCount > 0,
            $"hits={result.Incremental.CacheHitCount} misses={result.Incremental.CacheMissCount}");
        Assert.Equal(0, result.Incremental.CacheMissCount);
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "custom.txt")));
        Assert.False(File.Exists(Path.Combine(site.Output, "en", "sitemap.xml")));
    }

    [Fact]
    public async Task LayoutSwitchFailure_LeavesPreviousOutputIntact()
    {
        using var site = new SiteBuilder("i18n-site");
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });
        var englishBefore = File.ReadAllBytes(Path.Combine(site.Output, "en", "index.html"));
        var redirectBefore = File.ReadAllBytes(Path.Combine(site.Output, "index.html"));
        site.Sandbox.Write("content/en/broken.md", "---\ntitle: Broken\ncollection: page\nlanguage: en\ngeo:\n  llms:\n    priority: 999\n---\nBroken\n");
        var rootConfig = RootConfig(site.Config) with
        {
            Site = RootConfig(site.Config).Site with
            {
                Seo = site.Config.Site.Seo with { Diagnostics = "strict" }
            }
        };

        await Assert.ThrowsAnyAsync<Exception>(() => engine.BuildAsync(
            rootConfig, site.Root, new ConfigOverrides { Incremental = false }));

        Assert.Equal(englishBefore, File.ReadAllBytes(Path.Combine(site.Output, "en", "index.html")));
        Assert.Equal(redirectBefore, File.ReadAllBytes(Path.Combine(site.Output, "index.html")));
        Assert.False(Directory.Exists(Path.Combine(site.Output, ".bukit", "variants", "en")));
    }

    [Fact]
    public async Task LegacyRedirect_IsSkippedWhenUserContentOwnsThePath()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        var contentPath = site.Sandbox.PathFor("content/about.md");
        File.WriteAllText(contentPath, File.ReadAllText(contentPath)
            .Replace("collection: page\n", "collection: page\naliases:\n  - /en/\n", StringComparison.Ordinal));

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });

        var html = File.ReadAllText(Path.Combine(site.Output, "en", "index.html"));
        Assert.Contains("/about/", html, StringComparison.Ordinal);
        Assert.DoesNotContain("content=\"0; url=/\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegacyRedirect_IsExcludedFromIndexesAndReports()
    {
        using var site = RootSite();
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.DoesNotContain("\"/en/\"", File.ReadAllText(Path.Combine(site.Output, "agent-manifest.json")), StringComparison.Ordinal);
        Assert.DoesNotContain("\"url\":\"/en/\"", File.ReadAllText(Path.Combine(site.Output, "search.json")), StringComparison.Ordinal);
        Assert.DoesNotContain("<loc>https://example.test/en/</loc>", File.ReadAllText(Path.Combine(site.Output, "sitemap.xml")), StringComparison.Ordinal);
        foreach (var file in new[] { "llms.txt", "llms-full.txt", "rss.xml" })
        {
            var path = Path.Combine(site.Output, file);
            if (File.Exists(path))
            {
                Assert.DoesNotContain("/en/", File.ReadAllText(path), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task RedirectsDisabled_WriteNeitherRedirect()
    {
        using var prefixed = new SiteBuilder("i18n-site");
        var prefixedConfig = prefixed.Config with
        {
            Site = prefixed.Config.Site with { DefaultLanguageRedirect = false }
        };
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(prefixedConfig, prefixed.Root,
            new ConfigOverrides { Incremental = false });
        Assert.False(File.Exists(Path.Combine(prefixed.Output, "index.html")));

        using var root = RootSite();
        var rootConfig = root.Config with
        {
            Site = root.Config.Site with { DefaultLanguageRedirect = false }
        };
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(rootConfig, root.Root,
            new ConfigOverrides { Incremental = false });
        Assert.False(File.Exists(Path.Combine(root.Output, "en", "index.html")));
    }

    [Fact]
    public async Task SwitchingLayout_CleansThePreviousLayout()
    {
        using var site = new SiteBuilder("i18n-site");
        var prefixed = site.Config;
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(prefixed, site.Root, new ConfigOverrides { Incremental = false });
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "index.html")));

        var rootConfig = RootConfig(prefixed);
        await engine.BuildAsync(rootConfig, site.Root, new ConfigOverrides { Incremental = false });
        var legacy = File.ReadAllText(Path.Combine(site.Output, "en", "index.html"));
        Assert.Contains("content=\"0; url=/\"", legacy, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(site.Output, "en", "zh-CN", "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));
        Assert.Contains("lang=\"en\"", File.ReadAllText(Path.Combine(site.Output, "index.html")), StringComparison.Ordinal);

        await engine.BuildAsync(prefixed, site.Root, new ConfigOverrides { Incremental = false });
        Assert.Contains("lang=\"en\"", File.ReadAllText(Path.Combine(site.Output, "en", "index.html")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));
        Assert.Contains("/en/", File.ReadAllText(Path.Combine(site.Output, "index.html")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncrementalRemoval_DoesNotTouchAnotherLanguage()
    {
        using var site = RootSite();
        site.Sandbox.Write("content/en/guides.md", "---\ntitle: Guides\ncollection: page\nlanguage: en\npublishAt: 2024-06-01T00:00:00Z\n---\nGuides\n");
        site.Sandbox.Write("content/zh-CN/guides.md", "---\ntitle: Chinese Guides\ncollection: page\nlanguage: zh-CN\npublishAt: 2024-06-01T00:00:00Z\n---\nChinese guides\n");
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = true, Clean = false });
        Assert.True(File.Exists(Path.Combine(site.Output, "guides", "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "guides", "index.html")));

        File.Delete(site.Sandbox.PathFor("content/en/guides.md"));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = true, Clean = false });

        Assert.False(File.Exists(Path.Combine(site.Output, "guides", "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "guides", "index.html")));
    }

    [Fact]
    public async Task DisablingRedirect_RemovesTheLegacyPage()
    {
        using var site = RootSite();
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "index.html")));

        var disabled = site.Config with
        {
            Site = site.Config.Site with { DefaultLanguageRedirect = false }
        };
        await engine.BuildAsync(disabled, site.Root, new ConfigOverrides { Incremental = false });

        Assert.False(File.Exists(Path.Combine(site.Output, "en", "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));
    }

    [Fact]
    public async Task RootLayout_CopiesStatic404ToRoot()
    {
        using var site = RootSite();
        site.Sandbox.Write("static/404.html", "<html><body>not found</body></html>");

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "404.html")));
        Assert.Contains("not found", File.ReadAllText(Path.Combine(site.Output, "404.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void SeoAlternates_RootDefaultUsesUnprefixedUrlsAndXDefault()
    {
        var config = ConfigForAlternates();
        var documents = new[]
        {
            AlternateDocument("about-en", "en"),
            AlternateDocument("about-zh", "zh-CN")
        };

        var alternates = SeoAlternatesService.BuildSeoAlternates(config, documents, ["en", "zh-CN"], "en", "/");

        var about = alternates["i18n:about"];
        Assert.Contains(about, item => item.Hreflang == "en" && item.Href == "https://example.test/about/");
        Assert.Contains(about, item => item.Hreflang == "x-default" && item.Href == "https://example.test/about/");
        Assert.Contains(about, item => item.Hreflang == "zh-CN" && item.Href == "https://example.test/zh-CN/about/");
    }

    private static AppConfig ConfigForAlternates()
    {
        var site = new SiteConfig
        {
            Name = "alternates",
            Title = "Alternates",
            Url = "https://example.test",
            Languages = ["en", "zh-CN"],
            DefaultLanguage = "en",
            DefaultLanguageInSubdir = false,
            Collections = new Dictionary<string, CollectionConfig>
            {
                ["page"] = new() { Permalink = "/{slug}/", Template = "pages/page.html" }
            }
        };
        return new AppConfig { Site = site, Content = TestContent.Markdown() };
    }

    private static ContentDocument AlternateDocument(string id, string language)
        => ContentDocument.Create(
            id,
            "About",
            "about",
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            "<p>About</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["language"] = language,
                ["i18nKey"] = "about",
                ["type"] = "page",
                ["collection"] = "page"
            }));

    [Fact]
    public async Task RootLayout_LanguageAuditDoesNotReadPreviousRootAggregates()
    {
        using var site = RootSite();
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });
        var clean = ReadVariantAudit(site);

        File.WriteAllText(Path.Combine(site.Output, "sitemap.xml"),
            "<?xml version=\"1.0\"?><sitemapindex><sitemap><loc>https://example.test/ghost.xml</loc></sitemap></sitemapindex>");
        File.WriteAllText(Path.Combine(site.Output, "search.json"), "{\"ghost\":true}");
        File.WriteAllText(Path.Combine(site.Output, "agent-manifest.json"), "{\"documents\":[{\"route\":\"/ghost/\"}]}");
        File.WriteAllText(Path.Combine(site.Output, "robots.txt"), "User-agent: *\nDisallow: /\n");
        File.WriteAllText(Path.Combine(site.Output, "llms.txt"), "ghost");
        File.WriteAllText(Path.Combine(site.Output, "llms-full.txt"), "ghost");

        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });
        var dirty = ReadVariantAudit(site);

        Assert.Equal(clean, dirty);
    }

    private static string ReadVariantAudit(SiteBuilder site)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(site.Output, ".bukit", "variants", "en", "seo-report.json")));
        var root = report.RootElement;
        return root.GetProperty("routes").GetRawText() + "\n" + root.GetProperty("issues").GetRawText();
    }

    [Fact]
    public async Task RootLayout_IndexSitemapUsesLanguagePathAndUnprefixedLocations()
    {
        using var site = new SiteBuilder("i18n-site");
        var path = site.Sandbox.PathFor("site.yaml");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("  defaultLanguage: en\n", "  defaultLanguage: en\n  defaultLanguageInSubdir: false\n", StringComparison.Ordinal)
            .Replace("  sitemapMode: merged\n", "  sitemapMode: index\n", StringComparison.Ordinal)
            .Replace("  search:\n    mode: merged\n", "  search:\n    mode: split\n", StringComparison.Ordinal));

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });

        var index = File.ReadAllText(Path.Combine(site.Output, "sitemap.xml"));
        Assert.Contains("https://example.test/en/sitemap.xml", index, StringComparison.Ordinal);
        Assert.Contains("https://example.test/zh-CN/sitemap.xml", index, StringComparison.Ordinal);
        var sitemapFiles = Directory.GetFiles(site.Output, "sitemap.xml", SearchOption.AllDirectories);
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "sitemap.xml")), string.Join(",", sitemapFiles));
        var english = File.ReadAllText(Path.Combine(site.Output, "en", "sitemap.xml"));
        Assert.Contains("<loc>https://example.test/</loc>", english, StringComparison.Ordinal);
        Assert.DoesNotContain("<loc>https://example.test/en/</loc>", english, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "sitemap.xml")));
        Assert.False(File.Exists(Path.Combine(site.Output, "zh-CN", "zh-CN", "sitemap.xml")), string.Join(",", sitemapFiles));
    }
}
