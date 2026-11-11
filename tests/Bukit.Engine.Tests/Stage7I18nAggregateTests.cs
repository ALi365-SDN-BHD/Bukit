using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class Stage7I18nAggregateTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task RootAggregates_ContainBothLanguages_AndReportsMatchOutput()
    {
        using var site = new SiteBuilder("i18n-site");
        var yaml = File.ReadAllText(site.Sandbox.PathFor("site.yaml"));
        Assert.Contains("  sitemapMode: merged", yaml, StringComparison.Ordinal);
        Assert.Contains("      template: pages/page.html", yaml, StringComparison.Ordinal);
        yaml = yaml.Replace("  sitemapMode: merged", """
              sitemapMode: merged
              feed:
                mode: merged
                formats: [rss, atom, json]
              seo:
                robotsTxt:
                  enabled: true
                geo:
                  enabled: true
                  llmsTxt: true
                  llmsFullTxt: true
            """, StringComparison.Ordinal);
        yaml = yaml.Replace("      template: pages/page.html", """
                  template: pages/page.html
                  output:
                    rss: true
            """, StringComparison.Ordinal);
        site.Sandbox.Write("site.yaml", yaml);
        var started = DateTimeOffset.UtcNow;
        var result = await site.BuildAsync();
        var ended = DateTimeOffset.UtcNow;

        string FileAt(string path) => Path.Combine(site.Output, path);
        Assert.True(File.Exists(FileAt("rss.xml")), "Missing root RSS feed.");
        string[] relative = ["/en/index/", "/zh-CN/index/"];
        var absolute = relative.Select(path => "https://example.test" + path).ToArray();
        string[] sitemapExpected = ["https://example.test/en/", absolute[0], "https://example.test/zh-CN/", absolute[1]];
        var sitemap = XDocument.Load(FileAt("sitemap.xml"));
        Assert.Equal(sitemapExpected, sitemap.Descendants().Where(node => node.Name.LocalName == "url")
            .Select(node => node.Elements().Single(element => element.Name.LocalName == "loc").Value).Order(StringComparer.Ordinal));

        var rss = XDocument.Load(FileAt("rss.xml"));
        Assert.Equal(absolute, rss.Descendants().Where(node => node.Name.LocalName == "item")
            .Select(node => node.Elements().Single(element => element.Name.LocalName == "link").Value).Order(StringComparer.Ordinal));
        var atom = XDocument.Load(FileAt("feed/atom.xml"));
        Assert.Equal(absolute, atom.Descendants().Where(node => node.Name.LocalName == "entry")
            .Select(node => node.Elements().Single(element => element.Name.LocalName == "link" &&
                element.Attribute("rel")?.Value is null or "alternate").Attribute("href")!.Value).Order(StringComparer.Ordinal));
        using var feed = JsonDocument.Parse(File.ReadAllText(FileAt("feed/feed.json")));
        Assert.Equal(absolute, feed.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("url").GetString()).Order(StringComparer.Ordinal));

        foreach (var path in new[] { "llms.txt", "llms-full.txt" })
            foreach (var url in absolute) Assert.Contains(url, File.ReadAllText(FileAt(path)), StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://example.test/sitemap.xml", File.ReadAllText(FileAt("robots.txt")), StringComparison.Ordinal);

        using var search = JsonDocument.Parse(File.ReadAllText(FileAt("search.json")));
        Assert.Equal(new[] { "/en/", relative[0], "/zh-CN/", relative[1] }, search.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("url").GetString()).Order(StringComparer.Ordinal));
        using var manifest = JsonDocument.Parse(File.ReadAllText(FileAt("agent-manifest.json")));
        var documents = manifest.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(relative, documents.Select(item => item.GetProperty("route").GetString()).Order(StringComparer.Ordinal));
        foreach (var (route, language) in new[] { (relative[0], "en"), (relative[1], "zh-CN") })
            Assert.Equal(language, Assert.Single(documents, item => item.GetProperty("route").GetString() == route).GetProperty("language").GetString());
        ReportAssertions.Generated(site, result, started, ended);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task Translations_SharingASlug_PassStrictSchemaAndLogEachAuditIssueOnce()
    {
        using var site = new SiteBuilder("i18n-site");
        var yaml = File.ReadAllText(site.Sandbox.PathFor("site.yaml"));
        Assert.Contains("  clean: false", yaml, StringComparison.Ordinal);
        site.Sandbox.Write("site.yaml", yaml.Replace("  clean: false", "  clean: false\n  schemaFailMode: strict", StringComparison.Ordinal));
        var logger = new RecordingLogger();

        await site.BuildAsync(logger: logger);

        // content/en/index.md and content/zh/index.md share the id "pages:index".
        Assert.DoesNotContain(logger.Warnings, line => line.Contains("canonical_duplicate_id", StringComparison.Ordinal));
        var auditLines = logger.Warnings
            .Where(line => line.StartsWith("seo.audit ", StringComparison.Ordinal) ||
                           line.StartsWith("publish.audit ", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(auditLines);
        Assert.Equal(auditLines.Length, auditLines.Distinct(StringComparer.Ordinal).Count());
        Assert.All(auditLines, line => Assert.Matches(@" route=(-|/(en|zh-CN)(/|\s))", line));
    }

    private sealed class RecordingLogger : ILogger
    {
        internal ConcurrentQueue<string> Warnings { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) => Warnings.Enqueue(message);
    }
}
