using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DefaultSearchIndexBuilderTests
{
    private static ContentItem CreateItem(string id, string title, string slug)
    {
        return new ContentItem(
            id,
            title,
            slug,
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, object>(),
            null,
            null);
    }

    private static RouteInfo CreateRoute(string url, string outputPath)
    {
        return new RouteInfo(url, outputPath, "post");
    }

    private static BuildVariantResult CreateVariantResult(string language, string baseUrl, List<(ContentItem, RouteInfo)> routed)
    {
        return new BuildVariantResult(
            language,
            "/tmp/output",
            baseUrl,
            false,
            NullContentBodyStore.Instance,
            routed,
            Array.Empty<(ContentItem, RouteInfo)>(),
            Array.Empty<(RouteInfo, DateTimeOffset)>(),
            new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Bukit.Rendering.SeoModel>(),
            Array.Empty<PluginExecutionInfo>(),
            0,
            0,
            new Dictionary<string, int>(),
            Bukit.Engine.BuildStageMetrics.Empty);
    }

    [Fact]
    public void GenerateMergedSearchIndex_WithBasicItems_GeneratesIndex()
    {
        var builder = new DefaultSearchIndexBuilder();
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_sbi_test_" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = CreateVariantResult("en", "https://example.com", new List<(ContentItem, RouteInfo)>
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
            var result = CreateVariantResult("en", "https://example.com", new List<(ContentItem, RouteInfo)>());

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
            var result = CreateVariantResult("en", "https://example.com", new List<(ContentItem, RouteInfo)>
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
