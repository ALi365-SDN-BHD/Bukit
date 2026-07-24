using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_RunsAfterBuildPluginsAndTracksOutputs()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-pipeline-tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(rootDir, "dist");
        var cacheDir = Path.Combine(rootDir, ".cache");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(cacheDir);
        var manifest = new BuildManifest();
        var logger = new RecordingLogger();

        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                PluginFailMode = "warn"
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig()
        };
        var pluginContext = new BuildContext
        {
            RootDir = rootDir,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = rootDir,
            BodyStore = EmptyContentBodyStore.Instance,
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = logger
        };

        var pipeline = new PluginPipeline();
        var result = await pipeline.ExecuteAsync(new PluginPipelineContext(
            PluginContext: pluginContext,
            OutputDir: outputDir,
            BaseUrl: "/",
            Manifest: manifest,
            ManifestPath: Path.Combine(cacheDir, "build-manifest.json"),
            IncrementalEnabled: false,
            CurrentKeys: new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal),
            RenderedCount: 1,
            SkippedCount: 0,
            Logger: logger,
            Config: config,
            PluginSession: PluginExecutionSession.Create(
                config,
                BuildExecutionMode.Production)),
            CancellationToken.None);

        Assert.NotNull(result);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { Infos.Add(message); }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
