using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class AssetPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_CopiesStaticAndAssetsAndGeneratesTokensAndAggregatesMetrics()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-asset-pipeline-tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(rootDir, "dist");
        var staticDir = Path.Combine(rootDir, "static");
        var assetsDir = Path.Combine(rootDir, "assets");
        var themeRoot = Path.Combine(rootDir, "themes", "starter");
        var mediaDir = Path.Combine(rootDir, ".cache", "media");

        Directory.CreateDirectory(staticDir);
        Directory.CreateDirectory(assetsDir);
        Directory.CreateDirectory(themeRoot);
        Directory.CreateDirectory(mediaDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(staticDir, "robots.txt"), "User-agent: *\nDisallow:");
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "body { color: red; }");
        File.WriteAllText(Path.Combine(mediaDir, "photo.jpg"), "fake-image");

        var tokensYaml = "colors:\n  primary: \"#000\"\nfont:\n  base: Arial\n";
        File.WriteAllText(Path.Combine(themeRoot, "tokens.yaml"), tokensYaml);

        var manifest = new BuildManifest();
        var logger = new RecordingLogger();
        var pipeline = new AssetPipeline();
        var config = new AppConfig
        {
            Build = new BuildConfig { AssetHashMode = "sha256" },
            Theme = new ThemeConfig
            {
                Name = "starter",
                Scss = new ScssConfig { Enabled = false },
                Images = new ImageOptimizationConfig { Enabled = false }
            },
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig { Provider = "markdown" }
        };

        var result = await pipeline.ExecuteAsync(new AssetPipelineContext(
            StaticDir: staticDir,
            ParentStaticDir: null,
            AssetsDir: assetsDir,
            ParentAssetsDir: null,
            MediaDownloadDir: mediaDir,
            ThemeRoot: themeRoot,
            ParentThemeRoot: null,
            OutputDir: outputDir,
            BaseUrl: "/",
            Renderer: null,
            SiteModel: new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "en" },
            StaticTemplate: null,
            Manifest: manifest,
            IncrementalEnabled: false,
            AssetHashMode: "sha256",
            ScssConfig: null,
            ImageConfig: null,
            Logger: logger,
            CurrentKeys: new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal)),
            CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(outputDir, "robots.txt")), "robots.txt not found");
        Assert.True(File.Exists(Path.Combine(outputDir, "assets", "css", "main.css")), "assets/css/main.css not found");
        Assert.True(File.Exists(Path.Combine(outputDir, "assets", "css", "theme-tokens.css")), "theme tokens not found");
        Assert.True(File.Exists(Path.Combine(outputDir, "assets", "uploads", "photo.jpg")), "media photo not found");
        Assert.Contains(logger.Infos, m => m.StartsWith("event=tokens.generated"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("staticSync"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("assetsSync"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("tokensGen"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("mediaCopy"));
    }

    [Fact]
    public async Task ExecuteAsync_WithParentStaticSyncsParentFirst()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-asset-pipeline-tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(rootDir, "dist");
        var parentStaticDir = Path.Combine(rootDir, "themes", "parent", "static");

        Directory.CreateDirectory(parentStaticDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(parentStaticDir, "favicon.ico"), "ico");

        var manifest = new BuildManifest();
        var logger = new RecordingLogger();
        var pipeline = new AssetPipeline();

        await pipeline.ExecuteAsync(new AssetPipelineContext(
            StaticDir: null,
            ParentStaticDir: parentStaticDir,
            AssetsDir: null,
            ParentAssetsDir: null,
            MediaDownloadDir: null,
            ThemeRoot: null,
            ParentThemeRoot: null,
            OutputDir: outputDir,
            BaseUrl: "/",
            Renderer: null,
            SiteModel: new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "en" },
            StaticTemplate: null,
            Manifest: manifest,
            IncrementalEnabled: false,
            AssetHashMode: "sha256",
            ScssConfig: null,
            ImageConfig: null,
            Logger: logger,
            CurrentKeys: new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal)),
            CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(outputDir, "favicon.ico")));
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
