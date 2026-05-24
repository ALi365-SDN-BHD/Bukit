using Bukit.Engine.Plugins.BuiltIn;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyFeedWriterTests : IDisposable
{
    private readonly string _tempDir;

    public TaxonomyFeedWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void WriteFeeds_WithTermPages_WritesFeedXml()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
            {
                Description = "All about tech"
            }
        };
        terms["tech"].Pages.Add(new TaxonomyPage("Post One", "/blog/one/", DateTimeOffset.UtcNow, "Summary one", null, false, null));
        terms["tech"].Pages.Add(new TaxonomyPage("Post Two", "/blog/two/", DateTimeOffset.UtcNow.AddDays(-1), "Summary two", null, false, null));

        TaxonomyFeedWriter.WriteFeeds(_tempDir, "https://example.com", "/", "My Site", terms, "tags");

        var feedPath = Path.Combine(_tempDir, "tags", "tech", "feed.xml");
        Assert.True(File.Exists(feedPath));

        var content = File.ReadAllText(feedPath);
        Assert.StartsWith("<?xml", content, StringComparison.Ordinal);
        Assert.Contains("<rss version=\"2.0\"", content, StringComparison.Ordinal);
        Assert.Contains("<title>My Site: Tech</title>", content, StringComparison.Ordinal);
        Assert.Contains("<description>All about tech</description>", content, StringComparison.Ordinal);
        Assert.Contains("<item>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteFeeds_EmptyTermPages_DoesNotWriteFeed()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
        };

        TaxonomyFeedWriter.WriteFeeds(_tempDir, "https://example.com", "/", "My Site", terms, "tags");

        var feedPath = Path.Combine(_tempDir, "tags", "tech", "feed.xml");
        Assert.False(File.Exists(feedPath));
    }

    [Fact]
    public void WriteFeeds_EmptyTerms_DoesNotThrow()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);

        TaxonomyFeedWriter.WriteFeeds(_tempDir, "https://example.com", "/", "My Site", terms, "tags");
    }
}
