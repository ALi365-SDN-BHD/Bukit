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
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("startedAt", out _));
        Assert.True(root.TryGetProperty("endedAt", out _));
        Assert.True(root.GetProperty("durationMs").GetInt64() >= 0);
        Assert.Equal("markdown", root.GetProperty("project").GetProperty("contentSource").GetString());
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
        Assert.True(root.GetProperty("enabled").GetBoolean());
        Assert.Equal(0, root.GetProperty("cacheHitCount").GetInt32());
        Assert.Equal(1, root.GetProperty("cacheMissCount").GetInt32());
        Assert.Equal(1, root.GetProperty("renderReasons").GetProperty("full_render").GetInt32());
        var variantElement = Assert.Single(root.GetProperty("variants").EnumerateArray());
        Assert.Equal("zh-CN", variantElement.GetProperty("language").GetString());
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
        Assert.Equal("passed", root.GetProperty("status").GetString());
        var routeTraversal = root.GetProperty("checks").GetProperty("routeTraversal");
        Assert.Equal("passed", routeTraversal.GetProperty("status").GetString());
        Assert.Equal("error", routeTraversal.GetProperty("severity").GetString());
        Assert.Equal(0, root.GetProperty("warnings").GetArrayLength());
        Assert.Equal(0, root.GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public void WriteIfEnabled_WhenDisabled_DoesNotCreateBuildReport()
    {
        var tempDir = CreateTempDir();
        var config = CreateConfig(enabled: false);
        var variant = CreateVariant(tempDir);
        var result = CreateResult(config, tempDir, variant);

        BuildReporter.WriteIfEnabled(config, tempDir, tempDir, result, new[] { variant }, new ConsoleLogger(LogLevel.Error));

        Assert.False(File.Exists(Path.Combine(tempDir, ".bukit", "build-report.json")));
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
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
            Content = new ContentConfig
            {
                Provider = "markdown"
            },
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
        var alpha = new ContentItem(
            "alpha",
            "Alpha",
            "alpha",
            DateTimeOffset.UtcNow,
            "<p>Alpha</p>",
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["collection"] = new("text", "post")
            });
        var zeta = new ContentItem(
            "zeta",
            "Zeta",
            "zeta",
            DateTimeOffset.UtcNow,
            "<p>Zeta</p>",
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["collection"] = new("text", "post")
            });

        return new BuildVariantResult(
            Language: "zh-CN",
            OutputDir: tempDir,
            BaseUrl: "/",
            SearchSnippetsEnabled: false,
            BodyStore: EmptyContentBodyStore.Instance,
            Routed: new[]
            {
                (zeta, new RouteInfo("/blog/zeta/", "blog/zeta/index.html", "pages/post.html")),
                (alpha, new RouteInfo("/blog/alpha/", "blog/alpha/index.html", "pages/post.html"))
            },
            DerivedRouted: Array.Empty<(ContentItem Item, RouteInfo Route)>(),
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
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)));
    }
}
