using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
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
    public async Task RootLayout_LegacyHomeRedirectIsNotIndexed()
    {
        using var site = RootSite();
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

        Assert.True(File.Exists(Path.Combine(site.Output, "en", "index.html")));
        var llms = File.ReadAllText(Path.Combine(site.Output, "llms.txt"));
        Assert.DoesNotContain("/en/", llms, StringComparison.Ordinal);
        var search = File.ReadAllText(Path.Combine(site.Output, "search.json"));
        Assert.DoesNotContain("/en/", search, StringComparison.Ordinal);
        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(site.Output, ".bukit", "variants", "en", "publish-audit-report.json")));
        var legacy = report.RootElement.GetProperty("documents").EnumerateArray()
            .Single(document => document.GetProperty("routeUrl").GetString() == "/en/");
        Assert.False(legacy.GetProperty("indexable").GetBoolean());
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
        Assert.Contains("content=\"0; url=https://example.test/\"", File.ReadAllText(legacy), StringComparison.Ordinal);
        Assert.Contains("rel=\"canonical\" href=\"https://example.test/\"", File.ReadAllText(legacy), StringComparison.Ordinal);
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
        Assert.Contains("content=\"0; url=https://example.test/repo/\"",
            File.ReadAllText(Path.Combine(site.Output, "en", "index.html")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "index", "en", "en/sitemap.xml")]
    [InlineData(false, "split", "en", "sitemap.xml")]
    [InlineData(false, "index", "zh-CN", "sitemap.xml")]
    [InlineData(true, "index", "en", "sitemap.xml")]
    public void SitemapRelativePath_MovesOnlyRootLanguageIndexSitemap(bool inSubdir, string mode, string language, string expected)
    {
        var site = new SiteConfig
        {
            Name = "layout",
            Title = "Layout",
            Languages = ["en", "zh-CN"],
            DefaultLanguage = "en",
            DefaultLanguageInSubdir = inSubdir,
            SitemapMode = mode
        };

        Assert.Equal(expected, BuildPathUtils.NormalizeRelPath(I18nOutputLayout.For(site).SitemapRelativePath(language)));
        Assert.Same(I18nOutputLayout.For(site), I18nOutputLayout.For(site));
        Assert.Throws<InvalidOperationException>(() => I18nOutputLayout.For(site).BaseUrl(language));
    }

    [Fact]
    public void CombineRelative_RejectsRootedPaths()
    {
        Assert.Equal(Path.Combine("zh-CN", "index.html"), I18nOutputLayout.CombineRelative("zh-CN", "index.html"));
        Assert.Equal("index.html", I18nOutputLayout.CombineRelative(string.Empty, "index.html"));
        Assert.Throws<ArgumentException>(() => I18nOutputLayout.CombineRelative("zh-CN", Path.GetFullPath("dist")));
    }

    [Fact]
    public void RootLayout_GeneratedOutputLinkPathsCoverRootLanguageTargets()
    {
        using var site = RootSite();

        var paths = BuildTransaction.GeneratedOutputLinkPaths(site.Config).Select(BuildPathUtils.NormalizeRelPath).ToArray();

        Assert.Contains(".bukit/variants/en", paths);
        Assert.Contains("en/index.html", paths);
        Assert.Contains("zh-CN/.bukit", paths);
        var disabled = site.Config with { Site = site.Config.Site with { DefaultLanguageRedirect = false } };
        Assert.DoesNotContain("en/index.html", BuildTransaction.GeneratedOutputLinkPaths(disabled).Select(BuildPathUtils.NormalizeRelPath));
    }

    [Theory]
    [InlineData("split", new[] { "https://example.test/repo/sitemap.xml", "https://example.test/repo/zh-CN/sitemap.xml" })]
    [InlineData("index", new[] { "https://example.test/repo/sitemap.xml", "https://example.test/repo/en/sitemap.xml" })]
    public async Task RootLayout_ProjectSiteRobotsAdvertisesTheWrittenSitemaps(string mode, string[] expected)
    {
        using var site = RootSite();
        var robotsConfig = WithRobots(site.Config);
        var config = robotsConfig with { Site = robotsConfig.Site with { BaseUrl = "/repo", SitemapMode = mode } };

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        var sitemapLines = File.ReadAllLines(Path.Combine(site.Output, "robots.txt"))
            .Where(line => line.StartsWith("Sitemap: ", StringComparison.Ordinal))
            .Select(line => line["Sitemap: ".Length..])
            .ToArray();
        Assert.Equal(expected, sitemapLines);
        if (mode == "index")
        {
            var index = File.ReadAllText(Path.Combine(site.Output, "sitemap.xml"));
            Assert.Contains("<loc>https://example.test/repo/en/sitemap.xml</loc>", index, StringComparison.Ordinal);
            Assert.Contains("<loc>https://example.test/repo/zh-CN/sitemap.xml</loc>", index, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(site.Output, "en", "sitemap.xml")));
        }
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
        else
        {
            Assert.Contains("Sitemap: https://example.test/en/sitemap.xml", robots, StringComparison.Ordinal);
            Assert.DoesNotContain("Sitemap: https://example.test/zh-CN/sitemap.xml", robots, StringComparison.Ordinal);
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
        // The regenerated file is owned again, so disabling robots later removes it.
        Assert.Contains("robots.txt", BuildManifest.Load(Path.Combine(site.Root, ".cache", "build-manifest.json")).OwnedOutputs);
    }

    [Fact]
    public async Task StaticRobots_FromNamedThemeOverridesGeneratedRobots()
    {
        using var site = RootSite();
        var themeRoot = site.Sandbox.PathFor("themes/local");
        CopyDirectory(site.Sandbox.PathFor("layouts"), Path.Combine(themeRoot, "layouts"));
        File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), "name: local\nversion: 1.0.0\nengine: bukit\n");
        site.Sandbox.Write("themes/local/static/robots.txt", "User-agent: *\nAllow: /theme\n");
        var config = WithRobots(site.Config) with { Theme = site.Config.Theme with { Name = "local" } };

        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });
        Assert.Equal("User-agent: *\nAllow: /theme\n", File.ReadAllText(Path.Combine(site.Output, "robots.txt")));

        // The second build sees robots.txt as a previously owned output; it must still be the static file.
        await engine.BuildAsync(config, site.Root, new ConfigOverrides());
        Assert.Equal("User-agent: *\nAllow: /theme\n", File.ReadAllText(Path.Combine(site.Output, "robots.txt")));
    }

    [Fact]
    public async Task StaticRobots_MiscasedNameConflictsInsteadOfReplacingGeneratedRobots()
    {
        using var site = RootSite();
        var config = WithRobots(site.Config);
        site.Sandbox.Write("static/Robots.txt", "User-agent: *\nAllow: /static\n");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRootOutputConflict, ex.Code);
        Assert.Contains("Robots.txt", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ConflictGuard_FollowsTheCopyStepDotfileRule(bool publishDotFiles, bool expectConflict)
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Build = site.Config.Build with { PublishDotFiles = publishDotFiles },
            Site = site.Config.Site with
            {
                Language = "en",
                Feed = site.Config.Site.Feed with { Mode = "merged", Path = ".feeds", Formats = ["atom"] }
            }
        };
        site.Sandbox.Write("static/.feeds/atom.xml", "<feed/>");

        var ex = Record.Exception(() =>
            I18nRootOutputConflictGuard.Validate(config, site.Sandbox.PathFor("static"), null));

        if (expectConflict)
        {
            Assert.Equal(DiagnosticCode.I18nRootOutputConflict, Assert.IsType<BukitException>(ex).Code);
        }
        else
        {
            Assert.Null(ex);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void AuditReadPaths_StaticRobotsFollowsTheCopyStepSymlinkRule(bool followSymlinks, bool expectStaticRobots)
    {
        using var site = RootSite();
        var config = site.Config with { Build = site.Config.Build with { FollowSymlinks = followSymlinks } };
        site.Sandbox.Write("static/policy/robots-source.txt", "User-agent: *\n");
        File.CreateSymbolicLink(site.Sandbox.PathFor("static/robots.txt"), "policy/robots-source.txt");
        var layout = I18nOutputLayout.Create(config.Site, ["en", "zh-CN"], "/");

        var paths = layout.AuditReadPaths(
            site.Output,
            "en",
            [site.Sandbox.PathFor("static")],
            I18nOutputLayout.StaticCopyOptions(config.Build));

        Assert.NotNull(paths);
        Assert.Equal(expectStaticRobots, paths.Robots is not null);
    }

    [Fact]
    public async Task SingleLanguageRootLayout_KeepsLanguageAndRootManifestsApart()
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Site = site.Config.Site with { Languages = ["en"], DefaultLanguage = "en" }
        };
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });

        var cache = Path.Combine(site.Root, ".cache");
        var languageManifest = BuildManifest.Load(Path.Combine(cache, "build-manifest.en.json"));
        var rootManifest = BuildManifest.Load(Path.Combine(cache, "build-manifest.json"));
        Assert.Contains("agent-manifest.json", rootManifest.OwnedOutputs);
        Assert.DoesNotContain("agent-manifest.json", languageManifest.OwnedOutputs);

        await engine.BuildAsync(config, site.Root, new ConfigOverrides());
        Assert.True(File.Exists(Path.Combine(site.Output, "agent-manifest.json")));
        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    [Fact]
    public async Task StaticSitemap_WithoutSiteUrlIsNotARootConflict()
    {
        using var site = RootSite();
        var config = site.Config with { Site = site.Config.Site with { Url = null, SitemapMode = "index" } };
        site.Sandbox.Write("static/sitemap.xml", "<urlset/>");

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.Equal("<urlset/>", File.ReadAllText(Path.Combine(site.Output, "sitemap.xml")));
    }

    [Theory]
    [InlineData("agent-manifest.json")]
    [InlineData("sitemap.xml")]
    [InlineData("rss.xml")]
    [InlineData("llms-full.txt")]
    public async Task StaticRootOwnedFiles_ConflictBeforeCopy(string fileName)
    {
        using var site = RootSite();
        var baseConfig = site.Config;
        var config = baseConfig with
        {
            Site = baseConfig.Site with
            {
                SitemapMode = "merged",
                Feed = baseConfig.Site.Feed with { Mode = "merged" },
                Seo = baseConfig.Site.Seo with
                {
                    Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true, LlmsFullTxt = true }
                }
            }
        };
        site.Sandbox.Write($"static/{fileName}", "static root output");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRootOutputConflict, ex.Code);
        Assert.Contains(fileName, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaticLlmsSymlinkOutsideSource_IsNotTreatedAsUserOutput(bool followSymlinks)
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Build = site.Config.Build with { FollowSymlinks = followSymlinks }
        };
        site.Sandbox.Write("outside-llms.txt", "outside");
        Directory.CreateDirectory(Path.Combine(site.Root, "static"));
        File.CreateSymbolicLink(
            Path.Combine(site.Root, "static", "llms.txt"),
            "../outside-llms.txt");

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "llms.txt")));
        Assert.DoesNotContain("outside", File.ReadAllText(Path.Combine(site.Output, "llms.txt")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaticSymlinkDirectoryOutsideSource_DoesNotSuppressLegacyRedirect(bool followSymlinks)
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Build = site.Config.Build with { FollowSymlinks = followSymlinks }
        };
        site.Sandbox.Write("outside/index.html", "outside");
        Directory.CreateDirectory(Path.Combine(site.Root, "static"));
        Directory.CreateSymbolicLink(Path.Combine(site.Root, "static", "en"), "../outside");

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        var html = File.ReadAllText(Path.Combine(site.Output, "en", "index.html"));
        Assert.Contains("http-equiv=\"refresh\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("outside", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaticSafeSymlink_ConflictsWhenFollowSymlinksIsEnabled()
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Build = site.Config.Build with { FollowSymlinks = true }
        };
        site.Sandbox.Write("static/target/llms.txt", "inside static");
        Directory.CreateDirectory(Path.Combine(site.Root, "static"));
        File.CreateSymbolicLink(
            Path.Combine(site.Root, "static", "llms.txt"),
            "target/llms.txt");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRootOutputConflict, ex.Code);
        Assert.Contains("llms.txt", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaticSafeSymlinkDirectory_SuppressesLegacyRedirectWhenFollowSymlinksIsEnabled()
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Build = site.Config.Build with { FollowSymlinks = true }
        };
        site.Sandbox.Write("static/target/en/index.html", "inside static");
        Directory.CreateDirectory(Path.Combine(site.Root, "static"));
        Directory.CreateSymbolicLink(
            Path.Combine(site.Root, "static", "en"),
            "target/en");

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        var html = File.ReadAllText(Path.Combine(site.Output, "en", "index.html"));
        Assert.Contains("inside static", html, StringComparison.Ordinal);
        Assert.DoesNotContain("http-equiv=\"refresh\"", html, StringComparison.Ordinal);
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
    public async Task StaticLlms_ConflictIgnoresFileNameCase()
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Site = site.Config.Site with
            {
                Seo = site.Config.Site.Seo with { Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true } }
            }
        };
        site.Sandbox.Write("static/LLMS.TXT", "static llms");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRootOutputConflict, ex.Code);
        Assert.Contains("LLMS.TXT", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("robots")]
    [InlineData("Robots")]
    [InlineData("AGENT-MANIFEST")]
    public void RootOwnedAggregate_MatchesKindIgnoringCase(string kind)
    {
        using var site = RootSite();

        Assert.True(PublicOutputLifecycle.IsRootOwnedAggregate(site.Config, kind, "en"));
        Assert.False(PublicOutputLifecycle.IsRootOwnedAggregate(site.Config, kind, "zh-CN"));
    }

    [Theory]
    [InlineData("Feed")]
    [InlineData("ATOM")]
    [InlineData("JsonFeed")]
    [InlineData("SiteMap")]
    public void MergedAggregate_MatchesKindIgnoringCase(string kind)
    {
        using var site = RootSite();
        var merged = site.Config with
        {
            Site = site.Config.Site with { SitemapMode = "merged", Feed = site.Config.Site.Feed with { Mode = "merged" } }
        };
        var split = site.Config with
        {
            Site = site.Config.Site with { SitemapMode = "split", Feed = site.Config.Site.Feed with { Mode = "split" } }
        };

        Assert.True(PublicOutputLifecycle.IsRootOwnedAggregate(merged, kind, "zh-CN"));
        Assert.False(PublicOutputLifecycle.IsRootOwnedAggregate(split, kind, "zh-CN"));
    }

    [Fact]
    public void GeneratedOutputs_MayNotReplaceAPage()
    {
        RouteInfo[] pages = [new("/en/", "en/index.html", "__redirect__"), new("/about/", "about/index.html", "page")];
        AssetOutputItem Generated(string source, string path, AssetOutputCategory category)
            => new(source, path, category, Operation: AssetOutputOperation.Generate);

        var plugin = Assert.Throws<InvalidOperationException>(() => VariantBuildPipeline.ValidateGeneratedOutputsDoNotReplacePages(
            pages, [Generated("taxonomy", "EN/index.html", AssetOutputCategory.Plugin)], StringComparer.OrdinalIgnoreCase));
        var projection = Assert.Throws<InvalidOperationException>(() => VariantBuildPipeline.ValidateGeneratedOutputsDoNotReplacePages(
            pages, [Generated("feed", "about/index.html", AssetOutputCategory.Projection)], StringComparer.OrdinalIgnoreCase));
        VariantBuildPipeline.ValidateGeneratedOutputsDoNotReplacePages(
            pages,
            [Generated("taxonomy", "taxonomy.json", AssetOutputCategory.Plugin), Generated("theme", "about/index.html", AssetOutputCategory.Static)],
            StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Bukit defect", plugin.Message, StringComparison.Ordinal);
        Assert.Contains("'/en/'", plugin.Message, StringComparison.Ordinal);
        Assert.StartsWith("Bukit defect: Projection 'feed'", projection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectionFeedPath_UnderAnotherLanguageFailsTheBuild()
    {
        using var site = RootSite();
        var config = WithPageFeedPath(site.Config, "zh-CN/news");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, ex.Code);
        Assert.Contains("zh-CN/news/rss.xml", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(site.Output, "zh-CN", "news", "rss.xml")));
    }

    [Fact]
    public async Task CollectionFeedPath_OntoARenderedPageFailsTheBuild()
    {
        using var site = RootSite();
        site.Sandbox.Write("content/en/news.md", """
            ---
            title: News
            collection: page
            language: en
            route:
              url: /news.xml
            ---
            News page.
            """);
        var config = WithPageFeedPath(site.Config, "news.xml");

        var ex = await Assert.ThrowsAsync<BukitException>(() =>
            new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
                new ConfigOverrides { Incremental = false }));

        // The feed is a planned projection and every rendered page (content, derived, list,
        // static HTML) is a render entry, so the asset plan rejects the collision before any
        // write; the internal assertion behind it is not reachable through this path.
        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, ex.Code);
        Assert.Contains("news.xml", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(site.Output, "news.xml")));
    }

    private static AppConfig WithPageFeedPath(AppConfig config, string feedPath)
        => config with
        {
            Site = config.Site with
            {
                Collections = config.Site.Collections!.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Key == "page"
                        ? pair.Value with { Output = pair.Value.Output with { Rss = true, FeedPath = feedPath } }
                        : pair.Value)
            }
        };

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
    public async Task LayoutSwitch_WithLostCacheManifestCleansThroughTheMarker()
    {
        using var site = new SiteBuilder("i18n-site");
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "index.html")));
        Directory.Delete(Path.Combine(site.Root, ".cache"), recursive: true);
        var rootConfig = RootConfig(site.Config) with
        {
            Site = RootConfig(site.Config).Site with { DefaultLanguageRedirect = false }
        };

        await engine.BuildAsync(rootConfig, site.Root, new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));
        Assert.False(Directory.Exists(Path.Combine(site.Output, "en")));
    }

    [Fact]
    public async Task LayoutSwitch_WithoutOutputMarkerRefusesToClean()
    {
        using var site = new SiteBuilder("i18n-site");
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });
        var englishBefore = File.ReadAllBytes(Path.Combine(site.Output, "en", "index.html"));
        File.Delete(Path.Combine(site.Output, ".bukit-output-marker"));

        var ex = await Assert.ThrowsAnyAsync<BukitException>(() => engine.BuildAsync(
            RootConfig(site.Config), site.Root, new ConfigOverrides { Incremental = false }));

        Assert.Equal(DiagnosticCode.BuildOutputNoMarker, ex.Code);
        Assert.Equal(englishBefore, File.ReadAllBytes(Path.Combine(site.Output, "en", "index.html")));
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
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(site.Output, "content"), "*.json", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(site.Output, "content", "en", "index.html.json")));
        Assert.False(File.Exists(Path.Combine(site.Output, "content", "en", "index.html.md")));
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
        Assert.Contains("content=\"0; url=https://example.test/\"", legacy, StringComparison.Ordinal);
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

    // ---- Migration redirects for default-language legacy URLs (site.defaultLanguageLegacyRedirects) ----

    private static AppConfig WithLegacyRedirects(AppConfig config, bool enabled = true)
        => config with { Site = config.Site with { DefaultLanguageLegacyRedirects = enabled } };

    private static bool IsRedirectPage(string path)
        => File.ReadAllText(path).Contains("http-equiv=\"refresh\"", StringComparison.Ordinal);

    private static SiteBuilder TaxonomyRootSite()
    {
        var site = new SiteBuilder("taxonomy-site");
        var path = site.Sandbox.PathFor("site.yaml");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("  language: en\n", "  language: en\n  languages:\n    - en\n    - zh-CN\n  defaultLanguage: en\n  defaultLanguageInSubdir: false\n", StringComparison.Ordinal));
        return site;
    }

    private static async Task<RecordingLogger> BuildWithLogger(AppConfig config, string root, bool incremental = false)
    {
        var logger = new RecordingLogger();
        await new SiteEngine(logger).BuildAsync(config, root, new ConfigOverrides { Incremental = incremental, Clean = false });
        return logger;
    }

    [Fact]
    public async Task LegacyRedirects_PrefixedLayoutWarnsAndDerivesNothing()
    {
        using var site = new SiteBuilder("i18n-site");
        site.Markdown("about", "About", "About body");

        var logger = await BuildWithLogger(WithLegacyRedirects(site.Config), site.Root);

        Assert.Contains(logger.Warnings, message => message.Contains("event=i18n.config.legacy_redirects_ignored", StringComparison.Ordinal));
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "en", "about", "index.html")));
        Assert.DoesNotContain(logger.Infos, message => message.Contains("event=i18n.legacy_redirects.derived", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LegacyRedirects_SingleLanguageWarnsEvenWithOtherSettingsAtDefaults()
    {
        using var site = new SiteBuilder();
        site.Markdown("about", "About", "About body");

        var logger = await BuildWithLogger(WithLegacyRedirects(site.Config), site.Root);

        Assert.Contains(logger.Warnings, message =>
            message.Contains("event=i18n.config.default_language_layout_ignored", StringComparison.Ordinal) &&
            message.Contains("defaultLanguageLegacyRedirects=true", StringComparison.Ordinal));
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "about", "index.html")));
    }

    [Fact]
    public async Task LegacyRedirects_CoverPagesDerivedListsAndStaticHtml()
    {
        using var site = TaxonomyRootSite();
        site.Sandbox.Write("static/legacy.html", "<p>Legacy static page</p>");
        var config = WithLegacyRedirects(site.Config) with
        {
            Theme = site.Config.Theme with { StaticTemplate = "pages/page.html" }
        };

        await BuildWithLogger(config, site.Root);

        var categories = Directory.EnumerateFiles(Path.Combine(site.Output, "categories"), "index.html", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(site.Output, path))
            .ToArray();
        Assert.NotEmpty(categories);
        Assert.Contains(categories, path => path.Contains("page", StringComparison.Ordinal));
        foreach (var relative in categories)
        {
            var legacy = Path.Combine(site.Output, "en", relative);
            Assert.True(File.Exists(legacy), relative);
            var target = "https://example.test/" + Path.GetDirectoryName(relative)!.Replace('\\', '/') + "/";
            Assert.Contains($"content=\"0; url={target}\"", File.ReadAllText(legacy), StringComparison.Ordinal);
        }

        // A static HTML route gets a directory URL: static/legacy.html is served at /legacy/.
        var staticLegacy = Path.Combine(site.Output, "en", "legacy", "index.html");
        Assert.True(IsRedirectPage(staticLegacy));
        Assert.Contains("url=https://example.test/legacy/", File.ReadAllText(staticLegacy), StringComparison.Ordinal);
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "legacy", "index.html")));
    }

    [Fact]
    public async Task LegacyRedirects_UseBaseUrlRelativeTargetsWithoutSiteUrl()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        var config = WithLegacyRedirects(site.Config) with
        {
            Site = site.Config.Site with { DefaultLanguageLegacyRedirects = true, BaseUrl = "/repo", Url = null }
        };

        await BuildWithLogger(config, site.Root);

        var legacy = File.ReadAllText(Path.Combine(site.Output, "en", "about", "index.html"));
        Assert.Contains("content=\"0; url=/repo/about/\"", legacy, StringComparison.Ordinal);
    }

    private static readonly string[] IndexFiles =
        ["agent-manifest.json", "search.json", "sitemap.xml", "llms.txt", "llms-full.txt", "rss.xml"];

    [Fact]
    public async Task LegacyRedirects_AreExcludedFromIndexesAndSkip404AndNonHtml()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        site.Sandbox.Write("content/not-found.md",
            "---\nslug: not-found\ntitle: Not found\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\nurl: /404.html\n---\nNot found\n");
        site.Sandbox.Write("static/file.txt", "plain");
        var config = WithLegacyRedirects(site.Config) with
        {
            Site = site.Config.Site with
            {
                DefaultLanguageLegacyRedirects = true,
                SearchIncludeDerived = true,
                Seo = site.Config.Site.Seo with { Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true, LlmsFullTxt = true } }
            }
        };

        await BuildWithLogger(config, site.Root);

        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "about", "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "404.html")));
        Assert.False(File.Exists(Path.Combine(site.Output, "en", "404.html")));
        Assert.False(File.Exists(Path.Combine(site.Output, "en", "file.txt")));
        foreach (var file in IndexFiles)
        {
            var path = Path.Combine(site.Output, file);
            Assert.True(File.Exists(path), file);
            Assert.DoesNotContain("/en/about/", File.ReadAllText(path), StringComparison.Ordinal);
        }

        Assert.DoesNotContain(Directory.EnumerateFiles(Path.Combine(site.Output, "content"), "*", SearchOption.AllDirectories),
            path => path.Replace('\\', '/').Contains("/content/en/about", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LegacyRedirects_UserContentWinsAndIsCountedInTheSummary()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        site.Markdown("contact", "Contact", "Contact body");
        site.Markdown("team", "Team", "Team body");
        var contactPath = site.Sandbox.PathFor("content/contact.md");
        File.WriteAllText(contactPath, File.ReadAllText(contactPath)
            .Replace("collection: page\n", "collection: page\naliases:\n  - /en/about/\n", StringComparison.Ordinal));
        site.Sandbox.Write("static/en/team/index.html", "<p>Kept team page</p>");
        site.Markdown("faq", "FAQ", "FAQ body");
        site.Sandbox.Write("content/faq-kept.md",
            "---\nslug: faq-kept\ntitle: Kept FAQ\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\nurl: /en/faq/\n---\nKept FAQ body\n");
        var config = WithLegacyRedirects(site.Config);
        config = config with
        {
            Site = config.Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(config.Site.Collections!)
                {
                    ["page"] = config.Site.Collections!["page"] with { ListRoute = "/en/contact/", ListTemplate = "pages/index.html" }
                }
            }
        };

        var logger = await BuildWithLogger(config, site.Root);

        var about = File.ReadAllText(Path.Combine(site.Output, "en", "about", "index.html"));
        Assert.Contains("/contact/", about, StringComparison.Ordinal);
        Assert.Equal("<p>Kept team page</p>", File.ReadAllText(Path.Combine(site.Output, "en", "team", "index.html")));
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "en", "faq", "index.html")));
        Assert.Contains("Kept FAQ body", File.ReadAllText(Path.Combine(site.Output, "en", "faq", "index.html")), StringComparison.Ordinal);
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "en", "contact", "index.html")));
        var summary = Assert.Single(logger.Infos, message => message.Contains("event=i18n.legacy_redirects.derived", StringComparison.Ordinal));
        Assert.Contains("skipped=4", summary, StringComparison.Ordinal);
        foreach (var path in new[] { "en/about/index.html", "en/team/index.html", "en/faq/index.html", "en/contact/index.html" })
        {
            Assert.Contains(path, summary, StringComparison.Ordinal);
        }

        var redirects = Directory.EnumerateFiles(Path.Combine(site.Output, "en"), "*.html", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(site.Output, path).Replace('\\', '/') != "en/index.html")
            .Count(IsRedirectPage);
        var count = int.Parse(summary.Split(' ').Single(part => part.StartsWith("count=", StringComparison.Ordinal))["count=".Length..]);
        Assert.True(count > 0, summary);
        // The alias redirect at en/about/ is user content, not a migration redirect.
        Assert.Equal(redirects - 1, count);
    }

    [Fact]
    public async Task LegacyRedirects_ListRouteOccupiesTheLegacyHomePath()
    {
        using var site = RootSite();
        var config = site.Config with
        {
            Site = site.Config.Site with
            {
                DefaultLanguageLegacyRedirects = true,
                Collections = new Dictionary<string, CollectionConfig>(site.Config.Site.Collections!)
                {
                    ["page"] = site.Config.Site.Collections!["page"] with { ListRoute = "/en/", ListTemplate = "pages/index.html" }
                }
            }
        };

        var logger = await BuildWithLogger(config, site.Root);

        Assert.False(IsRedirectPage(Path.Combine(site.Output, "en", "index.html")));
        Assert.Contains(logger.Infos, message => message.Contains("event=i18n.legacy_root_redirect.skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LegacyRedirects_IncrementalDisableRemovesOnlyRedirects()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        await BuildWithLogger(WithLegacyRedirects(site.Config), site.Root, incremental: true);
        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "about", "index.html")));
        var chineseBefore = File.ReadAllBytes(Path.Combine(site.Output, "zh-CN", "index.html"));

        var result = await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(
            WithLegacyRedirects(site.Config, false), site.Root, new ConfigOverrides { Incremental = true, Clean = false });

        Assert.False(File.Exists(Path.Combine(site.Output, "en", "about", "index.html")));
        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "about", "index.html")));
        Assert.Equal(chineseBefore, File.ReadAllBytes(Path.Combine(site.Output, "zh-CN", "index.html")));
        // Only the legacy home redirect renders again: it shares the redirect set's dependencies.
        Assert.Equal(1, result.Incremental.CacheMissCount);
    }

    [Fact]
    public async Task LegacyRedirects_IncrementalUserContentReplacesTheRedirect()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        site.Markdown("contact", "Contact", "Contact body");
        var config = WithLegacyRedirects(site.Config);
        await BuildWithLogger(config, site.Root, incremental: true);
        Assert.Contains("url=https://example.test/about/", File.ReadAllText(Path.Combine(site.Output, "en", "about", "index.html")), StringComparison.Ordinal);

        var contactPath = site.Sandbox.PathFor("content/contact.md");
        File.WriteAllText(contactPath, File.ReadAllText(contactPath)
            .Replace("collection: page\n", "collection: page\naliases:\n  - /en/about/\n", StringComparison.Ordinal));
        await BuildWithLogger(config, site.Root, incremental: true);

        var about = File.ReadAllText(Path.Combine(site.Output, "en", "about", "index.html"));
        Assert.Contains("/contact/", about, StringComparison.Ordinal);
        Assert.DoesNotContain("url=https://example.test/about/", about, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegacyRedirects_LayoutSwitchInBothDirections()
    {
        using var site = new SiteBuilder("i18n-site");
        site.Markdown("about", "About", "About body");
        var prefixed = WithLegacyRedirects(site.Config);
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(prefixed, site.Root, new ConfigOverrides { Incremental = false });
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "en", "about", "index.html")));

        await engine.BuildAsync(WithLegacyRedirects(RootConfig(site.Config)), site.Root, new ConfigOverrides { Incremental = false });
        Assert.Contains("url=https://example.test/about/", File.ReadAllText(Path.Combine(site.Output, "en", "about", "index.html")), StringComparison.Ordinal);
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "about", "index.html")));

        await engine.BuildAsync(prefixed, site.Root, new ConfigOverrides { Incremental = false });
        var about = File.ReadAllText(Path.Combine(site.Output, "en", "about", "index.html"));
        Assert.False(IsRedirectPage(Path.Combine(site.Output, "en", "about", "index.html")));
        Assert.Contains("lang=\"en\"", about, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(site.Output, "about", "index.html")));
    }

    [Fact]
    public async Task LegacyRedirects_RefuseToWriteThroughAnInternalSymlink()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        await BuildWithLogger(site.Config, site.Root);
        Directory.CreateDirectory(Path.Combine(site.Output, "zh-CN", "elsewhere"));
        Directory.CreateSymbolicLink(Path.Combine(site.Output, "en", "about"), Path.Combine("..", "zh-CN", "elsewhere"));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(
            WithLegacyRedirects(site.Config), site.Root, new ConfigOverrides { Incremental = false, Clean = false }));

        Assert.Contains("symbolic link", ex.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(site.Output, "zh-CN", "elsewhere")));
    }

    [Fact]
    public async Task LegacyRedirects_MigrateAliasesDirectlyAndSkipPrefixedAliases()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        var aboutPath = site.Sandbox.PathFor("content/about.md");
        File.WriteAllText(aboutPath, File.ReadAllText(aboutPath)
            .Replace("collection: page\n", "collection: page\naliases:\n  - /old/\n  - /en/legacy/\n", StringComparison.Ordinal));

        await BuildWithLogger(WithLegacyRedirects(site.Config), site.Root);

        var migrated = File.ReadAllText(Path.Combine(site.Output, "en", "old", "index.html"));
        Assert.Contains("content=\"0; url=https://example.test/about/\"", migrated, StringComparison.Ordinal);
        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "legacy", "index.html")));
        Assert.False(Directory.Exists(Path.Combine(site.Output, "en", "en")));
    }

    [Fact]
    public async Task LegacyRedirects_TaxonomyPagesGetExactlyOneRedirect()
    {
        using var site = TaxonomyRootSite();

        var logger = await BuildWithLogger(WithLegacyRedirects(site.Config), site.Root);

        var categoryIndex = Path.Combine(site.Output, "en", "categories", "index.html");
        Assert.True(IsRedirectPage(categoryIndex));
        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "categories", "tech", "index.html")));
        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(site.Output, ".bukit", "variants", "en", "publish-audit-report.json")));
        Assert.Single(report.RootElement.GetProperty("documents").EnumerateArray(),
            document => document.GetProperty("routeUrl").GetString() == "/en/categories/");
        Assert.Single(logger.Infos, message => message.Contains("event=i18n.legacy_redirects.derived", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LegacyRedirects_BuildTheOccupancySetOncePerDerive()
    {
        var (builds50, normalizations50) = await CountOccupancyWork(50);
        var (builds100, normalizations100) = await CountOccupancyWork(100);

        Assert.Equal(1, builds50);
        Assert.Equal(1, builds100);
        // Each extra page adds a constant number of normalizations (claim, lookup, redirect claim);
        // a quadratic scan would roughly quadruple the count when the page count doubles.
        var perPage = (normalizations100 - normalizations50) / 50.0;
        Assert.InRange(perPage, 1, 6);
        Assert.True(normalizations100 < normalizations50 * 2.5, $"{normalizations50} -> {normalizations100}");
    }

    private static async Task<(int Builds, int Normalizations)> CountOccupancyWork(int pages)
    {
        using var site = RootSite();
        for (var i = 0; i < pages; i++)
        {
            site.Markdown($"page-{i}", $"Page {i}", "Body");
        }

        var builds = new System.Runtime.CompilerServices.StrongBox<int>(0);
        var normalizations = new System.Runtime.CompilerServices.StrongBox<int>(0);
        I18nLegacyRedirectDeriver.OccupancyBuildsProbe.Value = builds;
        I18nLegacyRedirectDeriver.NormalizeCallsProbe.Value = normalizations;
        try
        {
            await BuildWithLogger(WithLegacyRedirects(site.Config), site.Root);
        }
        finally
        {
            I18nLegacyRedirectDeriver.OccupancyBuildsProbe.Value = null;
            I18nLegacyRedirectDeriver.NormalizeCallsProbe.Value = null;
        }

        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", $"page-{pages - 1}", "index.html")));
        return (builds.Value, normalizations.Value);
    }

    [Fact]
    public async Task LegacyRedirects_ReportsAndCountsGrowByTheRedirects()
    {
        using var off = RootSite();
        off.Markdown("about", "About", "About body");
        off.Markdown("contact", "Contact", "Contact body");
        var offResult = await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(
            off.Config, off.Root, new ConfigOverrides { Incremental = false, MetricsPath = "metrics.json" });

        using var on = RootSite();
        on.Markdown("about", "About", "About body");
        on.Markdown("contact", "Contact", "Contact body");
        var logger = new RecordingLogger();
        var onResult = await new SiteEngine(logger).BuildAsync(
            WithLegacyRedirects(on.Config), on.Root, new ConfigOverrides { Incremental = false, MetricsPath = "metrics.json" });

        var summary = Assert.Single(logger.Infos, message => message.Contains("event=i18n.legacy_redirects.derived", StringComparison.Ordinal));
        var count = int.Parse(summary.Split(' ').Single(part => part.StartsWith("count=", StringComparison.Ordinal))["count=".Length..]);
        Assert.True(count >= 2, summary);
        Assert.Equal(offResult.Summary.RouteCount + count, onResult.Summary.RouteCount);
        var offRoutes = File.ReadAllText(Path.Combine(off.Output, ".bukit", "routes.json"));
        var onRoutes = File.ReadAllText(Path.Combine(on.Output, ".bukit", "routes.json"));
        Assert.DoesNotContain("/en/about/", offRoutes, StringComparison.Ordinal);
        Assert.Contains("/en/about/", onRoutes, StringComparison.Ordinal);

        using var offReport = JsonDocument.Parse(File.ReadAllText(Path.Combine(off.Output, ".bukit", "build-report.json")));
        using var onReport = JsonDocument.Parse(File.ReadAllText(Path.Combine(on.Output, ".bukit", "build-report.json")));
        Assert.Equal(
            offReport.RootElement.GetProperty("summary").GetProperty("routeCount").GetInt32() + count,
            onReport.RootElement.GetProperty("summary").GetProperty("routeCount").GetInt32());
        static string[] Generated(JsonDocument report) => report.RootElement.GetProperty("generatedFiles").EnumerateArray()
            .Select(file => file.GetString()!.Replace('\\', '/')).ToArray();
        Assert.DoesNotContain(Generated(offReport), file => file.EndsWith("en/about/index.html", StringComparison.Ordinal));
        Assert.Contains(Generated(onReport), file => file.EndsWith("en/about/index.html", StringComparison.Ordinal));

        static int EnglishDerived(SiteBuilder site)
        {
            using var metrics = JsonDocument.Parse(File.ReadAllText(Path.Combine(site.Root, "metrics.json")));
            return metrics.RootElement.GetProperty("variants").EnumerateArray()
                .Single(variant => variant.GetProperty("language").GetString() == "en")
                .GetProperty("derived").GetInt32();
        }

        Assert.Equal(EnglishDerived(off) + count, EnglishDerived(on));
    }

    [Fact]
    public async Task LegacyRedirects_ToggleKeepsOrdinaryPagesUnrendered()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        site.Markdown("contact", "Contact", "Contact body");
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = true, Clean = false });
        var before = OrdinaryEntries(site);

        var logger = new RecordingLogger();
        var enabled = await new SiteEngine(logger).BuildAsync(
            WithLegacyRedirects(site.Config), site.Root, new ConfigOverrides { Incremental = true, Clean = false });
        var summary = Assert.Single(logger.Infos, message => message.Contains("event=i18n.legacy_redirects.derived", StringComparison.Ordinal));
        var count = int.Parse(summary.Split(' ').Single(part => part.StartsWith("count=", StringComparison.Ordinal))["count=".Length..]);
        // The new redirects render, plus the existing legacy home redirect; no ordinary page does.
        Assert.Equal(count + 1, enabled.Incremental.CacheMissCount);
        Assert.Equal(before, OrdinaryEntries(site));

        var disabled = await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = true, Clean = false });
        Assert.Equal(1, disabled.Incremental.CacheMissCount);
        Assert.Equal(before, OrdinaryEntries(site));
    }

    private static string OrdinaryEntries(SiteBuilder site)
    {
        var manifest = BuildManifest.Load(Path.Combine(site.Cache, "build-manifest.en.json"));
        return string.Join("\n", manifest.Entries
            .Where(entry => !entry.Value.OutputPath.StartsWith("en/", StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}|{entry.Value.ContentHash}|{entry.Value.RenderDependencyHash}|{entry.Value.TemplateHash}"));
    }

    [Fact]
    public async Task LegacyRedirects_HomeStaysWithDefaultLanguageRedirect()
    {
        using var site = RootSite();
        site.Markdown("about", "About", "About body");
        var config = site.Config with
        {
            Site = site.Config.Site with { DefaultLanguageRedirect = false, DefaultLanguageLegacyRedirects = true }
        };

        var logger = await BuildWithLogger(config, site.Root);

        Assert.False(File.Exists(Path.Combine(site.Output, "en", "index.html")));
        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "about", "index.html")));
        Assert.Contains(logger.Infos, message => message.Contains("event=i18n.legacy_redirects.home_skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LegacyRedirects_UseStableSafeSlugsForFileLikeAndNestedRoutes()
    {
        using var site = RootSite();
        site.Sandbox.Write("content/about.md",
            "---\nslug: about\ntitle: About\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\nurl: /about.html\n---\nAbout body\n");
        site.Sandbox.Write("content/post.md",
            "---\nslug: post\ntitle: Post\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\nurl: /blog/post/\n---\nPost body\n");
        var config = WithLegacyRedirects(site.Config);

        await BuildWithLogger(config, site.Root);
        var first = LegacyIds(site);
        await BuildWithLogger(config, site.Root);

        Assert.Equal(first, LegacyIds(site));
        Assert.Contains("/en/about.html", first.Keys);
        Assert.Contains("/en/blog/post/", first.Keys);
        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "about.html")));
        Assert.True(IsRedirectPage(Path.Combine(site.Output, "en", "blog", "post", "index.html")));
        Assert.All(first.Values, id => Assert.Matches("^i18n-legacy-en-[0-9a-f]{16}$", id));
        using var security = JsonDocument.Parse(File.ReadAllText(Path.Combine(site.Output, ".bukit", "security-report.json")));
        Assert.Equal("passed", security.RootElement.GetProperty("checks").GetProperty("unsafeSlug").GetProperty("status").GetString());
    }

    private static Dictionary<string, string> LegacyIds(SiteBuilder site)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(site.Output, ".bukit", "variants", "en", "publish-audit-report.json")));
        return report.RootElement.GetProperty("documents").EnumerateArray()
            .Where(document => document.TryGetProperty("sourceItemId", out var id) &&
                               id.ValueKind == JsonValueKind.String &&
                               id.GetString()!.StartsWith("i18n-legacy-en-", StringComparison.Ordinal))
            .ToDictionary(document => document.GetProperty("routeUrl").GetString()!, document => document.GetProperty("sourceItemId").GetString()!);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = [];
        public List<string> Warnings { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) { lock (Infos) Infos.Add(message); }
        public void Warn(string message) { lock (Warnings) Warnings.Add(message); }
        public void Error(string message) { }
    }
}
