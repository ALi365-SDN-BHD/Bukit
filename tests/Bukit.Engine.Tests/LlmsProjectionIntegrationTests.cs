using Bukit.Config;
using Bukit.Shared;
using System.Text.Json;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn;
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
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);
            WritePost(root, "visible.md", "Visible", "visible", extraFrontMatter: "");
            File.AppendAllText(Path.Combine(root, "content", "visible.md"), "\n\nBefore [Official **website**](https://example.org/company?q=rail&lang=中文) after. [Related](../related/) [Details](#details)\n");
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
            var markdown = File.ReadAllText(Path.Combine(root, "dist", "content/blog/visible/index.html.md"));
            foreach (var output in new[] { markdown, llmsFull })
            {
                var links = Markdown.Parse(output).Descendants<LinkInline>().Select(link => Uri.UnescapeDataString(link.Url!)).ToArray();
                Assert.Contains("https://example.org/company?q=rail&lang=中文", links);
                Assert.Contains(output == markdown ? "/blog/related/" : "https://example.com/blog/related/", links);
                Assert.Contains(output == markdown ? "/blog/visible/#details" : "https://example.com/blog/visible/#details", links);
            }
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dist", "content/blog/visible/index.html.json")));
            var htmlBody = json.RootElement.GetProperty("body").GetString()!;
            Assert.Contains("href=\"../related/\"", htmlBody);
            Assert.Contains(htmlBody, File.ReadAllText(Path.Combine(root, "dist", "blog/visible/index.html")));
            Assert.Equal("/blog/visible/", json.RootElement.GetProperty("route").GetString());
            using var search = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dist", "search.json")));
            var searchItem = search.RootElement.EnumerateArray().Single(item => item.GetProperty("url").GetString() == "/blog/visible/");
            Assert.Equal(SearchIndexBuilder.StripHtmlToText(htmlBody), searchItem.GetProperty("content").GetString());
            Assert.Equal(json.RootElement.GetProperty("id").GetString(), searchItem.GetProperty("id").GetString());
            var document = ContentDocument.Create("visible", "Visible", "visible", DateTimeOffset.UnixEpoch, htmlBody);
            var route = new RouteInfo("/blog/visible/", "blog/visible/index.html", "pages/post.html");
            await LlmsTxtPlugin.WriteLlmsFullTxtAsync(config, Path.Combine(root, "async"), "/", [new(document, route)], [],
                CanonicalContentGraph.Empty, new Dictionary<string, SeoIndexEntry>
                {
                    [route.OutputPath] = new(route, "https://example.com/blog/visible/", null, true, DateTimeOffset.UnixEpoch, document.Id, "post")
                }, NullContentBodyStore.Instance);
            Assert.Equal(Markdown.Parse(llmsFull).Descendants<LinkInline>().Select(link => link.Url),
                Markdown.Parse(File.ReadAllText(Path.Combine(root, "async", "llms-full.txt"))).Descendants<LinkInline>().Select(link => link.Url));
            Assert.DoesNotContain("https://example.com/blog/hidden/", llmsFull, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/blog/expired/", llmsFull, StringComparison.Ordinal);

            // AI agents can find each page's Markdown and JSON versions from the HTML and from llms.txt.
            var visibleHtml = File.ReadAllText(Path.Combine(root, "dist", "blog/visible/index.html"));
            foreach (var (type, extension) in new[] { ("text/markdown", ".md"), ("application/json", ".json") })
            {
                var href = System.Text.RegularExpressions.Regex.Match(
                    visibleHtml, $"<link rel=\"alternate\" type=\"{type}\" href=\"([^\"]+)\"").Groups[1].Value;
                Assert.Equal("/content/blog/visible/index.html" + extension, href);
                Assert.True(File.Exists(Path.Combine(root, "dist", href.TrimStart('/'))));
            }

            Assert.DoesNotContain("text/markdown", File.ReadAllText(Path.Combine(root, "dist", "blog/hidden/index.html")), StringComparison.Ordinal);
            var expiredHtml = Path.Combine(root, "dist", "blog/expired/index.html");
            // Expired pages keep their links: their .md/.json versions stay published, and page
            // output must not depend on the build clock.
            Assert.Contains("text/markdown", File.ReadAllText(expiredHtml), StringComparison.Ordinal);
            Assert.Contains("- [Agent manifest](https://example.com/agent-manifest.json): ", llms, StringComparison.Ordinal);
            var manifestBytes = File.ReadAllBytes(Path.Combine(root, "dist", "agent-manifest.json"));
            // No BOM: JSON parsers may reject one (RFC 8259).
            Assert.Equal((byte)'{', manifestBytes[0]);
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
    public async Task BuildAsync_ThemeRenderMode_ExposesRepresentationUrlsToTemplates()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-representation-urls-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: representation-urls
                  title: Representation Urls
                  url: https://example.com
                  baseUrl: /repo/
                  language: en
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
                  seo:
                    renderMode: theme
                    geo:
                      enabled: true
                content:
                  sources:
                    - type: markdown
                      name: post
                      collection: post
                      markdown:
                        dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);
            WritePost(root, "visible.md", "Visible", "visible", extraFrontMatter: "");
            WritePost(root, "hidden.md", "Hidden", "hidden", extraFrontMatter: "robots: noindex,nofollow");
            // The layout follows the guide: it links the versions only when the page exposes them.
            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!doctype html>
                <html><head><title>{{ page.title }}</title>{{ if page.representation_urls }}<link rel="alternate" type="text/markdown" href="{{ page.representation_urls.markdown }}"><link rel="alternate" type="application/json" href="{{ page.representation_urls.json }}">{{ end }}</head><body>{{ content }}</body></html>
                """);
            foreach (var name in new[] { "post", "page", "index", "list" })
            {
                File.WriteAllText(Path.Combine(root, "layouts", "pages", name + ".html"), """
                    {% layout "layouts/base.html" %}
                    {{ page.content }}
                    """);
            }

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            await new SiteEngine(logger).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var html = File.ReadAllText(Path.Combine(root, "dist", "blog/visible/index.html"));
            foreach (var (type, extension) in new[] { ("text/markdown", ".md"), ("application/json", ".json") })
            {
                var hrefs = System.Text.RegularExpressions.Regex.Matches(html, $"type=\"{type}\" href=\"([^\"]+)\"")
                    .Select(match => match.Groups[1].Value)
                    .ToArray();
                // Theme mode: only the theme's own link, pointing at the file Bukit wrote.
                var href = Assert.Single(hrefs);
                Assert.Equal("/repo/content/blog/visible/index.html" + extension, href);
                Assert.True(File.Exists(Path.Combine(root, "dist", href["/repo/".Length..])));
            }

            // A noindex page gets no URLs, so the theme does not link its .md copy (which has no noindex marker).
            var hiddenHtml = File.ReadAllText(Path.Combine(root, "dist", "blog/hidden/index.html"));
            Assert.DoesNotContain("text/markdown", hiddenHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("application/json", hiddenHtml, StringComparison.Ordinal);
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
            markdown:
              dir: content
            title: {{title}}
            slug: {{slug}}
            publishAt: 2026-06-05T00:00:00Z
            {{extra}}---
            # {{title}}
            """);
    }
}
