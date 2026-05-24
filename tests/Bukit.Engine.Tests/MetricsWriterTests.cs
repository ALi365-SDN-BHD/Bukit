using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MetricsWriterTests
{
    [Fact]
    public void WriteIfRequested_WritesStageDurationsAndCounts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var metricsPath = Path.Combine(tempDir, "metrics.json");

        var config = new AppConfig
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
            }
        };

        var variantMetrics = new BuildStageMetrics(
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["variantTotal"] = 120,
                ["renderPages"] = 80,
                ["renderSpecialLists"] = 30
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["contentHash"] = 3,
                ["bodyLoad"] = 2,
                ["pageRender"] = 1
            });

        var variant = new BuildVariantResult(
            Language: "zh-CN",
            OutputDir: tempDir,
            BaseUrl: "/",
            SearchSnippetsEnabled: false,
            BodyStore: EmptyContentBodyStore.Instance,
            Routed: Array.Empty<(ContentItem Item, RouteInfo Route)>(),
            DerivedRouted: Array.Empty<(ContentItem Item, RouteInfo Route)>(),
            DerivedRoutes: Array.Empty<(RouteInfo Route, DateTimeOffset LastModified)>(),
            SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            SeoModels: new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase),
            PluginExecutions: new List<PluginExecutionInfo>(),
            RenderedCount: 1,
            SkippedCount: 0,
            RenderReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["full_render"] = 1
            },
            StageMetrics: variantMetrics);

        MetricsWriter.WriteIfRequested(tempDir, metricsPath, config, tempDir, 1, new[] { variant });

        using var doc = JsonDocument.Parse(File.ReadAllText(metricsPath));
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("version").GetInt32());

        var stages = root.GetProperty("variants")[0].GetProperty("stages");
        Assert.Equal(120, stages.GetProperty("durationsMs").GetProperty("variantTotal").GetInt64());
        Assert.Equal(30, stages.GetProperty("durationsMs").GetProperty("renderSpecialLists").GetInt64());
        Assert.Equal(3, stages.GetProperty("counts").GetProperty("contentHash").GetInt32());
        Assert.Equal(2, stages.GetProperty("counts").GetProperty("bodyLoad").GetInt32());
        var htmlPath = Path.ChangeExtension(metricsPath, ".html");
        Assert.True(File.Exists(htmlPath));
        var html = File.ReadAllText(htmlPath);
        Assert.Contains("Bukit Build Report", html, StringComparison.Ordinal);
        Assert.Contains("full_render", html, StringComparison.Ordinal);
    }
}
