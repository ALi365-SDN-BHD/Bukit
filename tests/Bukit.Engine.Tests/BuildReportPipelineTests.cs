using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildReportPipelineTests
{
    [Fact]
    public void Execute_ReturnsBuildVariantResultWithCorrectStructure()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-report-pipeline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        var logger = new RecordingLogger();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", Language = "en" },
            Content = new ContentConfig { Provider = "markdown" }
        };
        var pipeline = new BuildReportPipeline();
        var stageMetrics = new BuildStageMetricsCollector().Snapshot();

        var result = pipeline.Execute(new BuildReportPipelineContext(
            Config: config,
            Language: "en",
            OutputDir: outputDir,
            BaseUrl: "/",
            SearchSnippetsEnabled: false,
            BodyStore: EmptyContentBodyStore.Instance,
            Routed: new List<(ContentItem, RouteInfo)>(),
            DerivedRouted: new List<(ContentItem, RouteInfo)>(),
            DerivedRoutes: new List<(RouteInfo, DateTimeOffset)>(),
            SeoIndex: new Dictionary<string, SeoIndexEntry>(),
            SeoModels: new Dictionary<string, SeoModel>(),
            PluginExecutions: new List<PluginExecutionInfo>(),
            RenderedCount: 1,
            SkippedCount: 0,
            RenderReasons: new Dictionary<string, int>(),
            StageMetrics: stageMetrics,
            Logger: logger,
            DefaultLanguage: null));

        Assert.Equal("en", result.Language);
        Assert.Equal(outputDir, result.OutputDir);
        Assert.Equal(1, result.RenderedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Same(stageMetrics, result.StageMetrics);
    }

    private sealed class RecordingLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
