using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class LlmsProjectionIntegrationTests
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
    public async Task BuildAsync_LlmsTxt_ExcludesNoindexAndExpiredRoutes()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-llms-projection-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: llms-projection
                  title: Llms Projection
                  url: https://example.com
                  baseUrl: /
                  language: en
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
                  seo:
                    geo:
                      enabled: true
                      llmsTxt: true
                      llmsFullTxt: true
                content:
                  sources:
                    - type: markdown
                      name: post
                      collection: post
                      markdown:
                        dir: content
                  markdown:
                    dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);
            WritePost(root, "visible.md", "Visible", "visible", extraFrontMatter: "");
            WritePost(root, "hidden.md", "Hidden", "hidden", extraFrontMatter: "robots: noindex,nofollow");
            WritePost(root, "expired.md", "Expired", "expired", extraFrontMatter: "expires_at: 2024-01-01T00:00:00Z");
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

            var llms = File.ReadAllText(Path.Combine(root, "dist", "llms.txt"));
            var llmsFull = File.ReadAllText(Path.Combine(root, "dist", "llms-full.txt"));
            Assert.Contains("https://example.com/blog/visible/", llms, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/blog/hidden/", llms, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/blog/expired/", llms, StringComparison.Ordinal);
            Assert.Contains("https://example.com/blog/visible/", llmsFull, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/blog/hidden/", llmsFull, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/blog/expired/", llmsFull, StringComparison.Ordinal);
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

    private static void WritePost(string root, string fileName, string title, string slug, string extraFrontMatter)
    {
        var extra = string.IsNullOrWhiteSpace(extraFrontMatter) ? string.Empty : extraFrontMatter + Environment.NewLine;
        File.WriteAllText(Path.Combine(root, "content", fileName), $$"""
            ---
            type: post
            collection: post
            title: {{title}}
            slug: {{slug}}
            publishAt: 2026-06-05T00:00:00Z
            {{extra}}---
            # {{title}}
            """);
    }
}
