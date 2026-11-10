using Xunit;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.PublishAuditRules;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for RepresentationAuditRules representation presence and projection file checks.
/// </summary>
public sealed class RepresentationAuditRulesTests : IDisposable
{
    private readonly string _outputDir;

    public RepresentationAuditRulesTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "bukit-rep-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_outputDir, recursive: true);
    }

    private static PublishDocument MakeDocument(
        IReadOnlyList<string> representationKinds,
        string routeUrl = "/posts/post-1/",
        string outputPath = "posts/post-1/index.html",
        ContentRecord? record = null)
        => new(
            RouteUrl: routeUrl,
            OutputPath: outputPath,
            Canonical: "https://example.com/posts/post-1/",
            Indexable: true,
            ContentType: "post",
            IsDerived: false,
            SourceItemId: null,
            LastModified: null,
            Title: "Post 1",
            Description: null,
            Language: "en",
            Author: null,
            Organization: null,
            Source: "markdown",
            OriginalSource: null,
            ReviewStatus: null,
            Summary: "Summary",
            UpdatedAt: null,
            SourceReferences: [],
            EntityNames: [],
            EntitySummaries: [],
            RepresentationKinds: representationKinds,
            SchemaTypes: [],
            SemanticOutline: [],
            SitemapIncluded: false,
            SearchIncluded: false,
            RssIncluded: false,
            AtomFeedIncluded: false,
            JsonFeedIncluded: false,
            LlmsIncluded: false,
            LlmsFullIncluded: false,
            RobotsIncluded: false,
            ManifestIncluded: false,
            SeoModel: null,
            ContentRecord: record);

    private static ContentRecord MakeRecord() => new(
        new ContentIdentity("post-1", "post-1", "post-1", "post", "published"),
        new ContentPresentation("Post 1", "Summary", "<p>Body</p>", "en", []),
        new ContentClassification("post", "post", [], []),
        new ContentOwnership(null, null, null, null),
        new ContentLifecycle(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), null, null, null),
        new ProvenanceRecord("markdown", null, [], [], null),
        new TrustMetadata(null, "approved", []),
        [],
        [],
        []);

    [Fact]
    public void Analyze_MissingRequiredKinds_AddsError()
    {
        var doc = MakeDocument([]);
        var issues = new List<PublishAuditIssue>();
        RepresentationAuditRules.Analyze(doc, _outputDir, issues);
        Assert.Contains(issues, i => i.Code == "publish.representation_missing");
    }

    [Fact]
    public void Analyze_AllRequiredKinds_NoMissingError()
    {
        var doc = MakeDocument(["html", "semantic-html", "json", "markdown"]);
        var issues = new List<PublishAuditIssue>();
        RepresentationAuditRules.Analyze(doc, _outputDir, issues);
        Assert.DoesNotContain(issues, i => i.Code == "publish.representation_missing");
    }

    [Fact]
    public void Analyze_NullContentRecord_StopsEarly()
    {
        var doc = MakeDocument(["html", "semantic-html", "json", "markdown"], record: null);
        var issues = new List<PublishAuditIssue>();
        RepresentationAuditRules.Analyze(doc, _outputDir, issues);
        // No projection file checks run when ContentRecord is null
        Assert.DoesNotContain(issues, i => i.Code == "publish.representation_file_missing");
    }

    [Fact]
    public void Analyze_DeclaredJsonButFileMissing_AddsError()
    {
        var doc = MakeDocument(["html", "semantic-html", "json", "markdown"], record: MakeRecord());
        var issues = new List<PublishAuditIssue>();
        RepresentationAuditRules.Analyze(doc, _outputDir, issues);
        Assert.Contains(issues, i => i.Code == "publish.representation_file_missing");
    }

    [Fact]
    public void Analyze_ProjectionFileExists_NoMissingError()
    {
        // Create the json projection file where it is expected
        var record = MakeRecord();
        var basePath = ContentProjectionPaths.GetContentProjectionBasePath(_outputDir, new Bukit.Engine.Abstractions.Routing.RouteInfo("/posts/post-1/", "posts/post-1/index.html", ""));
        var dir = Path.GetDirectoryName(basePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(basePath + ".json", "{}");

        var doc = MakeDocument(["html", "semantic-html", "json", "markdown"], record: record);
        var issues = new List<PublishAuditIssue>();
        RepresentationAuditRules.Analyze(doc, _outputDir, issues);

        // JSON projection exists; markdown may still be missing, but no json-specific error
        Assert.DoesNotContain(issues, i => i.Code == "publish.representation_file_missing" && i.Message.Contains(".json"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("{ not json", "publish.manifest_invalid")]
    [InlineData("{\"items\": []}", "publish.manifest_invalid")]
    [InlineData("{\"documents\": {}}", "publish.manifest_invalid")]
    [InlineData("{\"documents\": [{\"route\": \"/posts/post-1/\", \"id\": \"post-1\", \"canonicalId\": \"post-1\", \"language\": \"en\", \"reviewStatus\": null}]}", null)]
    [InlineData("{\"documents\": [{\"route\": \"/posts/post-1/\", \"id\": \"other\", \"language\": \"de\"}]}", "publish.manifest_mismatch")]
    [InlineData("{\"documents\": [{\"route\": \"/elsewhere/\", \"canonicalId\": \"post-1\", \"language\": \"en\"}]}", "publish.manifest_mismatch")]
    public void Analyze_SharedManifestSnapshot_ReportsSameIssuesAsPerDocumentParse(string? manifest, string? expectedCode)
    {
        if (manifest is not null)
        {
            File.WriteAllText(Path.Combine(_outputDir, "agent-manifest.json"), manifest);
        }

        var documents = new[]
        {
            MakeDocument(["html", "semantic-html", "json", "markdown"], record: MakeRecord()),
            MakeDocument(["html", "semantic-html", "json", "markdown"], routeUrl: "/posts/post-2/", outputPath: "posts/post-2/index.html", record: MakeRecord()),
            MakeDocument(["html", "semantic-html", "json", "markdown"], record: MakeRecord()) with { Indexable = false }
        };

        var perDocument = new List<PublishAuditIssue>();
        foreach (var document in documents)
        {
            RepresentationAuditRules.Analyze(document, _outputDir, perDocument);
        }

        var shared = new List<PublishAuditIssue>();
        using (var snapshot = new AgentManifestSnapshot(_outputDir))
        {
            foreach (var document in documents)
            {
                RepresentationAuditRules.Analyze(document, _outputDir, shared, snapshot);
            }
        }

        Assert.Equal(perDocument, shared);
        var manifestIssues = shared.Where(issue => issue.Route == "/posts/post-1/" && issue.Code.StartsWith("publish.manifest_", StringComparison.Ordinal)).ToList();
        if (expectedCode is null)
        {
            Assert.Empty(manifestIssues);
        }
        else
        {
            Assert.Contains(manifestIssues, issue => issue.Code == expectedCode);
        }
    }
}
