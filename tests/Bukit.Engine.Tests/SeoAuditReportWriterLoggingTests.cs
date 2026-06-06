using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoAuditReportWriterLoggingTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "bukit-seo-audit-logging-tests-" + Guid.NewGuid().ToString("N"));

    public SeoAuditReportWriterLoggingTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public void Write_UsesIssueDomainLogPrefixes()
    {
        WriteOutput("post/index.html");
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
        var logger = new CapturingLogger();

        SeoAuditReportWriter.Write(Config(), _outputDir, index, models, ContentGraph(), logger);

        Assert.Contains(logger.Warnings, x => x.StartsWith("publish.audit ", StringComparison.Ordinal) && x.Contains("code=publish.author_missing", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, x => x.StartsWith("geo.audit ", StringComparison.Ordinal) && x.Contains("code=geo.llms_txt_missing", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Warnings, x => x.StartsWith("seo.audit ", StringComparison.Ordinal) && x.Contains("code=publish.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Warnings, x => x.StartsWith("seo.audit ", StringComparison.Ordinal) && x.Contains("code=geo.", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_outputDir, recursive: true);
    }

    private void WriteOutput(string path)
    {
        var fullPath = Path.Combine(_outputDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "<!doctype html><html><head></head><body></body></html>");
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

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Debug(string message) { }

        public void Info(string message) { }

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message) => Warnings.Add(message);
    }
}
