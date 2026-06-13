using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildPipelinePerformanceTests
{
    private const int PerformanceThresholdMs = 30_000;

    [Fact]
    public async Task FullBuild_With10Pages_CompletesUnderThreshold_AndAllStageKeysPresent()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-perf-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            var themeName = "starter";
            var themeDir = Path.Combine(root, "themes", themeName);
            Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
            Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), $$"""
                site:
                  name: perf-test
                  title: Performance Test
                  baseUrl: /
                  language: en
                  seo:
                    enabled: false
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
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
                  name: {{themeName}}
                """);

            for (var i = 1; i <= 10; i++)
            {
                File.WriteAllText(Path.Combine(root, "content", $"page-{i}.md"), $"""
                    ---
                    type: post
                    collection: post
                    markdown:
                      dir: content
                    title: Page {i}
                    slug: page-{i}
                    publishAt: 2024-01-{i:D2}T00:00:00Z
                    summary: This is page {i}
                    tags:
                      - test
                      - page-{i}
                    ---
                    # Page {i}

                    Content for page {i}.
                    """);
            }

            File.WriteAllText(Path.Combine(themeDir, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html>
                <html>
                <head>
                  <meta charset="utf-8">
                  <title>{{ site.title }}</title>
                </head>
                <body>
                  <h1>{{ site.title }}</h1>
                  {{ content }}
                </body>
                </html>
                """);

            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "post.html"), """
                <h2>{{ page.title }}</h2>
                <p>{{ page.summary }}</p>
                <div>{{ page.content }}</div>
                """);

            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "page.html"), """
                <h2>{{ page.title }}</h2>
                <div>{{ page.content }}</div>
                """);

            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "index.html"), """
                <h2>Home</h2>
                <ul>
                {{ for page in pages }}
                  <li><a href="{{ page.url }}">{{ page.title }}</a></li>
                {{ end }}
                </ul>
                """);

            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "list.html"), """
                <h2>{{ pages.title }}</h2>
                <ul>
                {{ for page in pages.pages }}
                  <li><a href="{{ page.url }}">{{ page.title }}</a></li>
                {{ end }}
                </ul>
                """);
            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "taxonomy-index.html"), "{{ page.content }}");
            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "taxonomy-term.html"), "{{ page.content }}");

            File.WriteAllText(Path.Combine(themeDir, "theme.yaml"), $"""
                name: {themeName}
                version: 1.0
                templates:
                  home:
                    template: pages/index.html
                    required: true
                  post:
                    template: pages/post.html
                    accepts:
                      type: post
                  list:
                    template: pages/list.html
                    accepts:
                      kind: list
                  taxonomy_index:
                    template: pages/taxonomy-index.html
                    accepts:
                      kind: taxonomy_index
                  taxonomy_term:
                    template: pages/taxonomy-term.html
                    accepts:
                      kind: taxonomy_term
                """);

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var overrides = new ConfigOverrides { Incremental = false };
            var engine = new SiteEngine(new SilentLogger());

            var sw = Stopwatch.StartNew();
            var result = await engine.BuildAsync(config, root, overrides);
            sw.Stop();

            Assert.NotNull(result);
            Assert.True(sw.ElapsedMilliseconds < PerformanceThresholdMs,
                $"Build took {sw.ElapsedMilliseconds}ms, exceeding threshold {PerformanceThresholdMs}ms");
            Assert.True(result.DurationMs > 0, "Build duration should be > 0");
            Assert.True(result.DurationMs < PerformanceThresholdMs,
                $"Build duration {result.DurationMs}ms exceeds threshold {PerformanceThresholdMs}ms");
            Assert.True(result.Summary.PageCount >= 10, $"Expected at least 10 pages, got {result.Summary.PageCount}");
            Assert.True(result.Summary.RouteCount >= 10, $"Expected at least 10 routes, got {result.Summary.RouteCount}");
            Assert.NotEmpty(result.Variants);

            var outputDir = Path.Combine(root, "dist");
            Assert.True(File.Exists(Path.Combine(outputDir, "index.html")), "index.html missing");
            Assert.True(File.Exists(Path.Combine(outputDir, "blog", "page-1", "index.html")), "blog/page-1/index.html missing");
            Assert.True(File.Exists(Path.Combine(outputDir, "blog", "page-10", "index.html")), "blog/page-10/index.html missing");

            foreach (var variant in result.Variants)
            {
                Assert.True(variant.RenderedCount > 0, $"Variant {variant.Language} rendered nothing");
                Assert.True(variant.RouteCount > 0, $"Variant {variant.Language} has no routes");
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task FullBuild_With1Page_ProducesAllExpectedOutputFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-perf-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(root, "static"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: minimal
                  title: Minimal Site
                  baseUrl: /
                  language: en
                  seo:
                    enabled: false
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
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
                  name: starter
                """);

            File.WriteAllText(Path.Combine(root, "content", "hello.md"), """
                ---
                type: post
                collection: post
                markdown:
                  dir: content
                title: Hello World
                slug: hello-world
                publishAt: 2024-01-01T00:00:00Z
                summary: Hello
                ---
                # Hello World
                Content here.
                """);

            var themeDir = Path.Combine(root, "themes", "starter");
            Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
            Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "pages"));

            File.WriteAllText(Path.Combine(themeDir, "theme.yaml"), """
                name: starter
                version: 1.0
                templates:
                  home:
                    template: pages/index.html
                    required: true
                  post:
                    template: pages/post.html
                    accepts:
                      type: post
                  list:
                    template: pages/list.html
                    accepts:
                      kind: list
                """);

            File.WriteAllText(Path.Combine(themeDir, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html>
                <html><head><title>{{ site.title }}</title></head>
                <body><h1>{{ site.title }}</h1>{{ content }}</body>
                </html>
                """);

            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "post.html"), "<h2>{{ page.title }}</h2>{{ page.content }}");
            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "page.html"), "<h2>{{ page.title }}</h2>{{ page.content }}");
            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "index.html"), "<ul>{{ for p in pages }}<li>{{ p.title }}</li>{{ end }}</ul>");

            File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "list.html"), """
                <h2>{{ pages.title }}</h2>
                <ul>{{ for p in pages.pages }}<li>{{ p.title }}</li>{{ end }}</ul>
                """);

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var overrides = new ConfigOverrides { Incremental = false };
            var engine = new SiteEngine(new SilentLogger());

            var result = await engine.BuildAsync(config, root, overrides);

            Assert.NotNull(result);
            Assert.True(result.DurationMs > 0);
            Assert.True(result.Summary.PageCount >= 1);
            Assert.NotEmpty(result.Variants);

            var outputDir = Path.Combine(root, "dist");
            Assert.True(File.Exists(Path.Combine(outputDir, "index.html")));
            Assert.True(File.Exists(Path.Combine(outputDir, "blog", "hello-world", "index.html")));

            var variant = result.Variants[0];
            Assert.True(variant.RenderedCount > 0);
            Assert.True(variant.RouteCount > 0);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class SilentLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
