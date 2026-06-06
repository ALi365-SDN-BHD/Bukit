using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DefaultSearchIndexBuilderTests
{
    private static ContentDocument CreateItem(string id, string title, string slug)
    {
        return ContentDocument.Create(
            id,
            title,
            slug,
            DateTimeOffset.UtcNow,
            null,
            null);
    }

    private static RouteInfo CreateRoute(string url, string outputPath)
    {
        return new RouteInfo(url, outputPath, "post");
    }

    private static BuildVariantResult CreateVariantResult(string language, string baseUrl, List<(ContentDocument, RouteInfo)> routed)
    {
        return new BuildVariantResult(
            Language: language,
            OutputDir: "/tmp/output",
            BaseUrl: baseUrl,
            SearchSnippetsEnabled: false,
            BodyStore: NullContentBodyStore.Instance,
            DerivedRoutes: Array.Empty<(RouteInfo, DateTimeOffset)>(),
            SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            SeoModels: new Dictionary<string, Bukit.Rendering.SeoModel>(),
            PluginExecutions: Array.Empty<PluginExecutionInfo>(),
            RenderedCount: 0,
            SkippedCount: 0,
            RenderReasons: new Dictionary<string, int>(),
            StageMetrics: Bukit.Engine.BuildStageMetrics.Empty,
            RoutedDocuments: routed.ToRoutedDocuments());
    }

    [Fact]
    public void GenerateMergedSearchIndex_WithBasicItems_GeneratesIndex()
    {
        var builder = new DefaultSearchIndexBuilder();
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_sbi_test_" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = CreateVariantResult("en", "https://example.com", new List<(ContentDocument, RouteInfo)>
            {
                (CreateItem("1", "Hello", "hello"), CreateRoute("/hello", "hello/index.html")),
                (CreateItem("2", "World", "world"), CreateRoute("/world", "world/index.html")),
            });

            builder.GenerateMergedSearchIndex(tempDir, new[] { result }, false);

            var indexPath = Path.Combine(tempDir, "search.json");
            Assert.True(File.Exists(indexPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GenerateMergedSearchIndex_WithEmptyItems_DoesNotThrow()
    {
        var builder = new DefaultSearchIndexBuilder();
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_sbi_empty_" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = CreateVariantResult("en", "https://example.com", new List<(ContentDocument, RouteInfo)>());

            builder.GenerateMergedSearchIndex(tempDir, new[] { result }, false);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GenerateSearchIndexIndex_WithResults_CreatesIndexFile()
    {
        var builder = new DefaultSearchIndexBuilder();
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_sbi_idx_" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = CreateVariantResult("en", "https://example.com", new List<(ContentDocument, RouteInfo)>
            {
                (CreateItem("1", "Hello", "hello"), CreateRoute("/hello", "hello/index.html")),
            });

            builder.GenerateSearchIndexIndex(tempDir, new[] { result });

            var indexPath = Path.Combine(tempDir, "search.index.json");
            Assert.True(File.Exists(indexPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
