using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nRootRedirectTests : IDisposable
{
    private readonly string _output = Path.Combine(Path.GetTempPath(), "bukit-root-redirect-" + Guid.NewGuid().ToString("N"));

    public I18nRootRedirectTests() => Directory.CreateDirectory(_output);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_output, recursive: true);
        }
        catch
        {
        }
    }

    private static AppConfig Config(string? url, string baseUrl, bool redirect = true) => new()
    {
        Site = new SiteConfig
        {
            Name = "redirect-test",
            Title = "Redirect test",
            Url = url,
            BaseUrl = baseUrl,
            Languages = ["en", "zh-CN"],
            DefaultLanguage = "en",
            DefaultLanguageInSubdir = true,
            DefaultLanguageRedirect = redirect
        },
        Content = TestContent.Markdown()
    };

    [Theory]
    [InlineData(null, "/", "/en/")]
    [InlineData(null, "/repo", "/repo/en/")]
    [InlineData("https://example.com", "/", "https://example.com/en/")]
    [InlineData("https://example.com", "/repo", "https://example.com/repo/en/")]
    public void Writer_UsesAbsoluteUrlWhenSiteUrlIsSet(string? siteUrl, string baseUrl, string expectedTarget)
    {
        I18nRootRedirectWriter.WriteIfRequested(Config(siteUrl, baseUrl), _output);

        var html = File.ReadAllText(Path.Combine(_output, "index.html"));
        Assert.Contains($"content=\"0; url={expectedTarget}\"", html, StringComparison.Ordinal);
        Assert.Contains($"<link rel=\"canonical\" href=\"{expectedTarget}\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Writer_DoesNothingWhenDisabled()
    {
        I18nRootRedirectWriter.WriteIfRequested(Config(null, "/", redirect: false), _output);

        Assert.False(File.Exists(Path.Combine(_output, "index.html")));
    }

    [Fact]
    public async Task Build_AddsRootRedirectAndExcludesItFromIndexes()
    {
        // Spec scenario "Root redirect for a prefixed site": the i18n-site fixture sets defaultLanguageInSubdir: true.
        using var site = new SiteBuilder("i18n-site");
        var config = site.Config with
        {
            Site = site.Config.Site with { Url = "https://example.com" }
        };

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Incremental = false });

        var redirectPath = Path.Combine(site.Output, "index.html");
        Assert.True(File.Exists(redirectPath));
        var html = File.ReadAllText(redirectPath);
        Assert.Contains("content=\"0; url=https://example.com/en/\"", html, StringComparison.Ordinal);
        Assert.Contains("noindex", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/index.html", File.ReadAllText(Path.Combine(site.Output, "sitemap.xml")));
        Assert.DoesNotContain("\"url\":\"/\"", File.ReadAllText(Path.Combine(site.Output, "search.json")));
        Assert.DoesNotContain("\"/index.html\"", File.ReadAllText(Path.Combine(site.Output, "agent-manifest.json")));

        var buildReport = File.ReadAllText(Path.Combine(site.Output, ".bukit", "build-report.json"));
        Assert.DoesNotContain("\"kind\":\"redirect\"", buildReport, StringComparison.OrdinalIgnoreCase);
        var publishAudit = File.ReadAllText(Path.Combine(site.Output, ".bukit", "publish-audit-report.json"));
        Assert.DoesNotContain("\"outputPath\": \"index.html\"", publishAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"kind\": \"redirect\"", publishAudit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Build_DefaultLayout_WritesTheLegacyHomeRedirectInsteadOfARootRedirect()
    {
        // Spec scenario "Legacy home redirect for a root default language": neither defaultLanguageInSubdir
        // nor defaultLanguageRedirect is set, so the default language builds at the root.
        using var site = new SiteBuilder("i18n-site");
        var configPath = site.Sandbox.PathFor("site.yaml");
        File.WriteAllText(configPath, File.ReadAllText(configPath)
            .Replace("  defaultLanguageInSubdir: true\n", string.Empty, StringComparison.Ordinal));
        Assert.False(site.Config.Site.DefaultLanguageInSubdir);

        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(site.Config, site.Root,
            new ConfigOverrides { Incremental = false });

        var home = File.ReadAllText(Path.Combine(site.Output, "index.html"));
        Assert.DoesNotContain("http-equiv=\"refresh\"", home, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lang=\"en\"", home, StringComparison.Ordinal);
        var legacy = File.ReadAllText(Path.Combine(site.Output, "en", "index.html"));
        Assert.Contains("content=\"0; url=https://example.test/\"", legacy, StringComparison.Ordinal);
        Assert.Contains("noindex", legacy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpgradeWithoutOptingIn_ChangesOnlyTheRootRedirect()
    {
        using var legacy = new SiteBuilder("i18n-site");
        var legacyConfig = legacy.Config with
        {
            Site = legacy.Config.Site with { DefaultLanguageRedirect = false }
        };
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(legacyConfig, legacy.Root,
            new ConfigOverrides { Incremental = false });
        var before = PublicFiles(legacy.Output);

        using var current = new SiteBuilder("i18n-site");
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(current.Config, current.Root,
            new ConfigOverrides { Incremental = false });
        var after = PublicFiles(current.Output);

        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Where(path => path != "index.html").Order(StringComparer.Ordinal));
        foreach (var (path, bytes) in before)
        {
            Assert.Equal(bytes, after[path]);
        }
    }

    private static Dictionary<string, byte[]> PublicFiles(string outputDir)
        => Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(outputDir, path).Split(Path.DirectorySeparatorChar)
                .Contains(".bukit", StringComparer.Ordinal))
            .Where(path => !string.Equals(Path.GetFileName(path), ".bukit-output-marker", StringComparison.Ordinal))
            .Where(path => !string.Equals(Path.GetFileName(path), ".bukit-build-state.json", StringComparison.Ordinal))
            .ToDictionary(
                path => Path.GetRelativePath(outputDir, path),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    [Fact]
    public async Task Build_RemovesRootRedirectWhenDisabled()
    {
        using var site = new SiteBuilder("i18n-site");
        var engine = new SiteEngine(new ConsoleLogger(LogLevel.Error));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });
        Assert.True(File.Exists(Path.Combine(site.Output, "index.html")));

        var configPath = site.Sandbox.PathFor("site.yaml");
        File.WriteAllText(configPath, File.ReadAllText(configPath)
            .Replace("  defaultLanguage: en\n", "  defaultLanguage: en\n  defaultLanguageRedirect: false\n", StringComparison.Ordinal));
        await engine.BuildAsync(site.Config, site.Root, new ConfigOverrides { Incremental = false });

        Assert.False(File.Exists(Path.Combine(site.Output, "index.html")));
        Assert.True(File.Exists(Path.Combine(site.Output, "en", "index.html")));
    }
}
