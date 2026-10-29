using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Shared;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed partial class SiteEngineIntegrationTests
{
    [Theory]
    [InlineData("search")]
    [InlineData("markdown")]
    [InlineData("llms-full")]
    public async Task BodyProjection_SeparateHiddenBlocksPreserveVisibleTextInPublicOutputs(string representation)
    {
        var (root, initial) = CreateMediaSite();
        var config = initial with
        {
            Site = initial.Site with { Seo = new SeoConfig { Geo = new SeoGeoConfig { LlmsFullTxt = true } } },
            Content = initial.Content with { Media = initial.Content.Media with { DownloadToLocal = false } }
        };
        const string html = "<script>script-hidden-one</ script><a href='/private-script'>script-hidden-link</a>script-hidden-middle</script>visible-between-script<script>script-hidden-two</script>visible-after-script" +
            "<style>style-hidden-one</style\u00a0><a href='/private-style'>style-hidden-link</a>style-hidden-middle</style>visible-between-style<style>style-hidden-two</style>visible-after-style" +
            "<a href='/public'>visible-link</a>";
        try
        {
            var document = MediaDocument("one");
            await new SiteEngine(new TestLogger(), new ProjectionProviderFactory([document], new ProjectionBodyStore(html)), new DefaultSearchIndexBuilder())
                .BuildAsync(config, root, new ConfigOverrides());

            var output = Path.Combine(root, "dist");
            using var projection = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "content/blog/one/index.html.json")));
            Assert.Equal(html, projection.RootElement.GetProperty("body").GetString());
            using var search = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "search.json")));
            var searchText = search.RootElement.EnumerateArray().Single(item => item.GetProperty("url").GetString() == "/blog/one/")
                .GetProperty("content").GetString()!;
            var markdown = File.ReadAllText(Path.Combine(output, "content/blog/one/index.html.md"));
            var llmsFull = File.ReadAllText(Path.Combine(output, "llms-full.txt"));
            var text = representation switch { "search" => searchText, "markdown" => markdown, _ => llmsFull };
            foreach (var visible in new[] { "visible-between-script", "visible-after-script", "visible-between-style", "visible-after-style", "visible-link" })
                Assert.Contains(visible, text, StringComparison.Ordinal);
            foreach (var hidden in new[] { "script-hidden-one", "script-hidden-link", "script-hidden-middle", "script-hidden-two", "style-hidden-one", "style-hidden-link", "style-hidden-middle", "style-hidden-two", "/private-script", "/private-style" })
                Assert.DoesNotContain(hidden, text, StringComparison.Ordinal);
            Assert.Contains("/public", markdown, StringComparison.Ordinal);
            Assert.Contains("/public", llmsFull, StringComparison.Ordinal);
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData("template", "search")]
    [InlineData("template", "markdown")]
    [InlineData("template", "llms-full")]
    [InlineData("noscript", "search")]
    [InlineData("noscript", "markdown")]
    [InlineData("noscript", "llms-full")]
    [InlineData("xmp", "search")]
    [InlineData("xmp", "markdown")]
    [InlineData("xmp", "llms-full")]
    public async Task BodyProjection_ParsingContextsPreservePublicTextAndActualLinks(string context, string representation)
    {
        var (root, initial) = CreateMediaSite();
        var config = initial with
        {
            Site = initial.Site with { Seo = new SeoConfig { Geo = new SeoGeoConfig { LlmsFullTxt = true } } },
            Content = initial.Content with { Media = initial.Content.Media with { DownloadToLocal = false } }
        };
        var html = context switch
        {
            "template" => "<a href='/outer'>before<template><a href='/hidden'>HIDDENMARKER</a></template>after</a>",
            "noscript" => "before<noscript><a href='/raw'>RAWLABEL</a></noscript><a href='/visible'>VISIBLEMARKER</a>",
            _ => "before<xmp>example</xmp>after"
        };
        try
        {
            await new SiteEngine(new TestLogger(), new ProjectionProviderFactory([MediaDocument("one")], new ProjectionBodyStore(html)), new DefaultSearchIndexBuilder())
                .BuildAsync(config, root, new ConfigOverrides());

            var output = Path.Combine(root, "dist");
            string text;
            if (representation == "search")
            {
                using var search = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "search.json")));
                text = search.RootElement.EnumerateArray().Single(item => item.GetProperty("url").GetString() == "/blog/one/")
                    .GetProperty("content").GetString()!;
                Assert.Equal(context switch
                {
                    "template" => "before after",
                    "noscript" => "before <a href='/raw'>RAWLABEL</a>  VISIBLEMARKER",
                    _ => "before example after"
                }, text);
            }
            else
            {
                text = File.ReadAllText(Path.Combine(output, representation == "markdown" ? "content/blog/one/index.html.md" : "llms-full.txt"));
                var links = Markdown.Parse(text).Descendants<LinkInline>().ToArray();
                if (context == "xmp")
                {
                    Assert.Empty(links);
                    Assert.Contains("before example after", text);
                }
                else Assert.Equal(context == "template" ? "/outer" : "/visible", Assert.Single(links).Url);
                if (context == "noscript")
                    Assert.Contains("&lt;a href='/raw'&gt;RAWLABEL&lt;/a&gt;", text);
            }
            Assert.Contains("before", text);
            if (context != "noscript") Assert.Contains("after", text);
            Assert.DoesNotContain("HIDDENMARKER", text);
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BodyProjection_DelayedBodyIsSharedByHtmlJsonAndMarkdownAcrossBuilds(bool localize, bool multilingual)
    {
        var (root, initial) = CreateMediaSite(multilingual, "/site/", "/media");
        var config = initial with { Content = initial.Content with { Media = initial.Content.Media with { DownloadToLocal = localize } } };
        using var server = new MediaImageServer();
        try
        {
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "post.html"), "<html><body><nav>LAYOUT ONLY</nav><main>{{ page.content }}</main></body></html>");
            var documents = multilingual ? new[] { MediaDocument("one"), MediaDocument("two", "zh") } : new[] { MediaDocument("one") };
            documents = documents.Select(document => document with { CustomFields = new Dictionary<string, ContentField>(document.CustomFields!) { ["bodyFingerprint"] = new("text", "stable-source-body") } }).ToArray();
            if (multilingual) documents[1] = documents[1] with { Body = documents[1].Body with { BodyKey = "one" } };
            var html = $"<h3>用笔方法</h3><p>中锋行笔，侧锋铺墨。</p><h3>葫芦画法</h3><p>先画小圆，再画大圆。</p><a href=\"https://example.org/guide?q=ink&amp;lang=zh\">绘画参考</a><img src=\"{server.Url}\" alt=\"葫芦图\">";
            Dictionary<string, string>? expected = null;
            foreach (var clean in new[] { true, false, true })
            {
                var store = new ProjectionBodyStore(html);
                var engine = new SiteEngine(new TestLogger(), new ProjectionProviderFactory(documents, store), new DefaultSearchIndexBuilder());
                var result = await engine.BuildAsync(config, root, new ConfigOverrides { Clean = clean, Incremental = true });
                Assert.Equal(1, store.Calls);
                if (!clean) Assert.True(result.Incremental.CacheHitCount >= documents.Length);
                var actual = new Dictionary<string, string>();
                foreach (var document in documents)
                {
                    var variant = Path.Combine(root, "dist", multilingual ? document.Record.Presentation.Language : "");
                    var page = File.ReadAllText(Path.Combine(variant, "blog", document.Slug, "index.html"));
                    var jsonText = File.ReadAllText(Path.Combine(variant, "content", "blog", document.Slug, "index.html.json"));
                    using var json = JsonDocument.Parse(jsonText);
                    var body = json.RootElement.GetProperty("body").GetString();
                    var expectedBody = localize ? html.Replace(server.Url, "/site/" + (multilingual ? document.Record.Presentation.Language + "/" : "") + "media/" + Path.GetFileName(Assert.Single(Directory.EnumerateFiles(Path.Combine(root, ".cache", "media"), "*.png")))) : html;
                    Assert.Equal(expectedBody, body);
                    Assert.Contains(expectedBody, page);
                    Assert.Empty(json.RootElement.GetProperty("sections").EnumerateArray());
                    var markdown = File.ReadAllText(Path.Combine(variant, "content", "blog", document.Slug, "index.html.md"));
                    Assert.Contains("## Body", markdown);
                    Assert.Contains("[绘画参考](<https://example.org/guide?q=ink&amp;lang=zh>)", markdown);
                    foreach (var text in new[] { "用笔方法", "中锋行笔，侧锋铺墨。", "葫芦画法", "先画小圆，再画大圆。" }) Assert.Contains(text, markdown);
                    Assert.DoesNotContain("LAYOUT ONLY", body);
                    Assert.DoesNotContain("LAYOUT ONLY", markdown);
                    actual[document.Id] = jsonText + markdown + page;
                }
                if (expected is not null) Assert.Equal(expected, actual);
                expected = actual;
                Assert.False(BuildRecoveryTracker.HasIncompleteBuild(Path.Combine(root, "dist")));
            }
            await new SiteEngine(new TestLogger(), new ProjectionProviderFactory([], new ProjectionBodyStore(html)), new DefaultSearchIndexBuilder())
                .BuildAsync(config, root, new ConfigOverrides { Clean = false, Incremental = true });
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "dist"), "*.html.json", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "dist"), "*.html.md", SearchOption.AllDirectories));
        }
        finally { CleanupDir(root); }
    }

    [Fact]
    public async Task BodyProjection_DerivedAndInlineDocumentsPreserveBodyAndClassification()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-body-projection-" + Guid.NewGuid().ToString("N"));
        const string html = "<h3>葫芦与福禄</h3><p>葫芦寓意福禄。</p>";
        var store = new ProjectionBodyStore(html);
        await using var cached = new BodyCacheDecorator(store);
        try
        {
            var document = MediaDocument("one");
            var derived = MediaDocument("derived") with { Body = document.Body };
            var inline = MediaDocument("inline") with { Body = new ContentBodyRef(Html: html) };
            inline = inline with
            {
                Record = inline.Record with
                {
                    Presentation = inline.Record.Presentation with { Body = "stale record body" },
                    Classification = inline.Record.Classification with { Sections = ["culture"] }
                }
            };
            var context = new PublishProjectionContext(new AppConfig { Site = new SiteConfig { Name = "test", Title = "Test" }, Content = TestContent.Markdown(collection: "post") }, root, CanonicalContentGraph.Empty,
                new Dictionary<string, SeoIndexEntry>(), new Dictionary<string, SeoModel>(),
                [new(document, new RouteInfo("/one/", "one/index.html", ""))], cached,
                DerivedDocuments: [new(derived, new RouteInfo("/derived/", "derived/index.html", "")), new(inline, new RouteInfo("/inline/", "inline/index.html", ""))]);
            await new DefaultContentProjectionWriter(new(), new(), new(), []).WriteAsync(context);
            Assert.Equal(1, store.Calls);
            foreach (var slug in new[] { "one", "derived", "inline" })
            {
                using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content", slug, "index.html.json")));
                Assert.Equal(html, json.RootElement.GetProperty("body").GetString());
                Assert.Contains("葫芦寓意福禄。", File.ReadAllText(Path.Combine(root, "content", slug, "index.html.md")));
                Assert.Equal(slug == "inline" ? new[] { "culture" } : [], json.RootElement.GetProperty("sections").EnumerateArray().Select(value => value.GetString()).ToArray());
            }
            Assert.Null(document.Record.Presentation.Body);
        }
        finally { CleanupDir(root); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BodyProjection_FailureOrCancellationCannotCompleteAndRecovers(bool localize, bool cancel)
    {
        var (root, initial) = CreateMediaSite();
        var config = initial with { Content = initial.Content with { Media = initial.Content.Media with { DownloadToLocal = localize } } };
        using var cts = new CancellationTokenSource();
        try
        {
            var documents = new[] { MediaDocument("one") };
            async Task Build(IContentBodyStore store, bool clean = false, CancellationToken token = default)
                => await new SiteEngine(new TestLogger(), new ProjectionProviderFactory(documents, store), new DefaultSearchIndexBuilder())
                    .BuildAsync(config, root, new ConfigOverrides { Clean = clean, Incremental = true }, token);
            const string html = "<h3>用笔方法</h3><p>中锋行笔。</p>";
            await Build(new ProjectionBodyStore(html));
            var manifestPath = Path.Combine(root, ".cache", "build-manifest.json");
            var manifest = File.ReadAllBytes(manifestPath);
            var beforeFailure = TransactionHashes(root);
            var failing = new ProjectionFailureStore(cancel ? cts : null);
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(failing, token: cts.Token));
            else await Assert.ThrowsAsync<InvalidOperationException>(() => Build(failing));
            var output = Path.Combine(root, "dist");
            Assert.Equal(beforeFailure, TransactionHashes(root));
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(output));
            Assert.Equal(manifest, File.ReadAllBytes(manifestPath));
            await Build(new ProjectionBodyStore(html));
            Assert.False(BuildRecoveryTracker.HasIncompleteBuild(output));
            var recovered = PublicHashes(output);
            await Build(new ProjectionBodyStore(html), clean: true);
            Assert.Equal(recovered, PublicHashes(output));
        }
        finally { CleanupDir(root); }
    }

    [Fact, Trait("Category", "Integration")]
    public async Task BodyProjection_DraftWithdrawalNeverLoadsHiddenBodyAndEmptyBodyStaysEmpty()
    {
        var (root, config) = CreateMediaSite();
        try
        {
            var document = MediaDocument("one");
            var store = new ProjectionBodyStore("");
            await new SiteEngine(new TestLogger(), new ProjectionProviderFactory([document], store), new DefaultSearchIndexBuilder()).BuildAsync(config, root, new ConfigOverrides());
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dist", "content/blog/one/index.html.json")));
            Assert.Equal("", json.RootElement.GetProperty("body").GetString());
            Assert.DoesNotContain("## Body", File.ReadAllText(Path.Combine(root, "dist", "content/blog/one/index.html.md")));
            Assert.True(File.Exists(Path.Combine(root, "dist", "blog/one/index.html")));
            document = document with { CustomFields = new Dictionary<string, ContentField>(document.CustomFields!) { ["draft"] = new("bool", true) } };
            var hidden = new ProjectionBodyStore("PRIVATE DRAFT");
            await new SiteEngine(new TestLogger(), new ProjectionProviderFactory([document], hidden), new DefaultSearchIndexBuilder())
                .BuildAsync(config, root, new ConfigOverrides { Clean = false, Incremental = true });
            Assert.Equal(0, hidden.Calls);
            Assert.False(File.Exists(Path.Combine(root, "dist", "content/blog/one/index.html.json")));
            Assert.False(File.Exists(Path.Combine(root, "dist", "content/blog/one/index.html.md")));
            Assert.False(File.Exists(Path.Combine(root, "dist", "blog/one/index.html")));
        }
        finally { CleanupDir(root); }
    }

    private sealed class ProjectionFailureStore(CancellationTokenSource? cancel) : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
        {
            cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("projection body failure");
        }
    }

    private sealed class ProjectionBodyStore(string html) : IContentBodyStore
    {
        private int _calls;
        public int Calls => _calls;
        public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(5, cancellationToken);
            return new ContentBody(html);
        }
    }

    private sealed class ProjectionProviderFactory(ContentDocument[] documents, IContentBodyStore store) : IContentProviderFactory
    {
        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
            => new StaticContentProvider(new RawContentLoadResult(ToRawDocuments(documents), store));
        public Task<RawContentLoadResult> LocalizeContentImagesAsync(RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
            => ContentProviderFactory.LocalizeContentImagesAsync(result, media, rootDir, cacheDir, logger, cancellationToken);
    }
}
