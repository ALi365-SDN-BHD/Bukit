using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class LlmsTxtPluginTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-llmstxt-tests-" + Guid.NewGuid().ToString("N"));

    public LlmsTxtPluginTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_root, recursive: true);
    }

    private sealed class TestLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    [Fact]
    public async Task AfterBuild_WithGeoDisabled_DoesNotGenerateLlmsTxt()
    {
        var outputDir = Path.Combine(_root, "dist-disabled");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: false);

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        Assert.False(File.Exists(Path.Combine(outputDir, "llms.txt")));
        Assert.False(File.Exists(Path.Combine(outputDir, "llms-full.txt")));
    }

    [Fact]
    public async Task AfterBuild_WithLlmsTxtEnabled_GeneratesLlmsTxt()
    {
        var outputDir = Path.Combine(_root, "dist-llmstxt");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsTxt: true);

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var path = Path.Combine(outputDir, "llms.txt");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Test Site", content, StringComparison.Ordinal);
        Assert.Contains("A test site", content, StringComparison.Ordinal);
        Assert.Contains("- [Test Page]", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterBuild_WithLlmsTxtEnabled_ShouldUseCanonicalSummaryForLinkDescription()
    {
        var outputDir = Path.Combine(_root, "dist-llmstxt-summary");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsTxt: true,
            itemSummary: "Canonical llms summary");

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("- [Test Page](https://example.com/page-1/): Canonical llms summary", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterBuild_WithLlmsFullTxtEnabled_GeneratesLlmsFullTxt()
    {
        var outputDir = Path.Combine(_root, "dist-full");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true);

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var path = Path.Combine(outputDir, "llms-full.txt");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("# Test Page", content, StringComparison.Ordinal);
        Assert.Contains("URL:", content, StringComparison.Ordinal);
        Assert.Contains("/page-1", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterBuild_WithAbsoluteSiteUrl_WritesAbsoluteLlmsLinks()
    {
        var outputDir = Path.Combine(_root, "dist-absolute-url");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsTxt: true);

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("- [Test Page](https://example.com/page-1/)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("(/https://", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterBuild_WithOptionalLinks_IncludesOptionalSection()
    {
        var outputDir = Path.Combine(_root, "dist-optional");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsTxt: true,
            optionalLinks: new[]
            {
                new LlmsTxtOptionalLink { Title = "GitHub", Url = "https://github.com/repo", Description = "Source code" }
            });

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("## Optional", content, StringComparison.Ordinal);
        Assert.Contains("- [GitHub](https://github.com/repo): Source code", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterBuild_WithDescriptionFallbackInFullTxt_UsesFallbackChain()
    {
        var outputDir = Path.Combine(_root, "dist-fallback");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true,
            itemDescription: "Item-specific description",
            itemSeoDesc: null, itemSummary: null);

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("Item-specific description", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterBuild_WithCanonicalTrustMetadataInFullTxt_IncludesCanonicalFields()
    {
        const string relatedNotionId = "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb";
        var outputDir = Path.Combine(_root, "dist-canonical");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true,
            itemSummary: "Canonical summary");

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("Author: Ali", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Source: notion", content, StringComparison.Ordinal);
        Assert.Contains("Review Status: approved", content, StringComparison.Ordinal);
        Assert.Contains("Entities: Bukit", content, StringComparison.Ordinal);
        Assert.DoesNotContain(relatedNotionId, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AfterBuild_WithOnlySummaryInFullTxt_UsesSummary()
    {
        var outputDir = Path.Combine(_root, "dist-summary");
        Directory.CreateDirectory(outputDir);
        var (context, config) = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true,
            itemSummary: "Summary description",
            itemSeoDesc: null, itemDescription: null);

        var plugin = new LlmsTxtPlugin(config);
        await plugin.AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("Summary description", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterBuild_WithPositiveLimit_CapsCollectionAtConfiguredCount()
    {
        var outputDir = Path.Combine(_root, "dist-positive-limit");
        var articles = CreateArticles("posts", 21);
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 20);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        var urls = ReadSectionUrls(content, "Posts");
        var expected = Enumerable.Range(1, 20)
            .Reverse()
            .Select(index => $"https://example.com/posts/posts-{index:D2}/")
            .ToArray();
        Assert.Equal(expected, urls);
        Assert.DoesNotContain("https://example.com/posts/posts-00/", urls);
    }

    [Fact]
    public async Task AfterBuild_WithZeroLimit_IncludesAllArticlesInEveryCollection()
    {
        var outputDir = Path.Combine(_root, "dist-unlimited-collections");
        var articles = CreateArticles("posts", 21)
            .Concat(CreateArticles("news", 23))
            .ToArray();
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        var postUrls = ReadSectionUrls(content, "Posts");
        var newsUrls = ReadSectionUrls(content, "News");
        Assert.Equal(
            Enumerable.Range(0, 21)
                .Reverse()
                .Select(index => $"https://example.com/posts/posts-{index:D2}/"),
            postUrls);
        Assert.Equal(
            Enumerable.Range(0, 23)
                .Reverse()
                .Select(index => $"https://example.com/news/news-{index:D2}/"),
            newsUrls);

        var allUrls = postUrls.Concat(newsUrls).ToArray();
        Assert.Equal(44, allUrls.Length);
        Assert.Equal(allUrls.Length, allUrls.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AfterBuild_CollectionArticles_AreOrderedByPublishedDescending()
    {
        var outputDir = Path.Combine(_root, "dist-published-order");
        var articles = new[]
        {
            new ArticleFixture("middle", "posts", DateTimeOffset.Parse("2026-02-02T00:00:00Z"), "/posts/middle/"),
            new ArticleFixture("oldest", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/oldest/"),
            new ArticleFixture("newest", "posts", DateTimeOffset.Parse("2026-03-03T00:00:00Z"), "/posts/newest/")
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Equal(
            [
                "https://example.com/posts/newest/",
                "https://example.com/posts/middle/",
                "https://example.com/posts/oldest/"
            ],
            ReadSectionUrls(content, "Posts"));
    }

    [Fact]
    public async Task AfterBuild_Unlimited_DeduplicatesEntriesAndPreservesCanonicalUrls()
    {
        var outputDir = Path.Combine(_root, "dist-unlimited-deduplicated");
        var articles = new[]
        {
            new ArticleFixture("first", "posts", DateTimeOffset.Parse("2026-02-02T00:00:00Z"), "/posts/first/"),
            new ArticleFixture("second", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/second/")
        };
        var (context, config) = CreateArticleContext(
            outputDir,
            articles,
            maxArticles: 0,
            duplicateFirstInDerived: true);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        var urls = ReadSectionUrls(content, "Posts");
        Assert.Equal(
            ["https://example.com/posts/first/", "https://example.com/posts/second/"],
            urls);
        Assert.Equal(urls.Count, urls.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AfterBuild_EmptyConfiguredCollection_WritesLlmsTxtWithoutError()
    {
        var outputDir = Path.Combine(_root, "dist-empty-collection");
        var (context, config) = CreateArticleContext(
            outputDir,
            Array.Empty<ArticleFixture>(),
            maxArticles: 0,
            configuredCollections: ["empty"]);

        var exception = await Record.ExceptionAsync(() => new LlmsTxtPlugin(config).AfterBuildAsync(context));

        Assert.Null(exception);
        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("No indexable pages found.", content, StringComparison.Ordinal);
        Assert.DoesNotContain("## Empty", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LlmsTxt_EqualPublishAtLimit_IsInputOrderIndependent()
    {
        var published = DateTimeOffset.Parse("2026-02-02T00:00:00Z");
        var articles = new[]
        {
            new ArticleFixture("alpha", "posts", published, "/posts/alpha/"),
            new ArticleFixture("beta", "posts", published, "/posts/beta/"),
            new ArticleFixture("gamma", "posts", published, "/posts/gamma/")
        };

        async Task<string> RenderAsync(IReadOnlyList<ArticleFixture> order)
        {
            var outputDir = Path.Combine(_root, $"dist-llms-order-{Guid.NewGuid():N}");
            var (context, config) = CreateArticleContext(outputDir, order, maxArticles: 2);
            await new LlmsTxtPlugin(config).AfterBuildAsync(context);
            return File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        }

        var forward = await RenderAsync(articles);
        var reversed = await RenderAsync(articles.Reverse().ToArray());

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public async Task Curation_NonIndexableInclude_RemainsExcluded()
    {
        var outputDir = Path.Combine(_root, "dist-curation-nonindexable");
        var articles = new[]
        {
            new ArticleFixture("visible", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/visible/"),
            new ArticleFixture("hidden", "posts", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "/posts/hidden/",
                Llms: new Dictionary<string, object> { ["visibility"] = "include" }, Indexable: false)
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("https://example.com/posts/visible/", content, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/posts/hidden/", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Curation_ExplicitExclude_DisappearsFromBothFiles()
    {
        var outputDir = Path.Combine(_root, "dist-curation-exclude");
        var articles = new[]
        {
            new ArticleFixture("kept", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/kept/"),
            new ArticleFixture("dropped", "posts", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "/posts/dropped/",
                Llms: new Dictionary<string, object> { ["visibility"] = "exclude" })
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0, llmsFullTxt: true);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var compact = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        var full = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("https://example.com/posts/kept/", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/posts/dropped/", compact, StringComparison.Ordinal);
        Assert.Contains("https://example.com/posts/kept/", full, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/posts/dropped/", full, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Curation_ExplicitInclude_SurvivesCapAndAutoFillsRemainingSlots()
    {
        var outputDir = Path.Combine(_root, "dist-curation-include-cap");
        var articles = new[]
        {
            new ArticleFixture("posts-00", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/posts-00/"),
            new ArticleFixture("posts-01", "posts", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "/posts/posts-01/"),
            new ArticleFixture("pinned", "posts", DateTimeOffset.Parse("2025-12-31T00:00:00Z"), "/posts/pinned/",
                Llms: new Dictionary<string, object> { ["visibility"] = "include" })
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 1);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        var urls = ReadSectionUrls(content, "Posts");
        Assert.Equal(
            ["https://example.com/posts/posts-01/", "https://example.com/posts/pinned/"],
            urls);
    }

    [Fact]
    public async Task Curation_Priority_SortsDescendingThenPublishedThenUrl()
    {
        var outputDir = Path.Combine(_root, "dist-curation-priority");
        var articles = new[]
        {
            new ArticleFixture("low", "posts", DateTimeOffset.Parse("2026-03-03T00:00:00Z"), "/posts/low/",
                Llms: new Dictionary<string, object> { ["priority"] = 0 }),
            new ArticleFixture("high-b", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/high-b/",
                Llms: new Dictionary<string, object> { ["priority"] = 5 }),
            new ArticleFixture("high-a", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/high-a/",
                Llms: new Dictionary<string, object> { ["priority"] = 5 }),
            new ArticleFixture("high-c", "posts", DateTimeOffset.Parse("2026-02-02T00:00:00Z"), "/posts/high-c/",
                Llms: new Dictionary<string, object> { ["priority"] = 5 })
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Equal(
            [
                "https://example.com/posts/high-c/",
                "https://example.com/posts/high-a/",
                "https://example.com/posts/high-b/",
                "https://example.com/posts/low/"
            ],
            ReadSectionUrls(content, "Posts"));
    }

    [Fact]
    public async Task Curation_OptionalPages_AppearOnlyInSingleOptionalSection()
    {
        var outputDir = Path.Combine(_root, "dist-curation-optional");
        var articles = new[]
        {
            new ArticleFixture("main", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/main/"),
            new ArticleFixture("extra", "posts", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "/posts/extra/",
                Llms: new Dictionary<string, object> { ["tier"] = "optional" })
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Equal(["https://example.com/posts/main/"], ReadSectionUrls(content, "Posts"));
        Assert.Equal(["https://example.com/posts/extra/"], ReadSectionUrls(content, "Optional"));
        Assert.Equal(1, content.Split('\n').Count(line => line.Trim() == "## Optional"));
    }

    [Fact]
    public async Task Curation_FullOutput_IncludesPrimaryAndOptionalButNotExcluded()
    {
        var outputDir = Path.Combine(_root, "dist-curation-full");
        var articles = new[]
        {
            new ArticleFixture("main", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/main/"),
            new ArticleFixture("extra", "posts", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "/posts/extra/",
                Llms: new Dictionary<string, object> { ["tier"] = "optional" }),
            new ArticleFixture("gone", "posts", DateTimeOffset.Parse("2026-01-03T00:00:00Z"), "/posts/gone/",
                Llms: new Dictionary<string, object> { ["visibility"] = "exclude" })
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0, llmsFullTxt: true);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var full = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("https://example.com/posts/main/", full, StringComparison.Ordinal);
        Assert.Contains("https://example.com/posts/extra/", full, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/posts/gone/", full, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Curation_OmittedMetadata_PreservesCurrentOrdering()
    {
        var outputDir = Path.Combine(_root, "dist-curation-omitted");
        var articles = CreateArticles("posts", 5);
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 3);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Equal(
            [
                "https://example.com/posts/posts-04/",
                "https://example.com/posts/posts-03/",
                "https://example.com/posts/posts-02/"
            ],
            ReadSectionUrls(content, "Posts"));
    }

    [Fact]
    public async Task Curation_RepeatedGeneration_ProducesIdenticalBytes()
    {
        var articles = new[]
        {
            new ArticleFixture("main", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/main/",
                Llms: new Dictionary<string, object> { ["priority"] = 3 }),
            new ArticleFixture("extra", "posts", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "/posts/extra/",
                Llms: new Dictionary<string, object> { ["tier"] = "optional" })
        };

        async Task<string> RenderAsync(string dir)
        {
            var outputDir = Path.Combine(_root, dir);
            var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 1, llmsFullTxt: true);
            await new LlmsTxtPlugin(config).AfterBuildAsync(context);
            return File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8) +
                   File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        }

        Assert.Equal(await RenderAsync("dist-curation-repeat-a"), await RenderAsync("dist-curation-repeat-b"));
    }

    [Fact]
    public async Task Curation_InvalidMetadataWarnMode_IsAbsentFromBothFiles()
    {
        var outputDir = Path.Combine(_root, "dist-curation-invalid-warn");
        var articles = new[]
        {
            new ArticleFixture("kept", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/kept/"),
            new ArticleFixture("broken", "posts", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), "/posts/broken/",
                Llms: new Dictionary<string, object> { ["visibility"] = "always" })
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0, llmsFullTxt: true);

        await new LlmsTxtPlugin(config).AfterBuildAsync(context);

        var compact = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        var full = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("https://example.com/posts/kept/", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/posts/broken/", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("https://example.com/posts/broken/", full, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Curation_InvalidMetadataStrictMode_FailsBuild()
    {
        var outputDir = Path.Combine(_root, "dist-curation-invalid-strict");
        var articles = new[]
        {
            new ArticleFixture("broken", "posts", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "/posts/broken/",
                Llms: new Dictionary<string, object> { ["visibility"] = "always" })
        };
        var (context, config) = CreateArticleContext(outputDir, articles, maxArticles: 0, diagnostics: "strict");

        var exception = await Assert.ThrowsAsync<ConfigException>(
            () => new LlmsTxtPlugin(config).AfterBuildAsync(context));

        Assert.Contains("geo.llms_visibility_invalid", exception.Message, StringComparison.Ordinal);
    }

    private sealed record ArticleFixture(
        string Id,
        string Collection,
        DateTimeOffset Published,
        string RouteUrl,
        Dictionary<string, object>? Llms = null,
        bool Indexable = true)
    {
        public string Canonical => $"https://example.com{RouteUrl}";
    }

    private static IReadOnlyList<ArticleFixture> CreateArticles(string collection, int count)
        => Enumerable.Range(0, count)
            .Select(index => new ArticleFixture(
                $"{collection}-{index:D2}",
                collection,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddDays(index),
                $"/{collection}/{collection}-{index:D2}/"))
            .ToArray();

    private (BuildContext Context, AppConfig Config) CreateArticleContext(
        string outputDir,
        IReadOnlyList<ArticleFixture> articles,
        int maxArticles,
        bool duplicateFirstInDerived = false,
        IReadOnlyList<string>? configuredCollections = null,
        bool llmsFullTxt = false,
        string diagnostics = "warn")
    {
        var documents = new List<ContentDocument>();
        var routedDocuments = new List<RoutedContentDocument>();
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var article in articles)
        {
            var fieldValues = new Dictionary<string, object>
            {
                ["type"] = "post",
                ["collection"] = article.Collection,
                ["status"] = "published",
                ["summary"] = $"Summary for {article.Id}"
            };
            if (article.Llms is not null)
            {
                fieldValues["geo"] = new Dictionary<string, object>
                {
                    ["llms"] = article.Llms
                };
            }

            var fields = ContentFieldReader.ToFieldMap(fieldValues);
            var document = ContentDocument.Create(
                id: article.Id,
                title: $"Article {article.Id}",
                slug: article.Id,
                publishAt: article.Published,
                contentHtml: $"<p>{article.Id}</p>",
                fields: fields);
            var outputPath = $"{article.Collection}/{article.Id}/index.html";
            var route = new RouteInfo(article.RouteUrl, outputPath, "posts/post.html");
            var routed = new RoutedContentDocument(document, route);
            documents.Add(document);
            routedDocuments.Add(routed);
            seoIndex[outputPath] = new SeoIndexEntry(
                route,
                article.Canonical,
                Robots: null,
                Indexable: article.Indexable,
                LastModified: DateTimeOffset.UnixEpoch,
                SourceItemId: article.Id,
                ContentType: "post",
                IsDerived: false,
                Collection: article.Collection);
        }

        var collections = configuredCollections?.ToDictionary(
            name => name,
            name => new CollectionConfig { Permalink = $"/{name}/{{slug}}/" },
            StringComparer.OrdinalIgnoreCase);
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test Site",
                Description = "A test site",
                Url = "https://example.com",
                Collections = collections,
                Seo = new SeoConfig
                {
                    Diagnostics = diagnostics,
                    Geo = new SeoGeoConfig
                    {
                        Enabled = true,
                        LlmsTxt = true,
                        LlmsFullTxt = llmsFullTxt,
                        LlmsTxtMaxArticles = maxArticles
                    }
                }
            },
            Content = TestContent.Markdown()
        };
        var derivedDocuments = duplicateFirstInDerived && routedDocuments.Count > 0
            ? new[] { routedDocuments[0] }
            : Array.Empty<RoutedContentDocument>();
        var context = new BuildContext
        {
            RootDir = _root,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(_root, "layouts"),
            RoutedDocuments = routedDocuments,
            ContentGraph = new CanonicalContentGraph(
                documents.Select(document => document.Record).ToArray(),
                Array.Empty<EntityRecord>()),
            BodyStore = NullContentBodyStore.Instance,
            SeoIndex = seoIndex,
            Logger = new TestLogger()
        };
        context.DerivedDocuments.AddRange(derivedDocuments);

        return (context, config);
    }

    private static IReadOnlyList<string> ReadSectionUrls(string content, string heading)
    {
        var lines = content.Split('\n');
        var headingIndex = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), $"## {heading}", StringComparison.Ordinal));
        Assert.True(headingIndex >= 0, $"Missing llms.txt section: {heading}");

        return lines
            .Skip(headingIndex + 1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
            .Where(line => line.StartsWith("- [", StringComparison.Ordinal))
            .Select(line =>
            {
                var urlStart = line.IndexOf("](", StringComparison.Ordinal) + 2;
                var urlEnd = line.IndexOf(')', urlStart);
                return line[urlStart..urlEnd];
            })
            .ToArray();
    }

    private (BuildContext Context, AppConfig Config) CreateContext(
        string outputDir,
        bool geoEnabled = true,
        bool llmsTxt = false,
        bool llmsFullTxt = false,
        IReadOnlyList<LlmsTxtOptionalLink>? optionalLinks = null,
        string? itemSummary = null,
        string? itemSeoDesc = null,
        string? itemDescription = null)
    {
        var fieldValues = new Dictionary<string, object>
        {
            ["type"] = "page"
        };

        if (itemSummary is not null)
        {
            fieldValues["summary"] = itemSummary;
        }

        if (itemSeoDesc is not null)
        {
            fieldValues["seo_desc"] = itemSeoDesc;
        }

        if (itemDescription is not null)
        {
            fieldValues["description"] = itemDescription;
        }

        var item = ContentDocument.Create(
            id: "page-1",
            title: "Test Page",
            slug: "test-page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>Hello world</p>",
            fields: ContentFieldReader.ToFieldMap(fieldValues));
        var route = new RouteInfo("/page-1/", "page-1/index.html", "pages/page.html");

        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["page-1/index.html"] = new SeoIndexEntry(route, Canonical: "https://example.com/page-1", Robots: null, Indexable: true, DateTimeOffset.UtcNow, SourceItemId: null, ContentType: "page")
        };

        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test Site",
                Description = "A test site",
                Url = "https://example.com",
                Seo = new SeoConfig
                {
                    Geo = new SeoGeoConfig
                    {
                        Enabled = geoEnabled,
                        LlmsTxt = llmsTxt,
                        LlmsFullTxt = llmsFullTxt,
                        LlmsTxtOptionalLinks = optionalLinks
                    }
                }
            },
            Content = TestContent.Markdown()
        };
        var context = new BuildContext
        {
            RootDir = _root,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(_root, "layouts"),
            RoutedDocuments = new[] { (item, route) }.ToRoutedDocuments(),
            ContentGraph = new CanonicalContentGraph(
            [
                new ContentRecord(
                    new ContentIdentity("page-1", "test-page", "test-page", "page", "published"),
                    new ContentPresentation("Test Page", itemSummary ?? itemDescription ?? "Summary", "<p>Hello world</p>", "en", []),
                    new ContentClassification("page", "page", [], []),
                    new ContentOwnership("Ali", null, null, null),
                    new ContentLifecycle(item.PublishAt, null, null, null),
                    new ProvenanceRecord("notion", null, [], [], null),
                    new TrustMetadata(null, "approved", []),
                    [
                        new EntityRecord("company", "Bukit"),
                        new EntityRecord("page", "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb")
                    ],
                    [],
                    [])
            ], [new EntityRecord("company", "Bukit")]),
            BodyStore = NullContentBodyStore.Instance,
            SeoIndex = seoIndex,
            Logger = new TestLogger()
        };

        return (context, config);
    }
}
