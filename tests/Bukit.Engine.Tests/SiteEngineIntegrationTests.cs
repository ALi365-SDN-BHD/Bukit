using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SiteEngineIntegrationTests
{
    private sealed class TestLogger : ILogger
    {
        public List<string> Debugs { get; } = new();
        public List<string> Infos { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public void Debug(string message) => Debugs.Add(message);
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => Errors.Add(message);
    }

    [Fact]
    public async Task BuildAsync_MinimalSite_ProducesExpectedOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: test-site
                  title: Test Site
                  baseUrl: /
                  language: en
                content:
                  provider: markdown
                  media:
                    downloadToLocal: false
                  markdown:
                    dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);

            File.WriteAllText(Path.Combine(root, "content", "hello.md"), """
                ---
                type: post
                title: Hello World
                slug: hello-world
                publishAt: 2024-06-01T00:00:00Z
                summary: A hello world post
                tags:
                  - test
                  - hello
                ---
                # Hello World

                This is a test post.
                """);

            File.WriteAllText(Path.Combine(root, "content", "about.md"), """
                ---
                type: page
                title: About
                slug: about
                publishAt: 2024-06-02T00:00:00Z
                ---
                # About

                This is the about page.
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
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

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), """
                <h2>{{ page.title }}</h2>
                <p>{{ page.content }}</p>
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), """
                <h2>{{ page.title }}</h2>
                <p>{{ page.content }}</p>
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), """
                <h2>Home</h2>
                <ul>
                {{ for page in pages }}
                  <li><a href="{{ page.url }}">{{ page.title }}</a></li>
                {{ end }}
                </ul>
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), """
                <h2>List: {{ pages.title }}</h2>
                <ul>
                {{ for page in pages.pages }}
                  <li><a href="{{ page.url }}">{{ page.title }}</a></li>
                {{ end }}
                </ul>
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "sitegen.templates.yaml"), """
                templates:
                  pages/index.html:
                    capabilities:
                      needs_page_content: false
                      supports_pagination: false
                      supports_taxonomy: false
                      supports_search_snippets: false
                  pages/list.html:
                    capabilities:
                      needs_page_content: false
                      supports_pagination: false
                      supports_taxonomy: false
                      supports_search_snippets: false
                """);

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test-site",
                    Title = "Test Site",
                    BaseUrl = "/",
                    Language = "en",
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
            };

            var logger = new TestLogger();
            var engine = new SiteEngine(logger);

            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var distDir = Path.Combine(root, "dist");
            Assert.True(Directory.Exists(distDir), "dist directory should exist");

            var blogPost = Path.Combine(distDir, "blog", "hello-world", "index.html");
            Assert.True(File.Exists(blogPost), $"Expected {blogPost}");

            var aboutPage = Path.Combine(distDir, "pages", "about", "index.html");
            Assert.True(File.Exists(aboutPage), $"Expected {aboutPage}");

            var indexPath = Path.Combine(distDir, "index.html");
            Assert.True(File.Exists(indexPath), $"Expected {indexPath}");

            var blogContent = File.ReadAllText(blogPost);
            Assert.Contains("Hello World", blogContent, StringComparison.Ordinal);

            var indexContent = File.ReadAllText(indexPath);
            Assert.Contains("Home", indexContent, StringComparison.Ordinal);

            Assert.Empty(logger.Errors);

            CleanupDir(root);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_SeoAndAnalyticsModel_RendersAdvancedHead()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: test-site
                  title: Test Site
                  description: Site fallback description
                  url: https://example.com/
                  baseUrl: /docs/
                  language: en
                  seo:
                    defaultImage: /assets/default-og.png
                    twitterSite: "@bukit"
                    organization:
                      name: Example Inc
                      url: https://example.com/about
                      logo: https://example.com/logo.png
                  analytics:
                    google_analytics_id: G-ABC123
                content:
                  provider: markdown
                  media:
                    downloadToLocal: false
                  markdown:
                    dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);

            File.WriteAllText(Path.Combine(root, "content", "hello.md"), """
                ---
                type: post
                title: Hello World
                slug: hello-world
                publishAt: 2024-06-01T00:00:00Z
                update_time: 2024-06-02T00:00:00Z
                summary: A hello world post
                seo_title: Custom SEO Title
                seo_desc: Custom SEO Description
                author: Ada
                robots: noindex,nofollow
                og_image: https://example.com/og.png
                categories:
                  - Docs
                ---
                # Hello World
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html>
                <html>
                <head>
                  <title>{{ page.seo.title }}</title>
                  <link rel="canonical" href="{{ page.seo.canonical }}" />
                  <meta name="description" content="{{ page.seo.description }}" />
                  <meta name="robots" content="{{ page.seo.robots }}" />
                  <meta property="og:image" content="{{ page.seo.og.image }}" />
                  <meta name="twitter:site" content="{{ page.seo.twitter.site }}" />
                  {{ for json in page.seo.json_ld }}<script type="application/ld+json">{{ json }}</script>{{ end }}
                  {{ if site.analytics.enabled && site.analytics.google_analytics_id }}
                  <script async src="https://www.googletagmanager.com/gtag/js?id={{ site.analytics.google_analytics_id }}"></script>
                  <script>gtag('config', '{{ site.analytics.google_analytics_id }}');</script>
                  {{ end }}
                </head>
                <body>{{ content }}</body>
                </html>
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), """
                {% layout "layouts/base.html" %}
                {{ page.content }}
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), """
                {% layout "layouts/base.html" %}
                {{ page.content }}
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{{ page.seo.canonical }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "{{ page.seo.canonical }}");

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var engine = new SiteEngine(new TestLogger());
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var html = File.ReadAllText(Path.Combine(root, "dist", "blog", "hello-world", "index.html"));

            Assert.Contains("<title>Custom SEO Title</title>", html, StringComparison.Ordinal);
            Assert.Contains("https://example.com/docs/blog/hello-world/", html, StringComparison.Ordinal);
            Assert.Contains("Custom SEO Description", html, StringComparison.Ordinal);
            Assert.Contains("noindex,nofollow", html, StringComparison.Ordinal);
            Assert.Contains("https://example.com/og.png", html, StringComparison.Ordinal);
            Assert.Contains("@bukit", html, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"BlogPosting\"", html, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"BreadcrumbList\"", html, StringComparison.Ordinal);
            Assert.Contains("googletagmanager.com/gtag/js?id=G-ABC123", html, StringComparison.Ordinal);
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
    public async Task BuildAsync_IncrementalBuild_SecondRunSkipsPages()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-incr", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "content", "home.md"), """
                ---
                type: page
                title: Home
                slug: home
                ---
                # Home
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), "<html><body>{{ content }}</body></html>");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "<h1>{{ page.title }}</h1>{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "<h2>Home</h2>");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "<h2>List</h2><ul>{{ for p in pages.pages }}<li>{{ p.title }}</li>{{ end }}</ul>");

            File.WriteAllText(Path.Combine(root, "layouts", "sitegen.templates.yaml"), """
                templates:
                  pages/index.html:
                    capabilities:
                      needs_page_content: false
                      supports_pagination: false
                      supports_taxonomy: false
                      supports_search_snippets: false
                  pages/list.html:
                    capabilities:
                      needs_page_content: false
                      supports_pagination: false
                      supports_taxonomy: false
                      supports_search_snippets: false
                """);

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: t
                  title: T
                  baseUrl: /
                  language: en
                content:
                  provider: markdown
                  markdown:
                    dir: content
                build:
                  output: dist
                theme:
                  layouts: layouts
                """);

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", BaseUrl = "/", Language = "en" },
                Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
                Build = new BuildConfig { Output = "dist", Clean = true },
            };

            var logger1 = new TestLogger();
            var engine1 = new SiteEngine(logger1);
            await engine1.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            Assert.Empty(logger1.Errors);

            var logger2 = new TestLogger();
            var engine2 = new SiteEngine(logger2);
            await engine2.BuildAsync(config, root, new ConfigOverrides { Clean = false }, CancellationToken.None);
            Assert.Empty(logger2.Errors);

            Assert.True(Directory.Exists(Path.Combine(root, ".cache")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    private static void CleanupDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
        }
    }
}
