using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoHtmlRendererTests
{
    [Fact]
    public void InjectIntoHead_RemovesManagedTagsWithQuotedGreaterThanCharacters()
    {
        var html = """
            <!doctype html>
            <html>
            <head>
              <link rel="canonical" href="https://old.example/?q=a>b" />
              <link rel="prev" href="https://old.example/prev/" />
              <link rel="next" href="https://old.example/next/" />
              <meta property="og:title" content="Old > Title" />
              <script type="application/ld+json">{"old":">"}</script>
            </head>
            <body>ok</body>
            </html>
            """;

        var seo = new SeoModel
        {
            Title = "New > Title",
            Description = "Desc > text",
            Canonical = "https://example.com/new/",
            Prev = "https://example.com/old/",
            Next = "https://example.com/new/page/2/",
            Og = new SeoOpenGraphModel { Title = "New > Title", Url = "https://example.com/new/" },
            Twitter = new SeoTwitterModel { Title = "New > Title" },
            JsonLd = new[] { "{\"@context\":\"https://schema.org\",\"@type\":\"WebPage\",\"name\":\"New\"}" }
        };

        var injected = SeoHtmlRenderer.InjectIntoHead(html, seo, new AnalyticsModel());

        Assert.DoesNotContain("old.example", injected, StringComparison.Ordinal);
        Assert.DoesNotContain("q=a", injected, StringComparison.Ordinal);
        Assert.DoesNotContain("b\" />", injected, StringComparison.Ordinal);
        Assert.DoesNotContain("Old &gt; Title", injected, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"old\"", injected, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.com/new/\"", injected, StringComparison.Ordinal);
        Assert.Contains("rel=\"prev\" href=\"https://example.com/old/\"", injected, StringComparison.Ordinal);
        Assert.Contains("rel=\"next\" href=\"https://example.com/new/page/2/\"", injected, StringComparison.Ordinal);
        Assert.Contains("content=\"New &gt; Title\"", injected, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(injected, "rel=\"canonical\""));
        Assert.Equal(1, CountOccurrences(injected, "rel=\"prev\""));
        Assert.Equal(1, CountOccurrences(injected, "rel=\"next\""));
        Assert.Equal(1, CountOccurrences(injected, "property=\"og:title\""));
        Assert.Contains("<body>ok</body>", injected, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
