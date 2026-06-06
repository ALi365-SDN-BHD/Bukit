using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PublishAggregateInventoryTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "bukit-publish-aggregate-inventory-" + Guid.NewGuid().ToString("N"));

    public PublishAggregateInventoryTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public void Build_AddsCrawlerAndAgentAggregateRepresentationsToRouteInventory()
    {
        WriteOutput("post/index.html", PageHtml("Post", "https://example.com/post/"));
        File.WriteAllText(Path.Combine(_outputDir, "llms.txt"), "https://example.com/post/");
        File.WriteAllText(Path.Combine(_outputDir, "llms-full.txt"), "https://example.com/post/");
        File.WriteAllText(Path.Combine(_outputDir, "robots.txt"), "User-agent: *\nAllow: /\nSitemap: https://example.com/sitemap.xml\n");
        File.WriteAllText(Path.Combine(_outputDir, "agent-manifest.json"), """
            { "schema": "https://bukit.dev/schemas/agent-manifest.v1.json", "documents": [{ "route": "/post/" }] }
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var result = SeoAuditReportWriter.BuildMachineReadabilityTrustAudit(ConfigWithGeo(), _outputDir, index, models, ContentGraph());

        var document = Assert.Single(result.PublishReport.Documents);
        Assert.Contains("llms", document.RepresentationKinds);
        Assert.Contains("llms-full", document.RepresentationKinds);
        Assert.Contains("robots", document.RepresentationKinds);
        Assert.Contains("agent-manifest", document.RepresentationKinds);
        Assert.Contains(document.Representations, x => x.Kind == "llms" && x.Path == "llms.txt" && x.Url == "/llms.txt" && x.Generated);
        Assert.Contains(document.Representations, x => x.Kind == "llms-full" && x.Path == "llms-full.txt" && x.Url == "/llms-full.txt" && x.Generated);
        Assert.Contains(document.Representations, x => x.Kind == "robots" && x.Path == "robots.txt" && x.Url == "/robots.txt" && x.Generated);
        Assert.Contains(document.Representations, x => x.Kind == "agent-manifest" && x.Path == "agent-manifest.json" && x.Url == "/agent-manifest.json" && x.Generated);
    }

    [Fact]
    public void Build_ReportsLlmsRouteGapWhenIndexableDocumentIsMissing()
    {
        WriteOutput("post/index.html", PageHtml("Post", "https://example.com/post/"));
        File.WriteAllText(Path.Combine(_outputDir, "llms.txt"), "# llms\nhttps://example.com/other/");
        File.WriteAllText(Path.Combine(_outputDir, "llms-full.txt"), "# llms-full\nhttps://example.com/other/");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Entry("/post/", "post/index.html", "https://example.com/post/")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = Model("Post", "https://example.com/post/")
        };

        var result = SeoAuditReportWriter.BuildMachineReadabilityTrustAudit(ConfigWithGeo(), _outputDir, index, models, ContentGraph());

        Assert.Contains(result.SeoReport.Issues, x => x.Code == "publish.llms_missing_route" && x.Route == "/post/");
        Assert.Contains(result.SeoReport.Issues, x => x.Code == "publish.llms_full_missing_route" && x.Route == "/post/");
    }

    public void Dispose()
    {
        try { Directory.Delete(_outputDir, recursive: true); } catch { }
    }

    private void WriteOutput(string path, string html)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, html);
    }

    private static string PageHtml(string title, string canonical)
        => $"""
            <!doctype html>
            <html>
            <head><title>{title}</title><link rel="canonical" href="{canonical}" /></head>
            <body><header></header><nav></nav><main><article><h1>{title}</h1><time datetime="2026-06-05">June 5</time><p>Body content for people and machines.</p></article></main><footer></footer></body>
            </html>
            """;

    private static SeoIndexEntry Entry(string url, string outputPath, string canonical)
        => new(new RouteInfo(url, outputPath, "pages/post.html"), canonical, null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post");

    private static SeoModel Model(string title, string canonical)
        => new()
        {
            Title = title,
            Description = title + " description",
            Canonical = canonical
        };

    private static CanonicalContentGraph ContentGraph()
        => new(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], ["audit"]),
                new ContentOwnership("Ali", "Bukit", null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), DateTimeOffset.Parse("2026-06-05T01:00:00Z"), null, null),
                new ProvenanceRecord("markdown", "https://example.com/original", [], [], "synced"),
                new TrustMetadata(0.9, "approved", []),
                [new EntityRecord("company", "Bukit", "Bukit product")],
                [],
                [])
        ], [new EntityRecord("company", "Bukit")]);

    private static AppConfig ConfigWithGeo()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Seo = new SeoConfig
                {
                    Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true, LlmsFullTxt = true },
                    RobotsTxt = new SeoRobotsTxtConfig { Enabled = true }
                }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
}
