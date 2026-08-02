using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoRouteMapWriterTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-seo-route-map-tests-" + Guid.NewGuid().ToString("N"));

    public SeoRouteMapWriterTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public void Build_SortsRoutesByCanonicalThenRouteKeyUsingOrdinalComparison()
    {
        var builder = new SeoRouteMapBuilder("https://example.com", "/");
        builder.Add(Entry("/lower/", "/a/"), Model("/a/"), null);
        builder.Add(Entry("/upper/", "/B/"), Model("/B/"), null);

        var map = builder.Build(DateTimeOffset.Parse("2026-08-03T00:00:00Z"));

        Assert.Equal(["/B/", "/a/"], map.Routes.Select(route => route.Canonical));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" \t ", "")]
    [InlineData("  HTTPS://example.com/root  ", "HTTPS://example.com/root")]
    public void Build_NormalizesBlankSiteUrlAndTrimsPresentValues(string? siteUrl, string expected)
    {
        var map = new SeoRouteMapBuilder(siteUrl, "/")
            .Build(DateTimeOffset.Parse("2026-08-03T00:00:00Z"));

        Assert.Equal(expected, map.SiteUrl);
    }

    [Fact]
    public void Build_EmitsUppercaseHttpSchemeValuesAcceptedBySchema()
    {
        const string siteUrl = "HTTPS://example.com";
        const string canonical = "HTTP://example.com/article/";
        var builder = new SeoRouteMapBuilder(siteUrl, "/");
        builder.Add(Entry("/article/", canonical), Model(canonical), null);
        var map = builder.Build(DateTimeOffset.Parse("2026-08-03T00:00:00Z"));

        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "docs",
            "schemas",
            "seo-route-map.v1.schema.json")));
        var properties = schema.RootElement.GetProperty("properties");
        var sitePattern = properties.GetProperty("siteUrl").GetProperty("oneOf")[1].GetProperty("pattern").GetString()!;
        var canonicalPattern = properties
            .GetProperty("routes")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("canonical")
            .GetProperty("oneOf")[0]
            .GetProperty("pattern")
            .GetString()!;

        Assert.True(Uri.TryCreate(map.SiteUrl, UriKind.Absolute, out _));
        Assert.Matches(sitePattern, map.SiteUrl);
        Assert.True(Uri.TryCreate(map.Routes[0].Canonical, UriKind.Absolute, out _));
        Assert.Matches(canonicalPattern, map.Routes[0].Canonical);
    }

    [Fact]
    public void Write_PreservesDuplicateCanonicalsAndSerializesOnlyPrivacySafeIdentities()
    {
        const string internalSourceId = "notion-internal-source-id";
        WriteOutput("content/index.html");
        WriteOutput("static/index.html");
        var index = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["content/index.html"] = Entry("/content/", "/shared/", internalSourceId),
            ["static/index.html"] = Entry("/static/", "/shared/", isDerived: true)
        };
        var models = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["content/index.html"] = Model("/shared/"),
            ["static/index.html"] = Model("/shared/")
        };
        var record = new ContentRecord(
            new ContentIdentity(internalSourceId, "content", "content", "article", "published"),
            new ContentPresentation("Content", null, null, "zh-CN", []),
            new ContentClassification("article", "insights", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(
                DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-08-02T00:00:00Z"),
                null,
                null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [],
            [],
            []);

        var report = SeoAuditReportWriter.Write(
            Config(),
            _outputDir,
            index,
            models,
            new CanonicalContentGraph([record], [], [], []),
            new ConsoleLogger(LogLevel.Error));

        var mapPath = Path.Combine(_outputDir, ".bukit", "seo-route-map.json");
        Assert.True(File.Exists(mapPath));
        Assert.False(File.Exists(Path.Combine(_outputDir, "seo-route-map.json")));
        var json = File.ReadAllText(mapPath);
        Assert.DoesNotContain(internalSourceId, json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("https://bukit.dev/schemas/seo-route-map.v1.json", root.GetProperty("schema").GetString());
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(report.GeneratedAt, root.GetProperty("generatedAt").GetDateTimeOffset());
        Assert.Equal(string.Empty, root.GetProperty("siteUrl").GetString());
        Assert.Equal("/", root.GetProperty("baseUrl").GetString());

        var routes = root.GetProperty("routes").EnumerateArray().ToArray();
        Assert.Equal(2, routes.Length);
        Assert.All(routes, route => Assert.Equal("/shared/", route.GetProperty("canonical").GetString()));
        Assert.Equal(
            routes.Select(route => route.GetProperty("routeKey").GetString()).OrderBy(value => value, StringComparer.Ordinal),
            routes.Select(route => route.GetProperty("routeKey").GetString()));
        Assert.All(routes, route => Assert.Matches("^route:sha256:[0-9a-f]{64}$", route.GetProperty("routeKey").GetString()));
        Assert.NotEqual(routes[0].GetProperty("routeKey").GetString(), routes[1].GetProperty("routeKey").GetString());

        var content = routes.Single(route => route.GetProperty("route").GetString() == "/content/");
        Assert.Matches("^content:sha256:[0-9a-f]{64}$", content.GetProperty("contentKey").GetString());
        Assert.Equal("zh-CN", content.GetProperty("language").GetString());
        Assert.Equal("article", content.GetProperty("contentType").GetString());
        Assert.Equal("insights", content.GetProperty("collection").GetString());
        Assert.Equal(DateTimeOffset.Parse("2026-08-01T00:00:00Z"), content.GetProperty("publishedAt").GetDateTimeOffset());
        Assert.Equal(DateTimeOffset.Parse("2026-08-02T00:00:00Z"), content.GetProperty("updatedAt").GetDateTimeOffset());

        var staticRoute = routes.Single(route => route.GetProperty("route").GetString() == "/static/");
        Assert.Equal(JsonValueKind.Null, staticRoute.GetProperty("contentKey").ValueKind);
        Assert.Equal(JsonValueKind.Null, staticRoute.GetProperty("language").ValueKind);
        Assert.Equal(JsonValueKind.Null, staticRoute.GetProperty("contentType").ValueKind);
        Assert.Equal(JsonValueKind.Null, staticRoute.GetProperty("collection").ValueKind);
        Assert.Equal(JsonValueKind.Null, staticRoute.GetProperty("publishedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, staticRoute.GetProperty("updatedAt").ValueKind);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_outputDir, recursive: true);
    }

    private void WriteOutput(string relativePath)
    {
        var path = Path.Combine(_outputDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<!doctype html><html><head></head><body></body></html>");
    }

    private static SeoIndexEntry Entry(
        string route,
        string canonical,
        string? sourceItemId = null,
        bool isDerived = false)
        => new(
            new RouteInfo(route, route.Trim('/').Replace('/', Path.DirectorySeparatorChar) + "/index.html", "pages/page.html"),
            canonical,
            Robots: null,
            Indexable: true,
            DateTimeOffset.Parse("2026-08-03T00:00:00Z"),
            sourceItemId,
            ContentType: isDerived ? "list" : "article",
            IsDerived: isDerived,
            Collection: isDerived ? "derived" : "entry");

    private static SeoModel Model(string canonical)
        => new()
        {
            Title = "Route",
            Description = "Route description",
            Canonical = canonical
        };

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = string.Empty,
                BaseUrl = "/",
                Language = "zh-CN"
            },
            Content = TestContent.Markdown()
        };

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
