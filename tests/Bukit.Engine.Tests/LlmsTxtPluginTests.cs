using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;
using Bukit.Routing;
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
        try { Directory.Delete(_root, recursive: true); } catch { }
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

        var plugin = new LlmsTxtPlugin();
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

        var plugin = new LlmsTxtPlugin();
        plugin.AfterBuild(context);

        var path = Path.Combine(outputDir, "llms.txt");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Test Site", content, StringComparison.Ordinal);
        Assert.Contains("A test site", content, StringComparison.Ordinal);
        Assert.Contains("- [Test Page]", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithLlmsFullTxtEnabled_GeneratesLlmsFullTxt()
    {
        var outputDir = Path.Combine(_root, "dist-full");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true);

        var plugin = new LlmsTxtPlugin();
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

        var plugin = new LlmsTxtPlugin();
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

        var plugin = new LlmsTxtPlugin();
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

        var plugin = new LlmsTxtPlugin();
        plugin.AfterBuild(context);

        var content = File.ReadAllText(Path.Combine(outputDir, "llms-full.txt"), Encoding.UTF8);
        Assert.Contains("Item-specific description", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterBuild_WithOnlySummaryInFullTxt_UsesSummary()
    {
        var outputDir = Path.Combine(_root, "dist-summary");
        Directory.CreateDirectory(outputDir);
        var context = CreateContext(outputDir, geoEnabled: true, llmsFullTxt: true,
            itemSummary: "Summary description",
            itemSeoDesc: null, itemDescription: null);

        var plugin = new LlmsTxtPlugin();
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
        var meta = new Dictionary<string, object>
        {
            ["type"] = "page"
        };

        if (itemSummary is not null)
        {
            meta["summary"] = itemSummary;
        }

        if (itemSeoDesc is not null)
        {
            meta["seo_desc"] = itemSeoDesc;
        }

        if (itemDescription is not null)
        {
            meta["description"] = itemDescription;
        }

        var item = new ContentItem(
            Id: "page-1",
            Title: "Test Page",
            Slug: "test-page",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>Hello world</p>",
            Meta: meta);
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
                Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() }
            },
            RootDir = _root,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(_root, "layouts"),
            Routed = new[] { (item, route) },
            BodyStore = NullContentBodyStore.Instance,
            SeoIndex = seoIndex,
            Logger = new TestLogger()
        };
    }
}
