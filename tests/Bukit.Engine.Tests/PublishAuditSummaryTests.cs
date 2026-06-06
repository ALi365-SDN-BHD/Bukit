using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PublishAuditSummaryTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "bukit-publish-audit-summary-tests-" + Guid.NewGuid().ToString("N"));

    public PublishAuditSummaryTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public void Build_ComputesPublishAuditSummaryBuckets()
    {
        WriteOutput("post/index.html", """
            <!doctype html>
            <html>
            <head><title>Post</title><link rel="canonical" href="https://example.com/post/" /></head>
            <body>
              <article>
                <img src="/a.png">
              </article>
            </body>
            </html>
            """);
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new(new RouteInfo("/post/", "post/index.html", "pages/post.html"), "https://example.com/post/", null, true, DateTimeOffset.Parse("2026-06-05T00:00:00Z"), "post-1", "post")
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["post/index.html"] = new()
            {
                Title = "Post",
                Description = "Post description",
                Canonical = "https://example.com/post/"
            }
        };

        var report = SeoAuditReportWriter.Build(Config(), _outputDir, index, models, ContentGraph());

        Assert.True(report.Summary.PublishIssueCount > 0);
        Assert.True(report.Summary.MachineReadabilityIssueCount > 0);
        Assert.True(report.Summary.TrustIssueCount > 0);
        Assert.True(report.Summary.RepresentationGapCount >= 0);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_outputDir, recursive: true);
    }

    private void WriteOutput(string path, string html)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, html);
    }

    private static CanonicalContentGraph ContentGraph()
        => new(
        [
            new ContentRecord(
                new ContentIdentity("post-1", "post", "post", "post", "published"),
                new ContentPresentation("Post", "Post description", "<article><p>body</p></article>", "en", []),
                new ContentClassification("post", "post", [], []),
                new ContentOwnership(null, null, null, null),
                new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, null, null),
                new ProvenanceRecord(null, null, [], [], null),
                new TrustMetadata(null, "", []),
                [],
                [],
                [])
        ], []);

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com"
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
}
