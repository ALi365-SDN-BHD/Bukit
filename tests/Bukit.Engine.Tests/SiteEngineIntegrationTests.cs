using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
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

    private sealed class StaticContentProviderFactory : IContentProviderFactory
    {
        private readonly ContentLoadResult _result;

        public StaticContentProviderFactory(ContentLoadResult result)
        {
            _result = result;
        }

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger) => new StaticContentProvider(_result);

        public Task<ContentLoadResult> LocalizeContentImagesAsync(ContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class StaticContentProvider : IContentProvider
    {
        private readonly ContentLoadResult _result;

        public StaticContentProvider(ContentLoadResult result)
        {
            _result = result;
        }

        public Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_result);
    }

    private sealed class DictionaryContentBodyStore : IContentBodyStore
    {
        private readonly IReadOnlyDictionary<string, string> _bodies;

        public DictionaryContentBodyStore(IReadOnlyDictionary<string, string> bodies)
        {
            _bodies = bodies;
        }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(_bodies[item.Id]));
    }

    private sealed class RenderConcurrencyProbe
    {
        private int _current;
        private int _maxObserved;

        public int MaxObserved => _maxObserved;

        public void Enter()
        {
            var current = Interlocked.Increment(ref _current);
            int observed;
            do
            {
                observed = _maxObserved;
                if (current <= observed)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref _maxObserved, current, observed) != observed);
        }

        public void Exit() => Interlocked.Decrement(ref _current);
    }

    private sealed class ProbeTemplateRenderer : ITemplateRenderer
    {
        private readonly RenderConcurrencyProbe _probe;

        public ProbeTemplateRenderer(RenderConcurrencyProbe probe)
        {
            _probe = probe;
        }

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            _probe.Enter();
            try
            {
                Thread.Sleep(50);
                return "<html><body>page</body></html>";
            }
            finally
            {
                _probe.Exit();
            }
        }

        public string RenderList(string templateRelativePath, ListPageModel model)
            => "<html><body>list</body></html>";
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
                collection: post
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
                collection: page
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
                    Collections = TestCollections()
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                },
                Build = new BuildConfig
                {
                    Output = "dist",
                    Clean = true,
                    Report = new BuildReportConfig
                    {
                        Enabled = true
                    }
                },
            };

            var logger = new TestLogger();
            var engine = new SiteEngine(logger);

            WriteTestThemeTemplates(root);

            var result = await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            Assert.Equal("dist", result.Project.Output);
            Assert.Equal("markdown", result.Project.ContentSource);
            Assert.True(result.Summary.PageCount > 0);
            Assert.Single(result.Variants);

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

            var reportDir = Path.Combine(distDir, ".bukit");
            Assert.True(File.Exists(Path.Combine(reportDir, "build-report.json")));
            Assert.True(File.Exists(Path.Combine(reportDir, "routes.json")));
            Assert.True(File.Exists(Path.Combine(reportDir, "security-report.json")));
            using var routesDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(reportDir, "routes.json")));
            Assert.Contains(routesDoc.RootElement.GetProperty("routes").EnumerateArray(), route => route.GetProperty("url").GetString() == "/blog/hello-world/");

            Assert.Empty(logger.Errors);

            CleanupDir(root);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_WritesPublishRepresentationsAndAuditArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-publish-projection-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "content", "hello.md"), """
                ---
                type: post
                collection: post
                title: Hello World
                slug: hello-world
                publishAt: 2024-06-01T00:00:00Z
                updatedAt: 2024-06-02T00:00:00Z
                summary: A hello world post
                author: Ali
                source: notion
                review_status: approved
                entities:
                  - Bukit
                ---
                # Hello World

                This is a test post.
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html>
                <html>
                <head>
                  <meta charset="utf-8">
                  <title>{{ page.title }}</title>
                  <link rel="canonical" href="{{ page.url }}">
                </head>
                <body>{{ content }}</body>
                </html>
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), """
                <main>
                  <article>
                    <h1>{{ page.title }}</h1>
                    <p>{{ page.summary }}</p>
                    <time datetime="{{ page.publish_date | date.to_string "%Y-%m-%d" }}">{{ page.publish_date | date.to_string "%Y-%m-%d" }}</time>
                    {{ page.content }}
                  </article>
                </main>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "<main><article><h1>{{ page.title }}</h1>{{ page.content }}</article></main>");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "<main><h1>{{ site.title }}</h1></main>");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "<main><h1>{{ page.title }}</h1></main>");

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test-site",
                    Title = "Test Site",
                    Url = "https://example.com",
                    BaseUrl = "/",
                    Language = "en",
                    Collections = TestCollections()
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                },
                Build = new BuildConfig
                {
                    Output = "dist",
                    Clean = true,
                    Report = new BuildReportConfig { Enabled = true }
                },
            };

            var logger = new TestLogger();
            var engine = new SiteEngine(logger);

            WriteTestThemeTemplates(root);

            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var distDir = Path.Combine(root, "dist");
            var jsonProjectionPath = Path.Combine(distDir, "content", "hello-world.json");
            var markdownProjectionPath = Path.Combine(distDir, "content", "hello-world.md");
            var agentManifestPath = Path.Combine(distDir, "agent-manifest.json");
            var publishAuditPath = Path.Combine(distDir, ".bukit", "publish-audit-report.json");

            Assert.True(File.Exists(jsonProjectionPath), $"Expected {jsonProjectionPath}");
            Assert.True(File.Exists(markdownProjectionPath), $"Expected {markdownProjectionPath}");
            Assert.True(File.Exists(agentManifestPath), $"Expected {agentManifestPath}");
            Assert.True(File.Exists(publishAuditPath), $"Expected {publishAuditPath}");

            using var projectionDoc = JsonDocument.Parse(File.ReadAllText(jsonProjectionPath));
            Assert.Equal("Hello World", projectionDoc.RootElement.GetProperty("title").GetString());
            Assert.Equal("Ali", projectionDoc.RootElement.GetProperty("author").GetString());
            Assert.Equal("approved", projectionDoc.RootElement.GetProperty("reviewStatus").GetString());
            Assert.Equal("notion", projectionDoc.RootElement.GetProperty("source").GetString());

            var markdownProjection = File.ReadAllText(markdownProjectionPath);
            Assert.Contains("# Hello World", markdownProjection, StringComparison.Ordinal);
            Assert.Contains("Review Status: approved", markdownProjection, StringComparison.Ordinal);

            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(agentManifestPath));
            var manifestDocuments = manifestDoc.RootElement.GetProperty("documents").EnumerateArray().ToArray();
            Assert.Contains(manifestDocuments, x =>
                (x.TryGetProperty("route", out var route) && route.GetString() == "/blog/hello-world/") ||
                (x.TryGetProperty("Route", out var routeUpper) && routeUpper.GetString() == "/blog/hello-world/"));
            var manifestDocument = manifestDocuments.Single(x => x.GetProperty("route").GetString() == "/blog/hello-world/");
            var manifestRepresentations = manifestDocument.GetProperty("representations").EnumerateArray().ToArray();
            foreach (var kind in PublishRepresentationRegistry.DocumentKinds(includeJsonLd: false))
            {
                var representation = manifestRepresentations.Single(x => x.GetProperty("kind").GetString() == kind);
                var representationUrl = representation.GetProperty("url").GetString();
                Assert.False(string.IsNullOrWhiteSpace(representationUrl));
                var expectedPath = kind is "html" or "semantic-html"
                    ? Path.Combine(distDir, representationUrl!.TrimStart('/'), "index.html")
                    : Path.Combine(distDir, representationUrl!.TrimStart('/'));
                Assert.True(File.Exists(expectedPath), $"Expected manifest {kind} representation to exist at {expectedPath}.");
            }

            using var publishAuditDoc = JsonDocument.Parse(File.ReadAllText(publishAuditPath));
            Assert.Equal("https://bukit.dev/schemas/publish-audit-report.v1.json", publishAuditDoc.RootElement.GetProperty("schema").GetString());
            var summary = publishAuditDoc.RootElement.GetProperty("summary");
            Assert.True(summary.GetProperty("publishIssueCount").GetInt32() >= 0);
            Assert.True(summary.GetProperty("machineReadabilityIssueCount").GetInt32() >= 0);
            Assert.True(summary.GetProperty("trustIssueCount").GetInt32() >= 0);
            Assert.True(summary.GetProperty("representationGapCount").GetInt32() >= 0);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_ThemeSource_UsesResolvedThemeLayoutsAndAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-theme-source-build-test", Guid.NewGuid().ToString("N"));

        try
        {
            var themeRoot = Path.Combine(root, ".cache", "themes", "local-theme");
            Directory.CreateDirectory(Path.Combine(themeRoot, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
            Directory.CreateDirectory(Path.Combine(themeRoot, "static"));

            File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), """
                name: local-theme
                version: 1.0.0
                """);
            File.WriteAllText(Path.Combine(themeRoot, "layouts", "pages", "page.html"), "<main>remote-page:{{ page.title }}</main>");
            File.WriteAllText(Path.Combine(themeRoot, "layouts", "pages", "index.html"), "<main>remote-index</main>");
            File.WriteAllText(Path.Combine(themeRoot, "layouts", "pages", "list.html"), "<main>remote-list</main>");
            File.WriteAllText(Path.Combine(themeRoot, "assets", "remote.css"), "body{color:green}");
            File.WriteAllText(Path.Combine(themeRoot, "static", "robots.txt"), "User-agent: *");

            var items = new[]
            {
                new ContentItem(
                    "about",
                    "About Remote Theme",
                    "about",
                    DateTimeOffset.Parse("2024-06-01T00:00:00Z"),
                    null,
                    new Dictionary<string, object> { ["type"] = "page", ["collection"] = "page", ["bodyFingerprint"] = "about-v1" },
                    BodyKey: "about")
            };
            var loadResult = new ContentLoadResult(items, new DictionaryContentBodyStore(new Dictionary<string, string>
            {
                ["about"] = "<p>Body</p>"
            }));

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test-site",
                    Title = "Test Site",
                    BaseUrl = "/",
                    Language = "en",
                    Collections = TestCollections()
                },
                Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Source = "local-theme" }
            };

            var logger = new TestLogger();
            var engine = new SiteEngine(logger, new StaticContentProviderFactory(loadResult), new DefaultSearchIndexBuilder());

            WriteTestThemeTemplates(root);

            await engine.BuildAsync(config, root, new ConfigOverrides { Incremental = false }, CancellationToken.None);

            var page = Path.Combine(root, "dist", "pages", "about", "index.html");
            Assert.True(File.Exists(page), $"Expected {page}");
            Assert.Contains("remote-page:About Remote Theme", File.ReadAllText(page));
            Assert.True(File.Exists(Path.Combine(root, "dist", "assets", "remote.css")));
            Assert.True(File.Exists(Path.Combine(root, "dist", "robots.txt")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_ReportDisabled_DoesNotWriteBuildReport()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "content", "hello.md"), """
                ---
                type: post
                collection: post
                title: Hello World
                slug: hello-world
                publishAt: 2024-06-01T00:00:00Z
                ---
                # Hello World
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html>
                <html><body>{{ content }}</body></html>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), """
                <h2>{{ page.title }}</h2>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), """
                <h2>{{ page.title }}</h2>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), """
                <h2>Home</h2>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), """
                <h2>List</h2>
                """);

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test-site",
                    Title = "Test Site",
                    BaseUrl = "/",
                    Language = "en",
                    Collections = TestCollections()
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
            };

            var engine = new SiteEngine(new TestLogger());
            WriteTestThemeTemplates(root);
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            Assert.False(File.Exists(Path.Combine(root, "dist", ".bukit", "build-report.json")));

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
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
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
                collection: post
                title: Hello World
                slug: hello-world
                publishAt: 2024-06-01T00:00:00Z
                update_time: 2024-06-02T00:00:00Z
                schema_type: BlogPosting
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
            WriteTestThemeTemplates(root);
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
                  feed:
                    formats: [rss, atom, json]
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
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
                collection: post
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
                collection: post
                title: Hidden Post
                slug: hidden
                publishAt: 2024-06-02T00:00:00Z
                robots: noindex,nofollow
                ---
                # Hidden
                """);

            File.WriteAllText(Path.Combine(root, "content", "expired.md"), """
                ---
                type: post
                collection: post
                title: Expired Post
                slug: expired
                publishAt: 2024-06-03T00:00:00Z
                expires_at: 2024-06-04T00:00:00Z
                ---
                # Expired
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
            WriteTestThemeTemplates(root);
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
            Assert.DoesNotContain("https://example.com/docs/blog/expired/", sitemap, StringComparison.Ordinal);

            var search = File.ReadAllText(Path.Combine(root, "dist", "search.json"));
            Assert.Contains("/docs/blog/visible/", search, StringComparison.Ordinal);
            Assert.DoesNotContain("/docs/blog/hidden/", search, StringComparison.Ordinal);
            Assert.DoesNotContain("/docs/blog/expired/", search, StringComparison.Ordinal);

            var rss = File.ReadAllText(Path.Combine(root, "dist", "rss.xml"));
            Assert.Contains("https://example.com/docs/blog/visible/", rss, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/docs/blog/hidden/", rss, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/docs/blog/expired/", rss, StringComparison.Ordinal);

            var atom = File.ReadAllText(Path.Combine(root, "dist", "feed", "atom.xml"));
            Assert.Contains("https://example.com/docs/blog/visible/", atom, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/docs/blog/hidden/", atom, StringComparison.Ordinal);
            Assert.DoesNotContain("https://example.com/docs/blog/expired/", atom, StringComparison.Ordinal);

            var manifest = File.ReadAllText(Path.Combine(root, "dist", "agent-manifest.json"));
            Assert.Contains("/docs/blog/visible/", manifest, StringComparison.Ordinal);
            Assert.DoesNotContain("/docs/blog/hidden/", manifest, StringComparison.Ordinal);
            Assert.DoesNotContain("/docs/blog/expired/", manifest, StringComparison.Ordinal);

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
            WriteTestThemeTemplates(root);
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
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
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
                collection: post
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

            WriteTestThemeTemplates(root);
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
    public async Task BuildAsync_CleanRefusesDirectoryWithoutBukitMarker()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-clean-marker-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(root, "dist"));
            File.WriteAllText(Path.Combine(root, "dist", "user-file.txt"), "keep");
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                collection: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", Language = "en", BaseUrl = "/", Collections = TestCollections() },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>

                new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("marker", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(root, "dist", "user-file.txt")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_MultiLanguageBuildRespectsGlobalConcurrencyBudget()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-i18n-concurrency", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");

            var items = Enumerable.Range(1, 6)
                .Select(i => new ContentItem(
                    $"item-{i}",
                    $"Item {i}",
                    $"item-{i}",
                    DateTimeOffset.UtcNow,
                    $"<p>Item {i}</p>",
                new Dictionary<string, object> { ["type"] = "page", ["collection"] = "page" }))
                .ToList();
            var bodyStore = new DictionaryContentBodyStore(items.ToDictionary(x => x.Id, x => x.ContentHtml ?? string.Empty, StringComparer.Ordinal));
            var concurrency = new RenderConcurrencyProbe();
            var engine = new SiteEngine(
                new TestLogger(),
                new StaticContentProviderFactory(new ContentLoadResult(items, bodyStore)),
                new DefaultSearchIndexBuilder(),
                _ => new ProbeTemplateRenderer(concurrency));
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "T",
                    Language = "en",
                    DefaultLanguage = "en",
                    Languages = new[] { "en", "fr", "de" },
                    BaseUrl = "/",
                    Collections = TestCollections()
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            await engine.BuildAsync(config, root, new ConfigOverrides { Jobs = 2 }, CancellationToken.None);

            Assert.True(concurrency.MaxObserved <= 2, $"Expected max concurrency <= 2, observed {concurrency.MaxObserved}.");
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_LanguageJobs_ProducesIdenticalOutputRegardlessOfConcurrency()
    {
        var root1 = Path.Combine(Path.GetTempPath(), "bukit-langjobs-1", Guid.NewGuid().ToString("N"));
        var root2 = Path.Combine(Path.GetTempPath(), "bukit-langjobs-3", Guid.NewGuid().ToString("N"));

        try
        {
            foreach (var root in new[] { root1, root2 })
            {
                Directory.CreateDirectory(Path.Combine(root, "content"));
                Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
                Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

                File.WriteAllText(Path.Combine(root, "content", "page.en.md"), """
                    ---
                    type: page
                    collection: page
                    title: Hello
                    slug: hello
                    language: en
                    i18nKey: hello
                    date: 2026-01-01
                    publishAt: 2026-01-01T00:00:00Z
                    ---
                    # Hello
                    """);
                File.WriteAllText(Path.Combine(root, "content", "page.fr.md"), """
                    ---
                    type: page
                    collection: page
                    title: Bonjour
                    slug: bonjour
                    language: fr
                    i18nKey: hello
                    publishAt: 2026-01-01T00:00:00Z
                    ---
                    # Bonjour
                    """);
                File.WriteAllText(Path.Combine(root, "content", "page.de.md"), """
                    ---
                    type: page
                    collection: page
                    title: Hallo
                    slug: hallo
                    language: de
                    i18nKey: hello
                    publishAt: 2026-01-01T00:00:00Z
                    ---
                    # Hallo
                    """);

                File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                    <!DOCTYPE html>
                    <html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>
                    """);
                File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
                File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{% layout \"layouts/base.html\" %}\nIndex");
                File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "{% layout \"layouts/base.html\" %}\nList");
            }

            var logger1 = new TestLogger();
            var config1 = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "T",
                    Language = "en",
                    Languages = new[] { "en", "fr", "de" },
                    DefaultLanguage = "en",
                    BaseUrl = "/",
                    Collections = TestCollections()
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true, LanguageJobs = 1 },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };
            WriteTestThemeTemplates(root1);
            await new SiteEngine(logger1).BuildAsync(config1, root1, new ConfigOverrides(), CancellationToken.None);

            var logger3 = new TestLogger();
            var config3 = config1 with { Build = config1.Build with { LanguageJobs = 3 } };
            WriteTestThemeTemplates(root2);
            await new SiteEngine(logger3).BuildAsync(config3, root2, new ConfigOverrides(), CancellationToken.None);

            Assert.Empty(logger1.Errors);
            Assert.Empty(logger3.Errors);

            var dist1 = Path.Combine(root1, "dist");
            var dist2 = Path.Combine(root2, "dist");
            Assert.True(Directory.Exists(dist1));
            Assert.True(Directory.Exists(dist2));

            var dirsMatch = DirectoriesMatch(dist1, dist2);
            Assert.True(dirsMatch, "Output directories should be identical regardless of languageJobs setting.");
        }
        finally
        {
            try { CleanupDir(root1); } catch { }
            try { CleanupDir(root2); } catch { }
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
                  collections:
                    page:
                      permalink: /pages/{slug}/
                      template: pages/page.html
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
                collection: page
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
                collection: page
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
                collection: page
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
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "taxonomy-index.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "taxonomy-term.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            var engine = new SiteEngine(logger);
            WriteTestThemeTemplates(root);
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
                  collections:
                    post:
                      permalink: /blog/{slug}/
                      template: pages/post.html
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
                collection: post
                title: This is a deliberately long SEO title that should be reported because it is over the normal search result length
                slug: visible
                summary: Visible post summary
                publishAt: 2024-01-01T00:00:00Z
                schema_type: BlogPosting
                ---
                # Visible
                """);
            File.WriteAllText(Path.Combine(root, "content", "hidden.md"), """
                ---
                type: post
                collection: post
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
            WriteTestThemeTemplates(root);
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var reportPath = Path.Combine(root, "dist", ".bukit", "seo-report.json");
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
                collection: page
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
                Site = new SiteConfig { Name = "t", Title = "T", BaseUrl = "/", Language = "en", Collections = TestCollections() },
                Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
                Build = new BuildConfig { Output = "dist", Clean = true },
            };

            var logger1 = new TestLogger();
            var engine1 = new SiteEngine(logger1);
            WriteTestThemeTemplates(root);
            await engine1.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            Assert.Empty(logger1.Errors);

            var logger2 = new TestLogger();
            var engine2 = new SiteEngine(logger2);
            WriteTestThemeTemplates(root);
            await engine2.BuildAsync(config, root, new ConfigOverrides { Clean = false }, CancellationToken.None);
            Assert.Empty(logger2.Errors);

            Assert.True(Directory.Exists(Path.Combine(root, ".cache")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalBuildDeletesRemovedPluginOutputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-plugin-output-delete", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                collection: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");

            var config = CreatePluginOutputConfig("success");
            WriteTestThemeTemplates(root);
            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides { AllowExternalPlugins = true }, CancellationToken.None);
            var pluginOutput = Path.Combine(root, "dist", "plugin-output.json");
            Assert.True(File.Exists(pluginOutput));

            var incrementalConfig = CreatePluginOutputConfig("no-output") with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false, AllowExternalPlugins = true }, CancellationToken.None);

            Assert.False(File.Exists(pluginOutput));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalBuildDeletesRemovedStaticFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-static-delete", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(root, "static"));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                collection: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");
            File.WriteAllText(Path.Combine(root, "static", "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(root, "static", "removed.txt"), "remove");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", BaseUrl = "/", Language = "en", Collections = TestCollections() },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts", Static = "static" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            var removedOutput = Path.Combine(root, "dist", "removed.txt");
            Assert.True(File.Exists(Path.Combine(root, "dist", "keep.txt")));
            Assert.True(File.Exists(removedOutput));

            File.Delete(Path.Combine(root, "static", "removed.txt"));
            var incrementalConfig = config with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false }, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(root, "dist", "keep.txt")));
            Assert.False(File.Exists(removedOutput));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalBuildDeletesRemovedThemeAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-asset-delete", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                collection: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");
            File.WriteAllText(Path.Combine(root, "assets", "keep.css"), "keep");
            File.WriteAllText(Path.Combine(root, "assets", "removed.css"), "remove");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", BaseUrl = "/", Language = "en", Collections = TestCollections() },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts", Assets = "assets" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            var removedOutput = Path.Combine(root, "dist", "assets", "removed.css");
            Assert.True(File.Exists(Path.Combine(root, "dist", "assets", "keep.css")));
            Assert.True(File.Exists(removedOutput));

            File.Delete(Path.Combine(root, "assets", "removed.css"));
            var incrementalConfig = config with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false }, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(root, "dist", "assets", "keep.css")));
            Assert.False(File.Exists(removedOutput));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalRerendersWhenParentLayoutChanges()
    {
        var root = CreateThemeInheritanceSite(createChildAssets: false, createChildStatic: false);
        try
        {
            File.WriteAllText(Path.Combine(root, "themes", "parent", "layouts", "pages", "page.html"), "Before {{ page.title }}");
            var config = CreateThemeInheritanceConfig();

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            var outputPath = Path.Combine(root, "dist", "pages", "hello", "index.html");
            Assert.Contains("Before", File.ReadAllText(outputPath));

            File.WriteAllText(Path.Combine(root, "themes", "parent", "layouts", "pages", "page.html"), "After {{ page.title }}");
            var incrementalConfig = config with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false }, CancellationToken.None);

            Assert.Contains("After", File.ReadAllText(outputPath));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalRerendersWhenUserLayoutOverrideChanges()
    {
        var root = CreateThemeInheritanceSite(createChildAssets: false, createChildStatic: false);
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "Before {{ page.title }}");
            var config = CreateThemeInheritanceConfig();

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            var outputPath = Path.Combine(root, "dist", "pages", "hello", "index.html");
            Assert.Contains("Before", File.ReadAllText(outputPath));

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "After {{ page.title }}");
            var incrementalConfig = config with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false }, CancellationToken.None);

            Assert.Contains("After", File.ReadAllText(outputPath));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalBuildRerendersWhenPartialChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-partial-change", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "partials"));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                collection: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ include 'partials/badge.html' }} {{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");
            File.WriteAllText(Path.Combine(root, "layouts", "partials", "badge.html"), "Before");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", BaseUrl = "/", Language = "en", Collections = TestCollections() },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            var outputPath = Path.Combine(root, "dist", "pages", "a", "index.html");
            Assert.Contains("Before", File.ReadAllText(outputPath));

            File.WriteAllText(Path.Combine(root, "layouts", "partials", "badge.html"), "After");
            var incrementalConfig = config with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false }, CancellationToken.None);

            Assert.Contains("After", File.ReadAllText(outputPath));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalBuildDeletesRemovedPages()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-incr-delete", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: post
                collection: post
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "content", "b.md"), """
                ---
                type: post
                collection: post
                title: B
                slug: b
                ---
                # B
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "T",
                    BaseUrl = "/",
                    Language = "en",
                    Collections = new Dictionary<string, CollectionConfig>
                    {
                        ["post"] = new()
                        {
                            Permalink = "/blog/{slug}/",
                            Template = "pages/post.html"
                        }
                    }
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            var removedOutput = Path.Combine(root, "dist", "blog", "b", "index.html");
            Assert.True(File.Exists(removedOutput));

            File.Delete(Path.Combine(root, "content", "b.md"));
            var incrementalConfig = config with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false }, CancellationToken.None);

            Assert.False(File.Exists(removedOutput));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_IncrementalBuildDeletesRemovedMediaFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-media-delete", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, ".cache", "media", "posts", "2026"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                collection: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, ".cache", "media", "cover.png"), "cover");
            File.WriteAllText(Path.Combine(root, ".cache", "media", "posts", "2026", "article-cover.png"), "article");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", BaseUrl = "/", Language = "en", Collections = TestCollections() },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);
            var removedOutput = Path.Combine(root, "dist", "assets", "uploads", "posts", "2026", "article-cover.png");
            Assert.True(File.Exists(Path.Combine(root, "dist", "assets", "uploads", "cover.png")));
            Assert.True(File.Exists(removedOutput));

            File.Delete(Path.Combine(root, ".cache", "media", "posts", "2026", "article-cover.png"));
            var incrementalConfig = config with { Build = config.Build with { Clean = false } };

            await new SiteEngine(new TestLogger()).BuildAsync(incrementalConfig, root, new ConfigOverrides { Clean = false }, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(root, "dist", "assets", "uploads", "cover.png")));
            Assert.False(File.Exists(removedOutput));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_ContentSourceCollectionMappings_RenderCustomRoutesAndListTemplates()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "companies"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "companies", "one.md"), """
                ---
                title: Company One
                slug: company-one
                publishAt: 2024-06-01T00:00:00Z
                ---
                # Company One
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "company.html"), "{{ page.title }} {{ page.url }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "Default list");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "company-list.html"), "Companies {{ for p in pages }}{{ p.url }} {{ end }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "china-list.html"), "China {{ for p in pages }}{{ p.url }} {{ end }}");

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "T",
                    Collections = new Dictionary<string, CollectionConfig>
                    {
                        ["companies"] = new()
                        {
                            Permalink = "/companies/{slug}/",
                            Template = "pages/company.html",
                            ListRoute = "/companies/",
                            ListTemplate = "pages/company-list.html"
                        },
                        ["china_companies"] = new()
                        {
                            Permalink = "/china-companies/{slug}/",
                            Template = "pages/company.html",
                            ListRoute = "/china-companies/",
                            ListTemplate = "pages/china-list.html"
                        }
                    }
                },
                Content = new ContentConfig
                {
                    Provider = "sources",
                    Sources = new[]
                    {
                        new ContentSourceConfig
                        {
                            Type = "markdown",
                            Name = "companies",
                            Collection = "companies",
                            AddToCollections = new[] { "china_companies" },
                            Markdown = new MarkdownConfig { Dir = "companies" }
                        }
                    },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(root, "dist", "companies", "company-one", "index.html")));
            Assert.True(File.Exists(Path.Combine(root, "dist", "china-companies", "company-one", "index.html")));
            Assert.Contains("Companies /companies/company-one/", File.ReadAllText(Path.Combine(root, "dist", "companies", "index.html")), StringComparison.Ordinal);
            Assert.Contains("China /china-companies/company-one/", File.ReadAllText(Path.Combine(root, "dist", "china-companies", "index.html")), StringComparison.Ordinal);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_DataSource_InjectsSiteDataBySourceNameAndKeepsModules()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "data"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "data", "menu.md"), """
                ---
                type: navigation
                title: Main Menu
                slug: main-menu
                label: Home
                order: 1
                ---
                ignored body
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), """
                data={{ site.data.menu[0].fields.label.value }}
                module={{ site.modules.navigation[0].fields.label.value }}
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", Collections = TestCollections() },
                Content = new ContentConfig
                {
                    Provider = "sources",
                    Sources = new[]
                    {
                        new ContentSourceConfig
                        {
                            Type = "markdown",
                            Name = "menu",
                            Mode = "data",
                            Markdown = new MarkdownConfig { Dir = "data" }
                        }
                    },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var html = File.ReadAllText(Path.Combine(root, "dist", "index.html"));
            Assert.Contains("data=Home", html, StringComparison.Ordinal);
            Assert.Contains("module=Home", html, StringComparison.Ordinal);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_DuplicateContentRouteUrl_FailsBeforeRendering()
    {
        var root = CreateRouteConflictSite();
        try
        {
            File.WriteAllText(Path.Combine(root, "content", "one.md"), """
                ---
                type: post
                collection: post
                title: One
                slug: same
                ---
                # One
                """);
            File.WriteAllText(Path.Combine(root, "content", "two.md"), """
                ---
                type: post
                collection: post
                title: Two
                slug: same
                ---
                # Two
                """);

            WriteTestThemeTemplates(root);

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>

                new SiteEngine(new TestLogger()).BuildAsync(CreateRouteConflictConfig(), root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("Route conflict on url", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/blog/same", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_DuplicateContentRouteOutputPath_FailsBeforeRendering()
    {
        var root = CreateRouteConflictSite();
        try
        {
            File.WriteAllText(Path.Combine(root, "content", "one.md"), """
                ---
                type: page
                collection: page
                title: One
                slug: one
                route:
                  url: /one/
                  outputPath: shared/index.html
                  template: pages/page.html
                ---
                # One
                """);
            File.WriteAllText(Path.Combine(root, "content", "two.md"), """
                ---
                type: page
                collection: page
                title: Two
                slug: two
                route:
                  url: /two/
                  outputPath: shared/index.html
                  template: pages/page.html
                ---
                # Two
                """);

            WriteTestThemeTemplates(root);

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>

                new SiteEngine(new TestLogger()).BuildAsync(CreateRouteConflictConfig(), root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("Route conflict on outputPath", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("shared/index.html", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_StaticHtmlRouteConflictsWithContentRoute_FailsBeforeWrite()
    {
        var root = CreateRouteConflictSite();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "static"));
            File.WriteAllText(Path.Combine(root, "content", "about.md"), """
                ---
                type: page
                collection: page
                title: About Content
                slug: about-content
                route:
                  url: /about/
                  outputPath: content-about/index.html
                  template: pages/page.html
                ---
                # About Content
                """);
            File.WriteAllText(Path.Combine(root, "static", "about.html"), "<main>Static About</main>");

            WriteTestThemeTemplates(root);

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>

                new SiteEngine(new TestLogger()).BuildAsync(CreateRouteConflictConfig(), root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("Route conflict on url", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("static", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("content", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/about/", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_StaticHtmlOutputPathConflictsWithContentRoute_FailsWithBothSources()
    {
        var root = CreateRouteConflictSite();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "static"));
            File.WriteAllText(Path.Combine(root, "content", "about.md"), """
                ---
                type: page
                collection: page
                title: About Content
                slug: about-content
                route:
                  url: /content-about/
                  outputPath: about/index.html
                  template: pages/page.html
                ---
                # About Content
                """);
            File.WriteAllText(Path.Combine(root, "static", "about.html"), "<main>Static About</main>");

            WriteTestThemeTemplates(root);

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>

                new SiteEngine(new TestLogger()).BuildAsync(CreateRouteConflictConfig(), root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("Route conflict on outputPath", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("static", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("content", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/about/", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("about/index.html", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_ChildThemeAssetsOverrideParentThemeAssets()
    {
        var root = CreateThemeInheritanceSite(createChildAssets: true, createChildStatic: false);
        try
        {
            File.WriteAllText(Path.Combine(root, "themes", "parent", "assets", "main.css"), "parent");
            File.WriteAllText(Path.Combine(root, "themes", "child", "assets", "main.css"), "child");

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(CreateThemeInheritanceConfig(), root, new ConfigOverrides(), CancellationToken.None);

            Assert.Equal("child", File.ReadAllText(Path.Combine(root, "dist", "assets", "main.css")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_ChildThemeStaticOverrideParentThemeStatic()
    {
        var root = CreateThemeInheritanceSite(createChildAssets: false, createChildStatic: true);
        try
        {
            File.WriteAllText(Path.Combine(root, "themes", "parent", "static", "robots.txt"), "parent");
            File.WriteAllText(Path.Combine(root, "themes", "child", "static", "robots.txt"), "child");

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(CreateThemeInheritanceConfig(), root, new ConfigOverrides(), CancellationToken.None);

            Assert.Equal("child", File.ReadAllText(Path.Combine(root, "dist", "robots.txt")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_ParentThemeAssetsAreCopiedWhenChildHasNoAssets()
    {
        var root = CreateThemeInheritanceSite(createChildAssets: false, createChildStatic: false);
        try
        {
            File.WriteAllText(Path.Combine(root, "themes", "parent", "assets", "main.css"), "parent");

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(CreateThemeInheritanceConfig(), root, new ConfigOverrides(), CancellationToken.None);

            Assert.Equal("parent", File.ReadAllText(Path.Combine(root, "dist", "assets", "main.css")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_DerivedPageContentConflict_FailsWithFailPolicy()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");

            for (var i = 1; i <= 3; i++)
            {
                File.WriteAllText(Path.Combine(root, "content", $"post{i}.md"), $$"""
                    ---
                    type: post
                    collection: post
                    title: Post {{i}}
                    slug: post-{{i}}
                    publishAt: 2024-06-0{{i}}:00:00:00Z
                    ---
                    # Post {{i}}
                    """);
            }

            File.WriteAllText(Path.Combine(root, "content", "conflict.md"), """
                ---
                type: page
                collection: page
                title: Conflict Page
                slug: conflict-page
                url: /blog/page/2/
                ---
                # Conflict
                """);

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "T",
                    Collections = new Dictionary<string, CollectionConfig>
                    {
                        ["post"] = new()
                        {
                            Permalink = "/blog/{slug}/",
                            Template = "pages/post.html",
                            ListRoute = "/blog/",
                            Pagination = new CollectionPaginationConfig { Enabled = true, PageSize = 2 }
                        },
                        ["page"] = new()
                        {
                            Permalink = "/pages/{slug}/",
                            Template = "pages/page.html"
                        }
                    }
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

                new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("route conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pagination", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_PaginationWithListTemplate_RendersBoth()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-integration-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{{ page.title }} {{ page.url }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "Page: {{ page.title }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "Blog List");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "pagination.html"),
                "Page {{ pagination.page }} of {{ pagination.total_pages }}");

            for (var i = 1; i <= 5; i++)
            {
                File.WriteAllText(Path.Combine(root, "content", $"post{i}.md"), $$"""
                    ---
                    type: post
                    collection: post
                    title: Post {{i}}
                    slug: post-{{i}}
                    publishAt: 2024-06-0{{i}}:00:00:00Z
                    ---
                    # Post {{i}}
                    """);
            }

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "T",
                    Collections = new Dictionary<string, CollectionConfig>
                    {
                        ["post"] = new()
                        {
                            Permalink = "/blog/{slug}/",
                            Template = "pages/post.html",
                            ListRoute = "/blog/",
                            ListTemplate = "pages/list.html",
                            Pagination = new CollectionPaginationConfig { Enabled = true, PageSize = 2 }
                        }
                    }
                },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            WriteTestThemeTemplates(root);

            await new SiteEngine(new TestLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(root, "dist", "blog", "index.html")));
            var listContent = File.ReadAllText(Path.Combine(root, "dist", "blog", "index.html"));
            Assert.Contains("Blog List", listContent, StringComparison.Ordinal);

            Assert.True(File.Exists(Path.Combine(root, "dist", "blog", "page", "2", "index.html")));
            Assert.True(File.Exists(Path.Combine(root, "dist", "blog", "page", "3", "index.html")));

            Assert.True(File.Exists(Path.Combine(root, "dist", "blog", "post-1", "index.html")));
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_DefaultLanguageFilter_OrphanContentNotInDefaultOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-i18n-orphan-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: i18n-orphan
                  title: I18n Orphan
                  url: https://example.com
                  baseUrl: /
                  language: en
                  languages: [en, zh]
                  defaultLanguage: en
                  sitemapMode: merged
                  searchMode: merged
                  collections:
                    page:
                      permalink: /pages/{slug}/
                      template: pages/page.html
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
            File.WriteAllText(Path.Combine(root, "content", "about.en.md"), """
                ---
                type: page
                collection: page
                title: About
                slug: about
                language: en
                i18nKey: about
                ---
                # About
                """);
            File.WriteAllText(Path.Combine(root, "content", "about.zh.md"), """
                ---
                type: page
                collection: page
                title: 关于
                slug: guanyu
                language: zh
                i18nKey: about
                ---
                # 关于
                """);
            File.WriteAllText(Path.Combine(root, "content", "solo.en.md"), """
                ---
                type: page
                collection: page
                title: Solo
                slug: solo
                language: en
                ---
                # Solo
                """);
            // zh-only orphan content — no i18nKey, only zh language
            File.WriteAllText(Path.Combine(root, "content", "orphan.zh.md"), """
                ---
                type: page
                collection: page
                title: 孤儿
                slug: orphan
                language: zh
                ---
                # 孤儿
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
            WriteTestThemeTemplates(root);
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            // en output should contain about, solo — NOT orphan (zh-only)
            var enPagesDir = Path.Combine(root, "dist", "en", "pages");
            Assert.True(Directory.Exists(enPagesDir), "en output directory should exist");
            Assert.True(Directory.Exists(Path.Combine(enPagesDir, "about")), "about (en) should exist in en output");
            Assert.True(Directory.Exists(Path.Combine(enPagesDir, "solo")), "solo (en) should exist in en output");
            Assert.False(Directory.Exists(Path.Combine(enPagesDir, "orphan")), "orphan (zh-only) should NOT exist in en output");

            // zh output should contain about (zh variant) and orphan — NOT solo (en-only)
            var zhPagesDir = Path.Combine(root, "dist", "zh", "pages");
            Assert.True(Directory.Exists(zhPagesDir), "zh output directory should exist");
            Assert.True(Directory.Exists(Path.Combine(zhPagesDir, "guanyu")), "guanyu (zh) should exist in zh output");
            Assert.True(Directory.Exists(Path.Combine(zhPagesDir, "orphan")), "orphan (zh) should exist in zh output");
            Assert.False(Directory.Exists(Path.Combine(zhPagesDir, "solo")), "solo (en-only) should NOT exist in zh output");

            // Merged sitemap should include all indexable routes
            var sitemapPath = Path.Combine(root, "dist", "sitemap.xml");
            Assert.True(File.Exists(sitemapPath), "merged sitemap should exist");
            var sitemap = File.ReadAllText(sitemapPath);
            Assert.Contains("hreflang=\"en\"", sitemap);
            Assert.Contains("hreflang=\"zh\"", sitemap);

            // Merged search index should exist
            var searchPath = Path.Combine(root, "dist", "search.json");
            Assert.True(File.Exists(searchPath), "merged search index should exist");

            Assert.Empty(logger.Errors);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_SearchMergedI18n_NoLanguageCrossContamination()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-search-i18n-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: i18n-search
                  title: I18n Search
                  url: https://example.com
                  baseUrl: /
                  language: en
                  languages: [en, zh]
                  defaultLanguage: en
                  sitemapMode: merged
                  searchMode: merged
                  collections:
                    page:
                      permalink: /pages/{slug}/
                      template: pages/page.html
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
            File.WriteAllText(Path.Combine(root, "content", "alpha.en.md"), """
                ---
                type: page
                collection: page
                title: Alpha
                slug: alpha
                language: en
                i18nKey: alpha
                ---
                # Alpha EN
                """);
            File.WriteAllText(Path.Combine(root, "content", "alpha.zh.md"), """
                ---
                type: page
                collection: page
                title: 阿尔法
                slug: a-er-fa
                language: zh
                i18nKey: alpha
                ---
                # 阿尔法 ZH
                """);
            // zh-only content
            File.WriteAllText(Path.Combine(root, "content", "beta.zh.md"), """
                ---
                type: page
                collection: page
                title: 贝塔
                slug: beta
                language: zh
                ---
                # 贝塔 ZH
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
            WriteTestThemeTemplates(root);
            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            // Verify en output
            var enPagesDir = Path.Combine(root, "dist", "en", "pages");
            Assert.True(Directory.Exists(Path.Combine(enPagesDir, "alpha")), "alpha (en) should exist in en output");
            Assert.False(Directory.Exists(Path.Combine(enPagesDir, "a-er-fa")), "a-er-fa (zh variant) should NOT exist in en output");
            Assert.False(Directory.Exists(Path.Combine(enPagesDir, "beta")), "beta (zh-only) should NOT exist in en output");

            // Verify zh output
            var zhPagesDir = Path.Combine(root, "dist", "zh", "pages");
            Assert.True(Directory.Exists(Path.Combine(zhPagesDir, "a-er-fa")), "a-er-fa (zh) should exist in zh output");
            Assert.True(Directory.Exists(Path.Combine(zhPagesDir, "beta")), "beta (zh) should exist in zh output");
            Assert.False(Directory.Exists(Path.Combine(zhPagesDir, "alpha")), "alpha (en) should NOT exist in zh output");

            // Merged search index should contain all pages from both languages
            var searchPath = Path.Combine(root, "dist", "search.json");
            Assert.True(File.Exists(searchPath), "merged search index should exist");
            using var doc = JsonDocument.Parse(File.ReadAllText(searchPath));
            var entries = doc.RootElement.EnumerateArray().ToArray();
            Assert.Contains(entries, e => e.GetProperty("url").GetString() == "/en/pages/alpha/");
            Assert.Contains(entries, e => e.GetProperty("url").GetString() == "/zh/pages/a-er-fa/");
            Assert.Contains(entries, e => e.GetProperty("url").GetString() == "/zh/pages/beta/");
            Assert.DoesNotContain(entries, e => e.GetProperty("url").GetString() == "/en/pages/beta/");

            // Merged sitemap should contain all routes
            var sitemapPath = Path.Combine(root, "dist", "sitemap.xml");
            Assert.True(File.Exists(sitemapPath), "merged sitemap should exist");

            Assert.Empty(logger.Errors);
        }
        finally
        {
            try { CleanupDir(root); } catch { }
        }
    }

    [Fact]
    public async Task BuildAsync_PaginationI18n_EachLanguageHasOnlyItsOwnContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-pagination-i18n-test", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));

            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: pagination-i18n
                  title: Pagination I18n
                  url: https://example.com
                  baseUrl: /
                  language: en
                  languages: [en, zh]
                  defaultLanguage: en
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

            File.WriteAllText(Path.Combine(root, "content", "a.en.md"), """
                ---
                type: post
                collection: post
                title: Alpha
                slug: a
                language: en
                publishAt: 2024-01-01T00:00:00Z
                ---
                # Alpha EN
                """);
            File.WriteAllText(Path.Combine(root, "content", "b.en.md"), """
                ---
                type: post
                collection: post
                title: Beta
                slug: b
                language: en
                publishAt: 2024-02-01T00:00:00Z
                ---
                # Beta EN
                """);
            File.WriteAllText(Path.Combine(root, "content", "c.zh.md"), """
                ---
                type: post
                collection: post
                title: 查理
                slug: c
                language: zh
                publishAt: 2024-01-15T00:00:00Z
                ---
                # 查理 ZH
                """);
            File.WriteAllText(Path.Combine(root, "content", "d.zh.md"), """
                ---
                type: post
                collection: post
                title: 德尔塔
                slug: d
                language: zh
                publishAt: 2024-02-15T00:00:00Z
                ---
                # 德尔塔 ZH
                """);

            File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), """
                <!DOCTYPE html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{% layout \"layouts/base.html\" %}\n{{ page.content }}");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{% layout \"layouts/base.html\" %}\nIndex");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "{% layout \"layouts/base.html\" %}\nList");
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "pagination.html"),
                "{% layout \"layouts/base.html\" %}\nPage {{ pagination.page }}");

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var configWithPagination = config with
            {
                Site = config.Site with
                {
                    Collections = new Dictionary<string, CollectionConfig>
                    {
                        ["post"] = new()
                        {
                            Permalink = "/blog/{slug}/",
                            Template = "pages/post.html",
                            ListRoute = "/blog/",
                            ListTemplate = "pages/list.html",
                            Pagination = new CollectionPaginationConfig { Enabled = true, PageSize = 1 }
                        }
                    }
                }
            };

            var engine = new SiteEngine(new TestLogger());
            WriteTestThemeTemplates(root);
            await engine.BuildAsync(configWithPagination, root, new ConfigOverrides(), CancellationToken.None);

            // en pagination page 2 should exist (2 en posts with pageSize 1)
            var enPage2 = Path.Combine(root, "dist", "en", "blog", "page", "2", "index.html");
            Assert.True(File.Exists(enPage2), "en pagination page 2 should exist");

            // zh pagination page 2 should exist (2 zh posts with pageSize 1)
            var zhPage2 = Path.Combine(root, "dist", "zh", "blog", "page", "2", "index.html");
            Assert.True(File.Exists(zhPage2), "zh pagination page 2 should exist");

            // en pagination page 3 should NOT exist (only 2 en posts)
            var enPage3 = Path.Combine(root, "dist", "en", "blog", "page", "3", "index.html");
            Assert.False(File.Exists(enPage3), "en pagination page 3 should NOT exist");

            // zh pagination page 3 should NOT exist (only 2 zh posts)
            var zhPage3 = Path.Combine(root, "dist", "zh", "blog", "page", "3", "index.html");
            Assert.False(File.Exists(zhPage3), "zh pagination page 3 should NOT exist");

            // en posts exist in en output
            Assert.True(File.Exists(Path.Combine(root, "dist", "en", "blog", "a", "index.html")), "en post a should exist");
            Assert.True(File.Exists(Path.Combine(root, "dist", "en", "blog", "b", "index.html")), "en post b should exist");

            // zh posts exist in zh output
            Assert.True(File.Exists(Path.Combine(root, "dist", "zh", "blog", "c", "index.html")), "zh post c should exist");
            Assert.True(File.Exists(Path.Combine(root, "dist", "zh", "blog", "d", "index.html")), "zh post d should exist");

            // en posts do NOT leak into zh output
            Assert.False(File.Exists(Path.Combine(root, "dist", "zh", "blog", "a", "index.html")), "en post a should NOT be in zh output");
            Assert.False(File.Exists(Path.Combine(root, "dist", "zh", "blog", "b", "index.html")), "en post b should NOT be in zh output");

            // zh posts do NOT leak into en output
            Assert.False(File.Exists(Path.Combine(root, "dist", "en", "blog", "c", "index.html")), "zh post c should NOT be in en output");
            Assert.False(File.Exists(Path.Combine(root, "dist", "en", "blog", "d", "index.html")), "zh post d should NOT be in en output");
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

    private static string CreateThemeInheritanceSite(bool createChildAssets, bool createChildStatic)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-theme-inheritance-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "content"));
        Directory.CreateDirectory(Path.Combine(root, "themes", "parent", "assets"));
        Directory.CreateDirectory(Path.Combine(root, "themes", "parent", "static"));
        Directory.CreateDirectory(Path.Combine(root, "themes", "parent", "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(root, "themes", "child", "layouts", "pages"));
        if (createChildAssets)
        {
            Directory.CreateDirectory(Path.Combine(root, "themes", "child", "assets"));
        }

        if (createChildStatic)
        {
            Directory.CreateDirectory(Path.Combine(root, "themes", "child", "static"));
        }

        File.WriteAllText(Path.Combine(root, "content", "hello.md"), """
            ---
            type: page
            collection: page
            title: Hello
            slug: hello
            ---
            # Hello
            """);
        File.WriteAllText(Path.Combine(root, "themes", "parent", "layouts", "pages", "page.html"), "{{ page.title }}");
        File.WriteAllText(Path.Combine(root, "themes", "parent", "layouts", "pages", "index.html"), "Index");
        File.WriteAllText(Path.Combine(root, "themes", "parent", "layouts", "pages", "list.html"), "List");
        File.WriteAllText(Path.Combine(root, "themes", "child", "theme.yaml"), """
            name: child
            extends: parent
            """);
        return root;
    }

    private static AppConfig CreatePluginOutputConfig(string mode)
        => new()
        {
            Site = new SiteConfig
            {
                Name = "t",
                Title = "T",
                BaseUrl = "/",
                Language = "en",
                Collections = TestCollections(),
                PluginFailMode = "strict",
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                        AllowAbsoluteEntry = true,
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000,
                        Options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["positionals"] = new object[] { Path.Combine(AppContext.BaseDirectory, "ProtocolEchoPlugin.dll"), mode }
                            }
                        }
                    }
                }
            },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig { Dir = "content" },
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig { Output = "dist", Clean = true },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };

    private static AppConfig CreateThemeInheritanceConfig()
        => new()
        {
            Site = new SiteConfig { Name = "t", Title = "T", Collections = TestCollections() },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig { Dir = "content" },
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig { Output = "dist", Clean = true },
            Theme = new ThemeConfig { Name = "child", Extends = "parent" }
        };

    private static string CreateRouteConflictSite()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-route-conflict-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "content"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "{{ page.title }}");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "Index");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"), "List");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "static.html"), "{{ page.content }}");
        return root;
    }

    private static AppConfig CreateRouteConflictConfig()
        => new()
        {
            Site = new SiteConfig { Name = "t", Title = "T", Collections = TestCollections() },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig { Dir = "content" },
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig { Output = "dist", Clean = true },
            Theme = new ThemeConfig { Layouts = "layouts", StaticTemplate = "pages/static.html" }
        };

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

    private static IReadOnlyDictionary<string, CollectionConfig> TestCollections()
        => new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new()
            {
                Permalink = "/blog/{slug}/",
                Template = "pages/post.html",
                ListRoute = "/blog/",
                ListTemplate = "pages/list.html"
            },
            ["page"] = new()
            {
                Permalink = "/pages/{slug}/",
                Template = "pages/page.html"
            }
        };

    private static void WriteTestThemeTemplates(string root)
    {
        var localLayouts = Path.Combine(root, "layouts");
        if (Directory.Exists(localLayouts))
        {
            EnsureThemeYaml(localLayouts, "test");
        }

        foreach (var themesDir in new[] { Path.Combine(root, "themes"), Path.Combine(root, ".cache", "themes") })
        {
            if (!Directory.Exists(themesDir))
            {
                continue;
            }

            foreach (var themeRoot in Directory.GetDirectories(themesDir))
            {
                if (Directory.Exists(Path.Combine(themeRoot, "layouts")))
                {
                    EnsureThemeYaml(themeRoot, Path.GetFileName(themeRoot));
                }
            }
        }

        static void EnsureThemeYaml(string themeRoot, string name)
        {
            var pagesDir = Directory.Exists(Path.Combine(themeRoot, "layouts", "pages"))
                ? Path.Combine(themeRoot, "layouts", "pages")
                : Path.Combine(themeRoot, "pages");
            if (Directory.Exists(pagesDir))
            {
                EnsureFile(Path.Combine(pagesDir, "taxonomy-index.html"), "{{ page.content }}");
                EnsureFile(Path.Combine(pagesDir, "taxonomy-term.html"), "{{ page.content }}");
                EnsureFile(Path.Combine(pagesDir, "pagination.html"), "{{ page.content }}");
                EnsureFile(Path.Combine(pagesDir, "search.html"), "{{ page.title }}");
            }

            var path = Path.Combine(themeRoot, "theme.yaml");
            var existing = File.Exists(path) ? File.ReadAllText(path) : $"name: {name}\nversion: 1.0\n";
            if (existing.Contains("templates:", StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(path, existing.TrimEnd() + """

templates:
  home:
    template: pages/index.html
    required: true
  post:
    template: pages/post.html
    accepts:
      type: post
      collection: post
  page:
    template: pages/page.html
    accepts:
      type: page
      collection: page
  detail:
    template: pages/page.html
    accepts:
      kind: detail
  list:
    template: pages/list.html
    accepts:
      kind: list
  pagination:
    template: pages/pagination.html
    accepts:
      kind: pagination
  archive:
    template: pages/page.html
    accepts:
      kind: archive
  taxonomy_index:
    template: pages/taxonomy-index.html
    accepts:
      kind: taxonomy_index
  taxonomy_term:
    template: pages/taxonomy-term.html
    accepts:
      kind: taxonomy_term
  search:
    template: pages/search.html
    accepts:
      kind: search
""" + Environment.NewLine);
        }

        static void EnsureFile(string path, string content)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content);
            }
        }
    }

    private static bool DirectoriesMatch(string dir1, string dir2)
    {
        static bool ShouldIgnore(string relativePath)
        {
            var fileName = Path.GetFileName(relativePath);
            if (fileName is ".bukit-build-state.json" or ".bukit-output-marker") return true;
            if (relativePath.Split('/', '\\').Any(s => s == ".bukit")) return true;
            return false;
        }

        var files1 = Directory.GetFiles(dir1, "*", SearchOption.AllDirectories)
            .Select(f => (Relative: Path.GetRelativePath(dir1, f), Absolute: f, Content: File.ReadAllText(f)))
            .Where(f => !ShouldIgnore(f.Relative))
            .OrderBy(x => x.Relative, StringComparer.Ordinal)
            .ToList();

        var files2 = Directory.GetFiles(dir2, "*", SearchOption.AllDirectories)
            .Select(f => (Relative: Path.GetRelativePath(dir2, f), Absolute: f, Content: File.ReadAllText(f)))
            .Where(f => !ShouldIgnore(f.Relative))
            .OrderBy(x => x.Relative, StringComparer.Ordinal)
            .ToList();

        var rel1 = files1.Select(f => f.Relative).ToHashSet(StringComparer.Ordinal);
        var rel2 = files2.Select(f => f.Relative).ToHashSet(StringComparer.Ordinal);

        if (!rel1.SetEquals(rel2))
        {
            var onlyIn1 = rel1.Except(rel2).ToList();
            var onlyIn2 = rel2.Except(rel1).ToList();
            var diffs = new List<string>();
            if (onlyIn1.Count > 0) diffs.Add($"Only in dir1: [{string.Join(", ", onlyIn1)}]");
            if (onlyIn2.Count > 0) diffs.Add($"Only in dir2: [{string.Join(", ", onlyIn2)}]");
            Assert.Fail($"File sets differ: {string.Join("; ", diffs)}");
            return false;
        }

        for (var i = 0; i < files1.Count; i++)
        {
            if (!string.Equals(files1[i].Content, files2[i].Content, StringComparison.Ordinal))
            {
                Assert.Fail($"Content differs in file '{files1[i].Relative}'." +
                    $"\n  dir1 ({files1[i].Absolute}): [{files1[i].Content.Substring(0, Math.Min(500, files1[i].Content.Length))}]" +
                    $"\n  dir2 ({files2[i].Absolute}): [{files2[i].Content.Substring(0, Math.Min(500, files2[i].Content.Length))}]");
                return false;
            }
        }

        return true;
    }
}
