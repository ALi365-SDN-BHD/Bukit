using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Routing;
using Bukit.Shared;
using System.Text.Json;
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
    public async Task BuildAsync_SeoInjectMode_InjectsHeadAndExcludesNoindexFromOutputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-seo-inject-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: test-site
                  title: Test "Site" & More
                  description: Site <fallback> & description
                  url: https://example.com/
                  baseUrl: /docs/
                  language: en-US
                  seo:
                    renderMode: inject
                    diagnostics: strict
                    defaultImage: /assets/default-og.png
                    robotsTxt:
                      enabled: true
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

            File.WriteAllText(Path.Combine(root, "content", "visible.md"), """
                ---
                type: post
                title: Visible "Post" & News
                slug: visible
                publishAt: 2024-06-01T00:00:00Z
                summary: Visible <summary> & text
                ---
                # Visible
                """);

            File.WriteAllText(Path.Combine(root, "content", "hidden.md"), """
                ---
                type: post
                title: Hidden Post
                slug: hidden
                publishAt: 2024-06-02T00:00:00Z
                robots: noindex,nofollow
                ---
                # Hidden
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html>
                <html>
                <head>
                  <meta charset="utf-8" />
                  <title>{{ page.title }}</title>
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

            var visibleHtml = File.ReadAllText(Path.Combine(root, "dist", "blog", "visible", "index.html"));
            Assert.Contains("<link rel=\"canonical\" href=\"https://example.com/docs/blog/visible/\"", visibleHtml, StringComparison.Ordinal);
            Assert.Contains("<meta name=\"description\" content=\"Visible &lt;summary&gt; &amp; text\"", visibleHtml, StringComparison.Ordinal);
            Assert.Contains("<meta property=\"og:title\" content=\"Visible &quot;Post&quot; &amp; News\"", visibleHtml, StringComparison.Ordinal);
            Assert.Contains("googletagmanager.com/gtag/js?id=G-ABC123", visibleHtml, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(visibleHtml, "rel=\"canonical\""));

            var sitemap = File.ReadAllText(Path.Combine(root, "dist", "sitemap.xml"));
            Assert.Contains("https://example.com/docs/blog/visible/", sitemap, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/docs/blog/hidden/", sitemap, StringComparison.Ordinal);

            var search = File.ReadAllText(Path.Combine(root, "dist", "search.json"));
            Assert.Contains("/docs/blog/visible/", search, StringComparison.Ordinal);
            Assert.DoesNotContain("/docs/blog/hidden/", search, StringComparison.Ordinal);

            var rss = File.ReadAllText(Path.Combine(root, "dist", "rss.xml"));
            Assert.Contains("https://example.com/docs/blog/visible/", rss, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/docs/blog/hidden/", rss, StringComparison.Ordinal);

            var robots = File.ReadAllText(Path.Combine(root, "dist", "robots.txt"));
            Assert.Contains("Sitemap: https://example.com/docs/sitemap.xml", robots, StringComparison.Ordinal);
            Assert.Empty(logger.Errors);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_StrongSeoSuite_CoversFinalRoutesAndCollectionSchemas()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-strong-seo-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: strong-seo
                  title: Strong SEO
                  description: Strong SEO fallback
                  url: https://example.com
                  baseUrl: /docs/
                  language: en-US
                  collections:
                    post:
                      permalink: /articles/{slug}/
                      template: pages/post.html
                      listRoute: /articles/
                    page:
                      permalink: /{slug}/
                      template: pages/page.html
                  seo:
                    renderMode: inject
                    diagnostics: strict
                    schema:
                      webPage: true
                      collectionPage: true
                      searchAction: true
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
                taxonomy:
                  pageSize: 1
                """);

            File.WriteAllText(Path.Combine(root, "content", "one.md"), """
                ---
                collection: post
                title: One
                slug: one
                publishAt: 2024-06-01T00:00:00Z
                tags: [seo]
                ---
                # One
                """);
            File.WriteAllText(Path.Combine(root, "content", "two.md"), """
                ---
                collection: post
                title: Two
                slug: two
                publishAt: 2024-06-02T00:00:00Z
                tags: [seo]
                ---
                # Two
                """);
            File.WriteAllText(Path.Combine(root, "content", "hidden.md"), """
                ---
                collection: post
                title: Hidden
                slug: hidden
                publishAt: 2024-06-03T00:00:00Z
                tags: [seo]
                robots: noindex
                ---
                # Hidden
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html>
                <html>
                <head>
                  <meta charset="utf-8" />
                  <title>{{ page.title }}</title>
                  <link rel="canonical" href="https://duplicate.example/" />
                  <meta property="og:title" content="duplicate" />
                  <script type="application/ld+json">{"duplicate":true}</script>
                </head>
                <body>{{ content }}</body>
                </html>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{% layout \"layouts/base.html\" %}\nIndex");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "{% layout \"layouts/base.html\" %}\nList");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "taxonomy-index.html"), "{% layout \"layouts/base.html\" %}\nTaxonomy");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "taxonomy-term.html"), "{% layout \"layouts/base.html\" %}\nTerm");

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            var engine = new SiteEngine(logger);
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var indexHtml = File.ReadAllText(Path.Combine(root, "dist", "index.html"));
            Assert.Contains("<link rel=\"canonical\" href=\"https://example.com/docs/\"", indexHtml, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"WebPage\"", indexHtml, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"SearchAction\"", indexHtml, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(indexHtml, "rel=\"canonical\""));
            Assert.DoesNotContain("duplicate.example", indexHtml, StringComparison.Ordinal);

            var listHtml = File.ReadAllText(Path.Combine(root, "dist", "articles", "index.html"));
            Assert.Contains("<link rel=\"canonical\" href=\"https://example.com/docs/articles/\"", listHtml, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"CollectionPage\"", listHtml, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"ItemList\"", listHtml, StringComparison.Ordinal);

            var taxonomyHtml = File.ReadAllText(Path.Combine(root, "dist", "tags", "seo", "page", "2", "index.html"));
            Assert.Contains("<link rel=\"canonical\" href=\"https://example.com/docs/tags/seo/page/2/\"", taxonomyHtml, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"CollectionPage\"", taxonomyHtml, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"ItemList\"", taxonomyHtml, StringComparison.Ordinal);

            var sitemap = File.ReadAllText(Path.Combine(root, "dist", "sitemap.xml"));
            Assert.Contains("https://example.com/docs/", sitemap, StringComparison.Ordinal);
            Assert.Contains("https://example.com/docs/articles/", sitemap, StringComparison.Ordinal);
            Assert.Contains("https://example.com/docs/tags/seo/page/2/", sitemap, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/docs/articles/hidden/", sitemap, StringComparison.Ordinal);

            var search = File.ReadAllText(Path.Combine(root, "dist", "search.json"));
            Assert.Contains("/docs/articles/one/", search, StringComparison.Ordinal);
            Assert.DoesNotContain("/docs/articles/hidden/", search, StringComparison.Ordinal);
            Assert.Empty(logger.Errors);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_SeoDiagnosticsStrict_FailsWhenThemeModeOmitsSeoTags()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-seo-strict-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: strict-seo
                  title: Strict SEO
                  url: https://example.com
                  seo:
                    renderMode: theme
                    diagnostics: strict
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
            File.WriteAllText(Path.Combine(root, "content", "one.md"), """
                ---
                type: post
                title: One
                slug: one
                ---
                # One
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{% layout \"layouts/base.html\" %}\nIndex");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "{% layout \"layouts/base.html\" %}\nList");

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            var engine = new SiteEngine(logger);

            var ex = await Assert.ThrowsAsync<ConfigException>(() => engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None));
            Assert.Contains("seo.canonical_missing", ex.Message, StringComparison.Ordinal);
            Assert.NotEmpty(logger.Errors);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_SeoInjectMode_I18nPagesEmitMutualHreflang()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-seo-i18n-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: i18n-seo
                  title: I18n SEO
                  url: https://example.com
                  baseUrl: /
                  language: en-US
                  languages: [en-US, ms-MY]
                  defaultLanguage: en-US
                  sitemapMode: merged
                  seo:
                    renderMode: inject
                    diagnostics: strict
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
            File.WriteAllText(Path.Combine(root, "content", "hello.en.md"), """
                ---
                type: page
                title: Hello
                slug: hello
                language: en-US
                i18nKey: hello
                tags: [shared]
                ---
                # Hello
                """);
            File.WriteAllText(Path.Combine(root, "content", "hello.ms.md"), """
                ---
                type: page
                title: Helo
                slug: helo
                language: ms-MY
                i18nKey: hello
                tags: [shared]
                ---
                # Helo
                """);
            File.WriteAllText(Path.Combine(root, "content", "solo.en.md"), """
                ---
                type: page
                title: Solo
                slug: solo
                language: en-US
                ---
                # Solo
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{% layout \"layouts/base.html\" %}\nIndex");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "{% layout \"layouts/base.html\" %}\nList");

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            var engine = new SiteEngine(logger);
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var enHtml = File.ReadAllText(Path.Combine(root, "dist", "en-US", "pages", "hello", "index.html"));
            Assert.Contains("hreflang=\"x-default\" href=\"https://example.com/en-US/pages/hello/\"", enHtml, StringComparison.Ordinal);
            Assert.Contains("hreflang=\"en-US\" href=\"https://example.com/en-US/pages/hello/\"", enHtml, StringComparison.Ordinal);
            Assert.Contains("hreflang=\"ms-MY\" href=\"https://example.com/ms-MY/pages/helo/\"", enHtml, StringComparison.Ordinal);

            var soloHtml = File.ReadAllText(Path.Combine(root, "dist", "en-US", "pages", "solo", "index.html"));
            Assert.DoesNotContain("hreflang=", soloHtml, StringComparison.Ordinal);

            var tagHtml = File.ReadAllText(Path.Combine(root, "dist", "en-US", "tags", "shared", "index.html"));
            Assert.Contains("hreflang=\"x-default\" href=\"https://example.com/en-US/tags/shared/\"", tagHtml, StringComparison.Ordinal);
            Assert.Contains("hreflang=\"en-US\" href=\"https://example.com/en-US/tags/shared/\"", tagHtml, StringComparison.Ordinal);
            Assert.Contains("hreflang=\"ms-MY\" href=\"https://example.com/ms-MY/tags/shared/\"", tagHtml, StringComparison.Ordinal);

            var sitemap = File.ReadAllText(Path.Combine(root, "dist", "sitemap.xml"));
            Assert.Contains("hreflang=\"x-default\" href=\"https://example.com/en-US/pages/hello/\"", sitemap, StringComparison.Ordinal);
            Assert.Contains("hreflang=\"ms-MY\" href=\"https://example.com/ms-MY/pages/helo/\"", sitemap, StringComparison.Ordinal);
            Assert.Contains("hreflang=\"ms-MY\" href=\"https://example.com/ms-MY/tags/shared/\"", sitemap, StringComparison.Ordinal);
            Assert.Empty(logger.Errors);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_SeoAuditReport_WritesRouteInventoryAndIssues()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-seo-report-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: seo-report
                  title: SEO Report
                  url: https://example.com
                  baseUrl: /docs/
                  seo:
                    renderMode: inject
                    diagnostics: warn
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
            File.WriteAllText(Path.Combine(root, "content", "visible.md"), """
                ---
                type: post
                title: This is a deliberately long SEO title that should be reported because it is over the normal search result length
                slug: visible
                summary: Visible post summary
                publishAt: 2024-01-01T00:00:00Z
                ---
                # Visible
                """);
            File.WriteAllText(Path.Combine(root, "content", "hidden.md"), """
                ---
                type: post
                title: Hidden
                slug: hidden
                robots: noindex
                publishAt: 2024-01-02T00:00:00Z
                ---
                # Hidden
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{% layout \"layouts/base.html\" %}\nIndex");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "{% layout \"layouts/base.html\" %}\nList");

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var engine = new SiteEngine(new TestLogger());
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var reportPath = Path.Combine(root, "dist", "seo-report.json");
            Assert.True(File.Exists(reportPath));
            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
            var rootElement = doc.RootElement;
            Assert.Equal("https://example.com", rootElement.GetProperty("siteUrl").GetString());
            Assert.True(rootElement.GetProperty("generatedAt").ValueKind == JsonValueKind.String);

            var routes = rootElement.GetProperty("routes").EnumerateArray().ToArray();
            var visible = routes.Single(x => x.GetProperty("url").GetString() == "/blog/visible/");
            Assert.True(visible.GetProperty("indexable").GetBoolean());
            Assert.True(visible.GetProperty("sitemapIncluded").GetBoolean());
            Assert.True(visible.GetProperty("searchIncluded").GetBoolean());
            Assert.True(visible.GetProperty("rssIncluded").GetBoolean());
            Assert.Contains("BlogPosting", visible.GetProperty("schemaTypes").EnumerateArray().Select(x => x.GetString()));

            var hidden = routes.Single(x => x.GetProperty("url").GetString() == "/blog/hidden/");
            Assert.False(hidden.GetProperty("indexable").GetBoolean());
            Assert.False(hidden.GetProperty("sitemapIncluded").GetBoolean());
            Assert.False(hidden.GetProperty("searchIncluded").GetBoolean());
            Assert.False(hidden.GetProperty("rssIncluded").GetBoolean());

            var issues = rootElement.GetProperty("issues").EnumerateArray().ToArray();
            Assert.Contains(issues, x => x.GetProperty("code").GetString() == "seo.title_too_long" &&
                                         x.GetProperty("severity").GetString() == "warning");
            Assert.True(rootElement.GetProperty("summary").GetProperty("warningCount").GetInt32() >= 1);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
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

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
