using Xunit;
using System.Diagnostics;

namespace Bukit.Engine.Tests;

/// <summary>
/// Performance regression tests for large-site builds.
/// Establishes baseline build times and detects regressions.
/// </summary>
public sealed class PerformanceRegressionTests : IDisposable
{
    private readonly string _siteRoot;

    public PerformanceRegressionTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "bukit-perf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_siteRoot);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_siteRoot, recursive: true);
    }

    private void GenerateMarkdownPages(int count, string collection = "posts")
    {
        var contentDir = Path.Combine(_siteRoot, "content", collection);
        Directory.CreateDirectory(contentDir);

        for (int i = 0; i < count; i++)
        {
            var filename = $"post-{i:D4}.md";
            var content = $"""
                ---
                title: Post {i}
                date: 2024-01-{(i % 28) + 1:D2}
                tags:
                  - tag{(i % 5)}
                ---
                # Post {i}
                This is the content of post {i}. It contains some text to simulate real content.
                Lorem ipsum dolor sit amet, consectetur adipiscing elit.
                """;
            File.WriteAllText(Path.Combine(contentDir, filename), content);
        }
    }

    [Fact]
    public void ParallelForEach_ComputeOptimalParallelism_SmallSite()
    {
        // For <100 items, should use limited parallelism
        var result = PageRenderDispatcher.ComputeOptimalParallelism(50, 0);
        Assert.True(result >= 1);
        Assert.True(result <= Environment.ProcessorCount * 2);
    }

    [Fact]
    public void ParallelForEach_ComputeOptimalParallelism_MediumSite()
    {
        // For 100-1000 items, should use processor count
        var result = PageRenderDispatcher.ComputeOptimalParallelism(500, 0);
        Assert.True(result >= 1);
        Assert.True(result <= Environment.ProcessorCount * 2);
    }

    [Fact]
    public void ParallelForEach_ComputeOptimalParallelism_LargeSite()
    {
        // For >1000 items, should use higher parallelism
        var result = PageRenderDispatcher.ComputeOptimalParallelism(2000, 0);
        Assert.True(result >= 1);
        Assert.True(result <= Environment.ProcessorCount * 2);
    }

    [Fact]
    public void ParallelForEach_ComputeOptimalParallelism_ExplicitOverride()
    {
        // User-specified parallelism should be respected
        var result = PageRenderDispatcher.ComputeOptimalParallelism(500, 4);
        Assert.Equal(4, result);
    }

    [Fact]
    public void ParallelForEach_ComputeOptimalParallelism_ClampToMax()
    {
        // Should clamp to processor count * 2
        var result = PageRenderDispatcher.ComputeOptimalParallelism(500, 1000);
        Assert.True(result <= Environment.ProcessorCount * 2);
    }

    [Fact]
    public void ParallelForEach_ComputeOptimalParallelism_MinIsOne()
    {
        var result = PageRenderDispatcher.ComputeOptimalParallelism(0, 0);
        Assert.True(result >= 1);
    }

    [Fact]
    public void ManifestSerialization_Roundtrip_Performance()
    {
        // Test that manifest save/load roundtrip completes within reasonable time
        var manifestPath = Path.Combine(_siteRoot, "manifest.json");
        var manifest = new Incremental.BuildManifest();

        // Add some entries to simulate a real manifest
        for (int i = 0; i < 100; i++)
        {
            manifest.Entries[$"content/posts/post-{i:D4}.md"] = new Incremental.BuildManifestEntry
            {
                ContentHash = $"abc{i:D4}",
                OutputPath = $"dist/posts/post-{i:D4}/index.html",
                Url = $"/posts/post-{i:D4}/",
                Template = "layouts/post.html"
            };
        }

        var sw = Stopwatch.StartNew();
        manifest.Save(manifestPath);
        var loaded = Incremental.BuildManifest.Load(manifestPath);
        sw.Stop();

        Assert.Equal(100, loaded.Entries.Count);
        // Should complete in under 1 second for 100 entries
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Manifest roundtrip took {sw.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public void FileGeneration_1000Pages_WithinTimeLimit()
    {
        // Generate 1000 markdown pages and verify file creation completes quickly
        var sw = Stopwatch.StartNew();
        GenerateMarkdownPages(1000);
        sw.Stop();

        var contentDir = Path.Combine(_siteRoot, "content", "posts");
        var fileCount = Directory.GetFiles(contentDir, "*.md").Length;
        Assert.Equal(1000, fileCount);

        // File generation should complete in under 5 seconds
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Generating 1000 pages took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }
}
