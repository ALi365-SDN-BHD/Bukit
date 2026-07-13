using System.Globalization;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildReporterTests
{
    [Fact]
    public void WriteIfEnabled_WritesBuildReportWithCoreFields()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var reportPath = Path.Combine(tempDir, ".bukit", "build-report.json");
        Assert.True(File.Exists(reportPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        var root = doc.RootElement;
        AssertArtifactContract(root, "https://bukit.dev/schemas/build-report.v1.json");
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("startedAt", out _));
        Assert.True(root.TryGetProperty("endedAt", out _));
        Assert.True(root.GetProperty("durationMs").GetInt64() >= 0);
        Assert.Equal("sources", root.GetProperty("project").GetProperty("contentSource").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("pageCount").GetInt32());
    }

    [Fact]
    public void WriteIfEnabled_WritesRoutesInStableOrder()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var routesPath = Path.Combine(tempDir, ".bukit", "routes.json");
        Assert.True(File.Exists(routesPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(routesPath));
        AssertArtifactContract(doc.RootElement, "https://bukit.dev/schemas/routes.v1.json");
        var routes = doc.RootElement.GetProperty("routes");
        Assert.Equal("/archive/2024/", routes[0].GetProperty("url").GetString());
        Assert.Equal("/blog/alpha/", routes[1].GetProperty("url").GetString());
        Assert.Equal("blog/alpha/index.html", routes[1].GetProperty("outputPath").GetString());
        Assert.Equal("pages/post.html", routes[1].GetProperty("template").GetString());
        Assert.Equal("post", routes[1].GetProperty("kind").GetString());
        Assert.Equal("zh-CN", routes[1].GetProperty("language").GetString());
        Assert.Equal("/blog/zeta/", routes[2].GetProperty("url").GetString());
    }

    [Fact]
    public void WriteIfEnabled_IncludesDerivedRoutes()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var routesPath = Path.Combine(tempDir, ".bukit", "routes.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(routesPath));
        var routes = doc.RootElement.GetProperty("routes").EnumerateArray().ToList();
        var archiveRoute = Assert.Single(routes, route => route.GetProperty("url").GetString() == "/archive/2024/");
        Assert.Equal("archive/2024/index.html", archiveRoute.GetProperty("outputPath").GetString());
        Assert.Equal("pages/archive.html", archiveRoute.GetProperty("template").GetString());
        Assert.Equal("derived", archiveRoute.GetProperty("kind").GetString());
        Assert.Equal("zh-CN", archiveRoute.GetProperty("language").GetString());
    }

    [Fact]
    public void WriteIfEnabled_WritesAssetsWithHashAndSize()
    {
        var tempDir = CreateTempDir();
        var assetsDir = Path.Combine(tempDir, "assets", "css");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "main.css"), "body{color:#111}");
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var assetsPath = Path.Combine(tempDir, ".bukit", "assets.json");
        Assert.True(File.Exists(assetsPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(assetsPath));
        AssertArtifactContract(doc.RootElement, "https://bukit.dev/schemas/assets.v1.json");
        var asset = doc.RootElement.GetProperty("assets")[0];
        Assert.Equal("assets/css/main.css", asset.GetProperty("path").GetString());
        Assert.Equal("assets/css/main.css", asset.GetProperty("source").GetString());
        Assert.True(asset.GetProperty("size").GetInt64() > 0);
        Assert.StartsWith("sha256:", asset.GetProperty("hash").GetString());
    }

    [Fact]
    public void WriteIfEnabled_WritesIncrementalManifest()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var manifestPath = Path.Combine(tempDir, ".bukit", "incremental-manifest.json");
        Assert.True(File.Exists(manifestPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;
        AssertArtifactContract(root, "https://bukit.dev/schemas/incremental-manifest.v1.json");
        Assert.True(root.GetProperty("enabled").GetBoolean());
        Assert.Equal(0, root.GetProperty("cacheHitCount").GetInt32());
        Assert.Equal(1, root.GetProperty("cacheMissCount").GetInt32());
        Assert.Equal(1, root.GetProperty("renderReasons").GetProperty("full_render").GetInt32());
        var variantElement = Assert.Single(root.GetProperty("variants").EnumerateArray());
        Assert.Equal("zh-CN", variantElement.GetProperty("language").GetString());
    }

    [Fact]
    public void WriteIfEnabled_WritesArtifactManifestWithHashes()
    {
        var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "index.html"), "<html>home</html>");
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var manifestPath = Path.Combine(tempDir, ".bukit", "artifact-manifest.json");
        Assert.True(File.Exists(manifestPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;
        AssertArtifactContract(root, "https://bukit.dev/schemas/artifact-manifest.v1.json");
        Assert.StartsWith("sha256:", root.GetProperty("artifactSetHash").GetString());
        var artifacts = root.GetProperty("artifacts").EnumerateArray().ToList();
        Assert.Equal(root.GetProperty("artifactCount").GetInt32(), artifacts.Count);
        Assert.Contains(artifacts, x => x.GetProperty("path").GetString() == "build-report.json");
        Assert.Contains(artifacts, x => x.GetProperty("path").GetString() == "routes.json");
        Assert.Contains(artifacts, x => x.GetProperty("path").GetString() == "assets.json");
        Assert.Contains(artifacts, x => x.GetProperty("path").GetString() == "incremental-manifest.json");
        Assert.Contains(artifacts, x => x.GetProperty("path").GetString() == "release-bundle-checksums.json");
        Assert.Contains(artifacts, x => x.GetProperty("path").GetString() == "security-report.json");
        Assert.All(artifacts, x => Assert.StartsWith("sha256:", x.GetProperty("hash").GetString()));
    }

    [Fact]
    public void WriteIfEnabled_WritesBuildManifestDigest()
    {
        var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "index.html"), "<html>home</html>");
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var digestPath = Path.Combine(tempDir, ".bukit", "build-manifest-digest.json");
        Assert.True(File.Exists(digestPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(digestPath));
        var root = doc.RootElement;
        AssertArtifactContract(root, "https://bukit.dev/schemas/build-manifest-digest.v1.json");
        Assert.StartsWith("sha256:", root.GetProperty("reportSetHash").GetString());
        Assert.True(root.GetProperty("reportCount").GetInt32() >= 3);
        var reports = root.GetProperty("reports").EnumerateArray().ToList();
        Assert.Contains(reports, x => x.GetProperty("path").GetString() == "artifact-manifest.json");
        Assert.Contains(reports, x => x.GetProperty("path").GetString() == "release-bundle-checksums.json");
        Assert.Contains(reports, x => x.GetProperty("path").GetString() == "security-report.json");
    }

    [Fact]
    public void WriteIfEnabled_WritesReleaseBundleChecksums()
    {
        var tempDir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(tempDir, "assets", "css"));
        File.WriteAllText(Path.Combine(tempDir, "index.html"), "<html>home</html>");
        File.WriteAllText(Path.Combine(tempDir, "assets", "css", "main.css"), "body{color:#111}");
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var checksumsPath = Path.Combine(tempDir, ".bukit", "release-bundle-checksums.json");
        Assert.True(File.Exists(checksumsPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(checksumsPath));
        var root = doc.RootElement;
        AssertArtifactContract(root, "https://bukit.dev/schemas/release-bundle-checksums.v1.json");
        Assert.StartsWith("sha256:", root.GetProperty("bundleHash").GetString());
        var files = root.GetProperty("files").EnumerateArray().ToList();
        Assert.Equal(root.GetProperty("fileCount").GetInt32(), files.Count);
        Assert.Contains(files, x => x.GetProperty("path").GetString() == "index.html");
        Assert.Contains(files, x => x.GetProperty("path").GetString() == "assets/css/main.css");
        Assert.DoesNotContain(files, x => x.GetProperty("path").GetString()!.StartsWith(".bukit/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WriteIfEnabled_WritesSecurityReport()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        var securityPath = Path.Combine(tempDir, ".bukit", "security-report.json");
        Assert.True(File.Exists(securityPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(securityPath));
        var root = doc.RootElement;
        AssertArtifactContract(root, "https://bukit.dev/schemas/security-report.v1.json");
        Assert.Equal("warning", root.GetProperty("status").GetString());
        var routeTraversal = root.GetProperty("checks").GetProperty("routeTraversal");
        Assert.Equal("not_checked", routeTraversal.GetProperty("status").GetString());
        Assert.Equal("error", routeTraversal.GetProperty("severity").GetString());
        Assert.Equal(1, root.GetProperty("warnings").GetArrayLength());
        Assert.Equal(0, root.GetProperty("errors").GetArrayLength());
        Assert.False(root.TryGetProperty("externalPlugins", out _));
    }

    [Fact]
    public void WriteIfEnabled_WithSecurityData_WritesRealCheckResults()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: true);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);
        var securityData = BuildReporter.CreateSecurityReportData(config, tempDir, tempDir, new[] { variant });

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error), securityData);

        var securityPath = Path.Combine(tempDir, ".bukit", "security-report.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(securityPath));
        var root = doc.RootElement;
        Assert.Equal("passed", root.GetProperty("status").GetString());
        var checks = root.GetProperty("checks");
        Assert.Equal("passed", checks.GetProperty("routeTraversal").GetProperty("status").GetString());
        Assert.Equal("passed", checks.GetProperty("unsafeSlug").GetProperty("status").GetString());
        Assert.Equal("not_applicable", checks.GetProperty("pluginOutputPath").GetProperty("status").GetString());
        Assert.Equal("not_applicable", checks.GetProperty("remoteThemeLock").GetProperty("status").GetString());
        Assert.Equal("not_applicable", checks.GetProperty("publicOutputPrivacy").GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("warnings").GetArrayLength());
        Assert.Equal(0, root.GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public void CreateSecurityReportData_FailsWhenPublicOutputContainsKnownNotionIdentifiersOrProviderMarkers()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        var tempDir = CreateTempDir();
        var publicDir = Path.Combine(tempDir, "content");
        var internalDir = Path.Combine(tempDir, ".bukit");
        Directory.CreateDirectory(publicDir);
        Directory.CreateDirectory(internalDir);
        File.WriteAllText(
            Path.Combine(publicDir, "leak.json"),
            $$"""{"id":"posts:{{notionId}}","source":"notion"}""");
        File.WriteAllText(
            Path.Combine(internalDir, "publish-audit-report.json"),
            $$"""{"sourceItemId":"posts:{{notionId}}","source":"notion"}""");

        var config = CreateConfig(enabled: true) with { Content = TestContent.Notion(notionId) };
        var variant = CreateVariant(tempDir) with { ContentGraph = CreateNotionGraph(notionId) };

        var data = BuildReporter.CreateSecurityReportData(config, tempDir, tempDir, [variant]);

        Assert.Equal("failed", data.PublicOutputPrivacy);
        var error = Assert.Single(data.Errors, value => value.Contains("content/leak.json", StringComparison.Ordinal));
        Assert.DoesNotContain(notionId, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateSecurityReportData_IgnoresInternalReportsAndUnrelatedBusinessUuid()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        const string businessId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        var tempDir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(tempDir, ".bukit"));
        File.WriteAllText(Path.Combine(tempDir, "public.json"), $$"""{"businessId":"{{businessId}}"}""");
        File.WriteAllText(
            Path.Combine(tempDir, ".bukit", "seo-report.json"),
            $$"""{"sourceItemId":"posts:{{notionId}}","source":"notion"}""");

        var config = CreateConfig(enabled: true) with { Content = TestContent.Notion(notionId) };
        var variant = CreateVariant(tempDir) with { ContentGraph = CreateNotionGraph(notionId) };

        var data = BuildReporter.CreateSecurityReportData(config, tempDir, tempDir, [variant]);

        Assert.Equal("passed", data.PublicOutputPrivacy);
        Assert.Empty(data.Errors);
    }

    [Fact]
    public void CreateSecurityReportData_DetectsCompactKnownIdentifierWithoutProviderMarker()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "public.txt"), notionId.Replace("-", string.Empty, StringComparison.Ordinal));
        var config = CreateConfig(enabled: true) with { Content = TestContent.Notion(notionId) };
        var variant = CreateVariant(tempDir) with { ContentGraph = CreateNotionGraph(notionId) };

        var data = BuildReporter.CreateSecurityReportData(config, tempDir, tempDir, [variant]);

        Assert.Equal("failed", data.PublicOutputPrivacy);
        Assert.Contains(data.Errors, error => error.Contains("public.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateSecurityReportData_DetectsStructuredProviderMarkersAndRedactsUuidPath()
    {
        const string unknownUuid = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, unknownUuid + ".json"), """{"source":"not\u0069on"}""");
        File.WriteAllText(Path.Combine(tempDir, "metadata.yaml"), "sourceKey: notion\n");
        var config = CreateConfig(enabled: true) with { Content = TestContent.Notion("db") };

        var data = BuildReporter.CreateSecurityReportData(config, tempDir, tempDir, [CreateVariant(tempDir)]);

        Assert.Equal("failed", data.PublicOutputPrivacy);
        Assert.Contains(data.Errors, error => error.Contains("[redacted-notion-id].json", StringComparison.Ordinal));
        Assert.Contains(data.Errors, error => error.Contains("metadata.yaml", StringComparison.Ordinal));
        Assert.DoesNotContain(data.Errors, error => error.Contains(unknownUuid, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateSecurityReportData_DetectsRelatedEntityAndRelationIdentifiers()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        const string entityId = "aaaaaaaa-1111-4222-8333-bbbbbbbbbbbb";
        const string relationId = "cccccccc-4444-4555-8666-dddddddddddd";
        var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "entity.txt"), entityId);
        File.WriteAllText(Path.Combine(tempDir, "relation.txt"), relationId);
        var config = CreateConfig(enabled: true) with { Content = TestContent.Notion("db") };
        var variant = CreateVariant(tempDir) with
        {
            ContentGraph = CreateNotionGraph(notionId, entityId, relationId)
        };

        var data = BuildReporter.CreateSecurityReportData(config, tempDir, tempDir, [variant]);

        Assert.Equal("failed", data.PublicOutputPrivacy);
        Assert.Contains(data.Errors, error => error.Contains("entity.txt", StringComparison.Ordinal));
        Assert.Contains(data.Errors, error => error.Contains("relation.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteIfEnabled_WhenDisabled_WritesSecurityReportOnly()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: false);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        Assert.False(File.Exists(Path.Combine(tempDir, ".bukit", "build-report.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, ".bukit", "release-bundle-checksums.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, ".bukit", "security-report.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, ".bukit", "artifact-manifest.json")));
        Assert.True(File.Exists(Path.Combine(tempDir, ".bukit", "build-manifest-digest.json")));
    }

    [Fact]
    public void EnforceSecurityGate_AutoModeInCi_ThrowsOnFailedSecurityReport()
    {
        var config = CreateConfig(enabled: true);
        var securityData = new SecurityReportData(
            RouteTraversal: "failed",
            UnsafeSlug: "passed",
            PluginOutputPath: "not_applicable",
            RemoteThemeLock: "not_applicable",
            PublicOutputPrivacy: "not_applicable",
            Warnings: Array.Empty<string>(),
            Errors: new[] { "route traversal detected" });

        var ex = Assert.Throws<InvalidOperationException>(() => BuildReporter.EnforceSecurityGate(config, securityData, isCi: true));
        Assert.Contains("BKT-BUILD-SECURITY-0001", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforceSecurityGate_StrictModeOutsideCi_ThrowsOnFailedSecurityReport()
    {
        var config = CreateConfig(enabled: true) with
        {
            Build = CreateConfig(enabled: true).Build with
            {
                Report = new BuildReportConfig { Enabled = true, SecurityFailMode = "strict" }
            }
        };
        var securityData = new SecurityReportData(
            RouteTraversal: "failed",
            UnsafeSlug: "passed",
            PluginOutputPath: "not_applicable",
            RemoteThemeLock: "not_applicable",
            PublicOutputPrivacy: "not_applicable",
            Warnings: Array.Empty<string>(),
            Errors: new[] { "route traversal detected" });

        Assert.Throws<InvalidOperationException>(() => BuildReporter.EnforceSecurityGate(config, securityData, isCi: false));
    }

    [Fact]
    public void EnforceSecurityGate_WarnMode_DoesNotThrow()
    {
        var config = CreateConfig(enabled: true) with
        {
            Build = CreateConfig(enabled: true).Build with
            {
                Report = new BuildReportConfig { Enabled = true, SecurityFailMode = "warn" }
            }
        };
        var securityData = new SecurityReportData(
            RouteTraversal: "failed",
            UnsafeSlug: "passed",
            PluginOutputPath: "not_applicable",
            RemoteThemeLock: "not_applicable",
            PublicOutputPrivacy: "not_applicable",
            Warnings: Array.Empty<string>(),
            Errors: new[] { "route traversal detected" });

        var ex = Record.Exception(() => BuildReporter.EnforceSecurityGate(config, securityData, isCi: true));
        Assert.Null(ex);
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void AssertArtifactContract(JsonElement root, string expectedSchema)
    {
        Assert.Equal(expectedSchema, root.GetProperty("schema").GetString());
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
    }

    private static AppConfig CreateConfig(bool enabled)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "bukit",
                Title = "Bukit",
                BaseUrl = "/",
                Language = "zh-CN"
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig
            {
                Output = "dist",
                Report = new BuildReportConfig
                {
                    Enabled = enabled
                }
            }
        };
    }

    private static BuildResult CreateResult(AppConfig config, string tempDir, BuildVariantResult variant)
    {
        return BuildResultFactory.Create(
            config,
            tempDir,
            tempDir,
            new ConfigOverrides(),
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow,
            1000,
            new[] { variant });
    }

    private static BuildVariantResult CreateVariant(string tempDir)
    {
        var alpha = ContentDocument.Create(
            "alpha",
            "Alpha",
            "alpha",
            DateTimeOffset.UtcNow,
            "<p>Alpha</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post"
            }));
        var zeta = ContentDocument.Create(
            "zeta",
            "Zeta",
            "zeta",
            DateTimeOffset.UtcNow,
            "<p>Zeta</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post"
            }));

        var routed = new[]
        {
            (zeta, new RouteInfo("/blog/zeta/", "blog/zeta/index.html", "pages/post.html")),
            (alpha, new RouteInfo("/blog/alpha/", "blog/alpha/index.html", "pages/post.html"))
        };
        var archive = ContentDocument.Create(
            "archive-2024",
            "Archive 2024",
            "archive-2024",
            DateTimeOffset.Parse("2024-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "derived",
                ["collection"] = "derived"
            }));

        return new BuildVariantResult(
            Language: "zh-CN",
            OutputDir: tempDir,
            BaseUrl: "/",
            SearchSnippetsEnabled: false,
            BodyStore: EmptyContentBodyStore.Instance,
            RoutedDocuments: routed.ToRoutedDocuments(),
            DerivedRoutes: new[]
            {
                (new RouteInfo("/archive/2024/", "archive/2024/index.html", "pages/archive.html"), DateTimeOffset.Parse("2024-01-01T00:00:00Z", CultureInfo.InvariantCulture))
            },
            SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            SeoModels: new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase),
            PluginExecutions: new List<PluginExecutionInfo>(),
            RenderedCount: 1,
            SkippedCount: 0,
            RenderReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["full_render"] = 1
            },
            StageMetrics: new BuildStageMetrics(
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)),
            DerivedDocuments: new[]
            {
                new RoutedContentDocument(
                    archive,
                    new RouteInfo("/archive/2024/", "archive/2024/index.html", "pages/archive.html"),
                    DateTimeOffset.Parse("2024-01-01T00:00:00Z", CultureInfo.InvariantCulture))
            });
    }

    private static CanonicalContentGraph CreateNotionGraph(string notionId, string? entityId = null, string? relationId = null)
    {
        var record = new ContentRecord(
            new ContentIdentity($"posts:{notionId}", "safe-route", notionId, "post", "published"),
            new ContentPresentation("Safe title", null, null, "en", []),
            new ContentClassification("post", "posts", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-07-13T00:00:00Z", CultureInfo.InvariantCulture), null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "published", []),
            entityId is null ? [] : [new EntityRecord("page", entityId)],
            relationId is null ? [] : [new ContentRelation("related", relationId)],
            []);

        return new CanonicalContentGraph([record], [], [], []);
    }
}
