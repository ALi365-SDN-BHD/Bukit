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
        terms["tech"].Pages.Add(new TaxonomyPage("post-one", "Post One", "/blog/one/", DateTimeOffset.UtcNow, "Summary one", null, false, null));
        terms["tech"].Pages.Add(new TaxonomyPage("post-two", "Post Two", "/blog/two/", DateTimeOffset.UtcNow.AddDays(-1), "Summary two", null, false, null));

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
    public void WriteFeeds_WithRoutePrefix_WritesFeedUnderConfiguredTaxonomyRoute()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
        };
        terms["tech"].Pages.Add(new TaxonomyPage("post-one", "Post One", "/blog/one/", DateTimeOffset.UtcNow, "Summary one", null, false, null));

        TaxonomyFeedWriter.WriteFeeds(_tempDir, "https://example.com", "/docs", "My Site", terms, "category", "/insights/category");

        var feedPath = Path.Combine(_tempDir, "insights", "category", "tech", "feed.xml");
        Assert.True(File.Exists(feedPath));

        var content = File.ReadAllText(feedPath);
        Assert.Contains("<link>https://example.com/docs/insights/category/tech/</link>", content, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.com/docs/insights/category/tech/feed.xml\"", content, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_tempDir, "category", "tech", "feed.xml")));
    }

    [Fact]
    public void WriteFeeds_RootBaseUrl_DoesNotProduceDoubleSlash()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["news"] = new TaxonomyTerm("News", "news")
        };
        terms["news"].Pages.Add(new TaxonomyPage("post-1", "Post One", "/blog/one/", DateTimeOffset.UtcNow, "Summary", null, false, null));

        TaxonomyFeedWriter.WriteFeeds(_tempDir, "https://example.test", "/", "My Site", terms, "tags");

        var feedPath = Path.Combine(_tempDir, "tags", "news", "feed.xml");
        var content = File.ReadAllText(feedPath);
        Assert.DoesNotContain("https://example.test//", content, StringComparison.Ordinal);
        Assert.Contains("https://example.test/blog/one/", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteFeeds_EmptyTerms_DoesNotThrow()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);

        TaxonomyFeedWriter.WriteFeeds(_tempDir, "https://example.com", "/", "My Site", terms, "tags");
    }

    [Fact]
    public void WriteFeeds_EqualTimestampWindow_IsInputOrderIndependent()
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        List<TaxonomyPage> BuildPages(bool reversed)
        {
            var pages = Enumerable.Range(0, 21)
                .Select(index => new TaxonomyPage(
                    $"post-{index:D2}",
                    $"Post {index:D2}",
                    $"/blog/post-{index:D2}/",
                    timestamp,
                    $"Summary {index:D2}",
                    null,
                    false,
                    null))
                .ToList();
            if (reversed)
            {
                pages.Reverse();
            }

            return pages;
        }

        string Render(IEnumerable<TaxonomyPage> pages)
        {
            var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
            {
                ["tech"] = new TaxonomyTerm("Tech", "tech")
            };
            foreach (var page in pages)
            {
                terms["tech"].Pages.Add(page);
            }

            var dir = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            TaxonomyFeedWriter.WriteFeeds(dir, "https://example.com", "/", "My Site", terms, "tags");
            return File.ReadAllText(Path.Combine(dir, "tags", "tech", "feed.xml"));
        }

        Assert.Equal(Render(BuildPages(reversed: false)), Render(BuildPages(reversed: true)));
    }
}
