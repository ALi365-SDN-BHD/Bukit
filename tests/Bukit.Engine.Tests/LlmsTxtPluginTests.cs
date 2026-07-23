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
    public void AfterBuild_WithGeoDisabled_DoesNotGenerateLlmsTxt()
    {
        var outputDir = Path.Combine(_root, "dist-disabled");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: false);

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        Assert.False(File.Exists(Path.Combine(outputDir, "llms.txt")));
        Assert.False(File.Exists(Path.Combine(outputDir, "llms-full.txt")));
    }

    [Fact]
    public void AfterBuild_WithLlmsTxtEnabled_GeneratesLlmsTxt()
    {
        var outputDir = Path.Combine(_root, "dist-llmstxt");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsTxt: true);

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var path = Path.Combine(outputDir, "llms.txt");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Test Site", content, StringComparison.Ordinal);
        Assert.Contains("A test site", content, StringComparison.Ordinal);
        Assert.Contains("- [Test Page]", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithLlmsTxtEnabled_ShouldUseCanonicalSummaryForLinkDescription()
    {
        var outputDir = Path.Combine(_root, "dist-llmstxt-summary");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsTxt: true,
            itemSummary: "Canonical llms summary");

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("- [Test Page](https://example.com/page-1/): Canonical llms summary", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithLlmsFullTxtEnabled_GeneratesLlmsFullTxt()
    {
        var outputDir = Path.Combine(_root, "dist-full");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true);

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var path = Path.Combine(outputDir, "llms-full.txt");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("# Test Page", content, StringComparison.Ordinal);
        Assert.Contains("URL:", content, StringComparison.Ordinal);
        Assert.Contains("/page-1", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithAbsoluteSiteUrl_WritesAbsoluteLlmsLinks()
    {
        var outputDir = Path.Combine(_root, "dist-absolute-url");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsTxt: true);

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("- [Test Page](https://example.com/page-1/)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("(/https://", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithOptionalLinks_IncludesOptionalSection()
    {
        var outputDir = Path.Combine(_root, "dist-optional");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsTxt: true,
            optionalLinks: new[]
            {
                new LlmsTxtOptionalLink { Title = "GitHub", Url = "https://github.com/repo", Description = "Source code" }
            });

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms.txt"), Encoding.UTF8);
        Assert.Contains("## Optional", content, StringComparison.Ordinal);
        Assert.Contains("- [GitHub](https://github.com/repo): Source code", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithDescriptionFallbackInFullTxt_UsesFallbackChain()
    {
        var outputDir = Path.Combine(_root, "dist-fallback");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true,
            itemDescription: "Item-specific description",
            itemSeoDesc: null, itemSummary: null);

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("Item-specific description", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithCanonicalTrustMetadataInFullTxt_IncludesCanonicalFields()
    {
        const string relatedNotionId = "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb";
        var outputDir = Path.Combine(_root, "dist-canonical");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true,
            itemSummary: "Canonical summary");

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("Author: Ali", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Source: notion", content, StringComparison.Ordinal);
        Assert.Contains("Review Status: approved", content, StringComparison.Ordinal);
        Assert.Contains("Entities: Bukit", content, StringComparison.Ordinal);
        Assert.DoesNotContain(relatedNotionId, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AfterBuild_WithOnlySummaryInFullTxt_UsesSummary()
    {
        var outputDir = Path.Combine(_root, "dist-summary");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true,
            itemSummary: "Summary description",
            itemSeoDesc: null, itemDescription: null);

        var plugin = new LlmsTxtPlugin(context.Config);
        plugin.AfterBuild(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("Summary description", content, StringComparison.Ordinal);
    }

    private BuildContext CreateContext(
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

        return new BuildContext
        {
            Config = new AppConfig
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
            },
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
    }
}
