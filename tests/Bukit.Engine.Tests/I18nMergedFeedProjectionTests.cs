using Bukit.Config;
using Bukit.Engine.Plugins;
using Bukit.Shared;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nMergedFeedProjectionTests
{
    private sealed class TestLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) => Errors.Add(message);
    }

    [Fact]
    public async Task BuildAsync_MergedI18nFeeds_GeneratesRootRssAtomAndJsonFeed()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-i18n-merged-feeds-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: merged-feeds
                  title: Merged Feeds
                  url: https://example.com
                  baseUrl: /
                  language: en
                  languages: [en, zh]
                  defaultLanguage: en
                  rssMode: merged
                  feed:
                    formats: [rss, atom, json]
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
                      output:
                        rss: true
                content:
                  provider: markdown
                  markdown:
                    dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);
            WritePost(root, "hello-en.md", "Hello EN", "hello", "en");
            WritePost(root, "hello-zh.md", "Hello ZH", "hello", "zh");
            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!doctype html>
                <html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), """
                {% layout "layouts/base.html" %}
                {{ page.content }}
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), """
                {% layout "layouts/base.html" %}
                {{ page.content }}
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), """
                {% layout "layouts/base.html" %}
                Index
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), """
                {% layout "layouts/base.html" %}
                List
                """);

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            var engine = new SiteEngine(logger);
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var rss = File.ReadAllText(Path.Combine(root, "dist", "rss.xml"));
            var atom = File.ReadAllText(Path.Combine(root, "dist", "feed", "atom.xml"));
            var json = File.ReadAllText(Path.Combine(root, "dist", "feed", "feed.json"));
            var manifest = File.ReadAllText(Path.Combine(root, "dist", "agent-manifest.json"));
            var publishAudit = File.ReadAllText(Path.Combine(root, "dist", ".bukit", "publish-audit-report.json"));
            Assert.Contains("https://example.com/en/blog/hello/", rss, StringComparison.Ordinal);
            Assert.Contains("https://example.com/zh/blog/hello/", rss, StringComparison.Ordinal);
            Assert.Contains("https://example.com/en/blog/hello/", atom, StringComparison.Ordinal);
            Assert.Contains("https://example.com/zh/blog/hello/", atom, StringComparison.Ordinal);
            Assert.Contains("https://example.com/en/blog/hello/", json, StringComparison.Ordinal);
            Assert.Contains("https://example.com/zh/blog/hello/", json, StringComparison.Ordinal);
            Assert.Contains("/en/blog/hello/", manifest, StringComparison.Ordinal);
            Assert.Contains("/zh/blog/hello/", manifest, StringComparison.Ordinal);
            Assert.DoesNotContain("publish.manifest_missing_route", publishAudit, StringComparison.Ordinal);

            using var auditDoc = JsonDocument.Parse(publishAudit);
            var documents = auditDoc.RootElement.GetProperty("documents").EnumerateArray().ToArray();
            Assert.Contains(documents, x =>
                x.GetProperty("routeUrl").GetString() == "/en/blog/hello/" &&
                x.GetProperty("manifestIncluded").GetBoolean());
            Assert.Contains(documents, x =>
                x.GetProperty("routeUrl").GetString() == "/zh/blog/hello/" &&
                x.GetProperty("manifestIncluded").GetBoolean());
            Assert.Empty(logger.Errors);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void BuiltInPluginSource_DoesNotDoubleOwnProjectionAggregateOutputs()
    {
        var plugins = new BuiltInPluginSource().GetPlugins().Select(x => x.GetType().Name).ToArray();

        Assert.DoesNotContain("FeedPlugin", plugins);
        Assert.DoesNotContain("SitemapPlugin", plugins);
        Assert.DoesNotContain("SearchIndexPlugin", plugins);
        Assert.DoesNotContain("LlmsTxtPlugin", plugins);
    }

    [Fact]
    public void RootAggregateProjectionAdapters_ImplementProjectionContract()
    {
        foreach (var projection in PublishRepresentationRegistry.RootAggregateProjectionAdapters())
        {
            Assert.IsAssignableFrom<IPublishProjection>(projection);
        }
    }

    [Fact]
    public async Task BuildAsync_MergedI18nGeo_GeneratesRootLlmsAndRobotsThroughProjectionInventory()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-i18n-root-geo-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: root-geo
                  title: Root Geo
                  url: https://example.com
                  baseUrl: /
                  language: en
                  languages: [en, zh]
                  defaultLanguage: en
                  seo:
                    robotsTxt:
                      enabled: true
                    geo:
                      enabled: true
                      llmsTxt: true
                      llmsFullTxt: true
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
                      output:
                        rss: true
                content:
                  provider: markdown
                  markdown:
                    dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);
            WritePost(root, "hello-en.md", "Hello EN", "hello", "en");
            WritePost(root, "hello-zh.md", "Hello ZH", "hello", "zh");
            WriteLayouts(root);

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            await new SiteEngine(logger).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var llms = File.ReadAllText(Path.Combine(root, "dist", "llms.txt"));
            var llmsFull = File.ReadAllText(Path.Combine(root, "dist", "llms-full.txt"));
            var robots = File.ReadAllText(Path.Combine(root, "dist", "robots.txt"));
            var publishAudit = File.ReadAllText(Path.Combine(root, "dist", ".bukit", "publish-audit-report.json"));

            Assert.Contains("https://example.com/en/blog/hello/", llms, StringComparison.Ordinal);
            Assert.Contains("https://example.com/zh/blog/hello/", llms, StringComparison.Ordinal);
            Assert.Contains("https://example.com/en/blog/hello/", llmsFull, StringComparison.Ordinal);
            Assert.Contains("https://example.com/zh/blog/hello/", llmsFull, StringComparison.Ordinal);
            Assert.Contains("Sitemap: https://example.com/sitemap.xml", robots, StringComparison.Ordinal);
            Assert.DoesNotContain("publish.llms_missing_route", publishAudit, StringComparison.Ordinal);
            Assert.DoesNotContain("publish.llms_full_missing_route", publishAudit, StringComparison.Ordinal);

            using var auditDoc = JsonDocument.Parse(publishAudit);
            var documents = auditDoc.RootElement.GetProperty("documents").EnumerateArray().ToArray();
            Assert.Contains(documents, x =>
                x.GetProperty("routeUrl").GetString() == "/en/blog/hello/" &&
                x.GetProperty("llmsIncluded").GetBoolean() &&
                x.GetProperty("robotsIncluded").GetBoolean());
            Assert.Contains(documents, x =>
                x.GetProperty("routeUrl").GetString() == "/zh/blog/hello/" &&
                x.GetProperty("llmsIncluded").GetBoolean() &&
                x.GetProperty("robotsIncluded").GetBoolean());
            Assert.Empty(logger.Errors);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void WritePost(string root, string fileName, string title, string slug, string language)
    {
        File.WriteAllText(Path.Combine(root, "content", fileName), $$"""
            ---
            type: post
            collection: post
            title: {{title}}
            slug: {{slug}}
            language: {{language}}
            publishAt: 2026-06-05T00:00:00Z
            ---
            # {{title}}
            """);
    }

    private static void WriteLayouts(string root)
    {
        File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
            <!doctype html>
            <html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>
            """);
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), """
            {% layout "layouts/base.html" %}
            {{ page.content }}
            """);
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), """
            {% layout "layouts/base.html" %}
            {{ page.content }}
            """);
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), """
            {% layout "layouts/base.html" %}
            Index
            """);
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), """
            {% layout "layouts/base.html" %}
            List
            """);
    }
}
