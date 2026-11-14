using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nSharedAssetsTests
{
    [Fact]
    public async Task SharingDisabled_KeepsPerLanguageAssetsAndTokens()
    {
        using var site = CreateSite();
        WriteSite(site, enabled: false, clean: true);
        await site.BuildAsync(clean: true);

        Assert.True(File.Exists(Path.Combine(site.Output, "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "assets", "style.css")));
        var chinesePage = ReadFirstHtml(site.Output, "zh-CN");
        Assert.Contains("href=\"/zh-CN/assets/style.css\"", chinesePage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharingEnabled_LeavesOneSharedAssetCopyAndUsesRootAssetBaseUrl()
    {
        using var site = CreateSite();
        WriteSite(site, enabled: true, clean: true);
        await site.BuildAsync(clean: true);

        Assert.True(File.Exists(Path.Combine(site.Output, "assets", "style.css")));
        Assert.False(File.Exists(Path.Combine(site.Output, "zh-CN", "assets", "style.css")));
        Assert.True(File.Exists(Path.Combine(site.Output, "assets", "logo.svg")));
        Assert.False(File.Exists(Path.Combine(site.Output, "zh-CN", "assets", "logo.svg")));
        Assert.True(File.Exists(Path.Combine(site.Output, "fonts", "a.woff2")));
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "fonts", "a.woff2")));

        var chinesePage = ReadFirstHtml(site.Output, "zh-CN");
        Assert.Contains("href=\"/assets/style.css\"", chinesePage, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/zh-CN/assets/style.css\"", chinesePage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultLanguageFailure_DoesNotStartOtherLanguages()
    {
        using var site = CreateSite();
        WriteSite(site, enabled: true, clean: true);
        site.Sandbox.Write("content/en/reserved.md", """
            ---
            title: Reserved
            collection: page
            language: en
            route:
              url: /zh-CN/reserved/
            ---
            Reserved page.
            """);
        var logger = new RecordingLogger();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            new Bukit.Engine.SiteEngine(logger).BuildAsync(
                site.Config,
                site.Root,
                new ConfigOverrides { Incremental = false }));

        Assert.DoesNotContain(
            logger.Infos,
            message => message.Contains("event=build.variant.start language=zh-CN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfiguredStaticPrefix_SharesThatPrefixToo()
    {
        using var site = CreateSite();
        WriteSite(site, enabled: true, clean: true, paths: "[assets/, fonts/]");
        await site.BuildAsync(clean: true);

        Assert.True(File.Exists(Path.Combine(site.Output, "fonts", "a.woff2")));
        Assert.False(File.Exists(Path.Combine(site.Output, "zh-CN", "fonts", "a.woff2")));
    }

    [Fact]
    public async Task SharingEnabledWithRepoBaseUrl_UsesRootAssetBaseUrl()
    {
        using var site = CreateSite();
        WriteSite(site, enabled: true, clean: true, baseUrl: "/repo");
        await site.BuildAsync(clean: true);

        var chinesePage = ReadFirstHtml(site.Output, "zh-CN");
        Assert.Contains("href=\"/repo/assets/style.css\"", chinesePage, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/repo/zh-CN/assets/style.css\"", chinesePage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncrementalToggle_RemovesAndRestoresNonDefaultCopies()
    {
        using var site = CreateSite();
        WriteSite(site, enabled: false, clean: true);
        await site.BuildAsync(clean: true);
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "assets", "style.css")));

        WriteSite(site, enabled: true, clean: false);
        await site.BuildAsync(clean: false, incremental: true);
        Assert.True(File.Exists(Path.Combine(site.Output, "assets", "style.css")));
        Assert.False(File.Exists(Path.Combine(site.Output, "zh-CN", "assets", "style.css")));
        Assert.Contains("href=\"/assets/style.css\"", ReadFirstHtml(site.Output, "zh-CN"), StringComparison.Ordinal);

        WriteSite(site, enabled: false, clean: false);
        await site.BuildAsync(clean: false, incremental: true);
        Assert.True(File.Exists(Path.Combine(site.Output, "zh-CN", "assets", "style.css")));
        Assert.Contains("href=\"/zh-CN/assets/style.css\"", ReadFirstHtml(site.Output, "zh-CN"), StringComparison.Ordinal);
    }

    private static SiteBuilder CreateSite()
    {
        var site = new SiteBuilder("i18n-site");
        site.Sandbox.Write("assets/style.css", "body {}");
        site.Sandbox.Write("static/assets/logo.svg", "<svg/>");
        site.Sandbox.Write("static/fonts/a.woff2", "font");
        site.Sandbox.Write("layouts/layouts/base.html", """
            {{ base_url = site.base_url }}
            {{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
            {{ asset_base_url = site.asset_base_url }}
            {{ if asset_base_url == "/" }}{{ asset_base_url = "" }}{{ end }}
            <!DOCTYPE html>
            <html lang="{{ site.language }}"><head><link rel="stylesheet" href="{{ asset_base_url }}/assets/style.css" /></head>
            <body>{{ content }}</body></html>
            """);
        site.Sandbox.Write(".site-base.yaml", File.ReadAllText(Path.Combine(site.Root, "site.yaml")));
        return site;
    }

    private static void WriteSite(SiteBuilder site, bool enabled, bool clean, string paths = "", string baseUrl = "/")
    {
        var path = Path.Combine(site.Root, "site.yaml");
        var yaml = File.ReadAllText(Path.Combine(site.Root, ".site-base.yaml"))
            .Replace("defaultLanguageInSubdir: true", "defaultLanguageInSubdir: false", StringComparison.Ordinal)
            .Replace("clean: false", $"clean: {clean.ToString().ToLowerInvariant()}", StringComparison.Ordinal)
            .Replace("  defaultLanguage: en", $$"""
                  defaultLanguage: en
                  baseUrl: "{{baseUrl}}"
                  sharedAssets:
                    enabled: {{enabled.ToString().ToLowerInvariant()}}
                {{(string.IsNullOrEmpty(paths) ? string.Empty : $"    paths: {paths}")}}
                """, StringComparison.Ordinal);
        File.WriteAllText(path, yaml);
    }

    private static string ReadFirstHtml(string output, string language)
        => File.ReadAllText(Directory.GetFiles(
            Path.Combine(output, language),
            "*.html",
            SearchOption.AllDirectories).First());

    private sealed class RecordingLogger : ILogger
    {
        internal List<string> Infos { get; } = [];

        public void Debug(string message)
        {
        }

        public void Info(string message) => Infos.Add(message);

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }
    }
}
