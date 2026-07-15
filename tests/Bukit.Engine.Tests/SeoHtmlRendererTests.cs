using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoHtmlRendererTests
{
    [Fact]
    public void InjectIntoHead_DoesNotAcceptAnalyticsModel()
    {
        var method = typeof(SeoHtmlRenderer).GetMethod(
            "InjectIntoHead",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            string.Equals(parameter.ParameterType.FullName, "Bukit.Rendering.AnalyticsModel", StringComparison.Ordinal));
    }

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

        var injected = SeoHtmlRenderer.InjectIntoHead(html, seo);

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

    [Fact]
    public void InjectIntoHead_ReplacesAllHeadTitlesAndPreservesBodySvgTitle()
    {
        var html = """
            <!doctype html>
            <html>
            <head>
              <title>Old title</title>
              <TITLE data-source="theme">Second title</TITLE>
            </head>
            <body><svg><title>Icon title</title></svg></body>
            </html>
            """;
        var seo = new SeoModel
        {
            Title = "Semantic title",
            DocumentTitle = "New & <Title>",
            Canonical = "https://example.com/new/"
        };

        var injected = SeoHtmlRenderer.InjectIntoHead(html, seo);
        var head = injected[..injected.IndexOf("</head>", StringComparison.OrdinalIgnoreCase)];

        Assert.Contains("<title>New &amp; &lt;Title&gt;</title>", head, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(head.ToLowerInvariant(), "<title>"));
        Assert.DoesNotContain("Old title", injected, StringComparison.Ordinal);
        Assert.DoesNotContain("Second title", injected, StringComparison.Ordinal);
        Assert.Contains("<svg><title>Icon title</title></svg>", injected, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectIntoHead_WhenTitleMissing_InsertsLegacyTitleFallback()
    {
        var html = "<!doctype html><html><head><meta charset=\"utf-8\"></head><body>ok</body></html>";
        var seo = new SeoModel
        {
            Title = "Legacy   title",
            Canonical = "https://example.com/legacy/"
        };

        var injected = SeoHtmlRenderer.InjectIntoHead(html, seo);

        Assert.Contains("<title>Legacy title</title>", injected, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectIntoHead_WhenHeadMissing_DoesNotCreateDocumentStructure()
    {
        var html = "<!doctype html><html><body>ok</body></html>";
        var seo = new SeoModel
        {
            Title = "Title",
            DocumentTitle = "Document title",
            Canonical = "https://example.com/"
        };

        var injected = SeoHtmlRenderer.InjectIntoHead(html, seo);

        Assert.Equal(html, injected);
    }

    [Fact]
    public void InjectIntoHead_PreservesUnmanagedAnalyticsScripts()
    {
        var html = """
            <html><head>
              <title>Old title</title>
              <script async src="https://www.googletagmanager.com/gtag/js?id=G-USER123"></script>
              <script>gtag('config', 'G-USER123');</script>
            </head><body></body></html>
            """;
        var seo = new SeoModel
        {
            Title = "New title",
            Canonical = "https://example.com/"
        };

        var injected = SeoHtmlRenderer.InjectIntoHead(html, seo);

        Assert.Contains("googletagmanager.com/gtag/js?id=G-USER123", injected, StringComparison.Ordinal);
        Assert.Contains("gtag('config', 'G-USER123')", injected, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectIntoHead_IgnoresHeadLikeMarkupInsideCommentsAndScripts()
    {
        var html = """
            <html><head>
              <!-- example </head><title>Comment title</title> -->
              <script>const sample = "</head><title>Script title</title>";</script>
              <title>Old title</title>
            </head><body></body></html>
            """;
        var seo = new SeoModel
        {
            Title = "Semantic",
            DocumentTitle = "New title",
            Canonical = "https://example.com/"
        };

        var injected = SeoHtmlRenderer.InjectIntoHead(html, seo);
        var inspection = HtmlDocumentTitleInspector.Inspect(injected);

        Assert.Equal("New title", Assert.Single(inspection.Titles));
        Assert.Contains("<!-- example </head><title>Comment title</title> -->", injected, StringComparison.Ordinal);
        Assert.Contains("const sample = \"</head><title>Script title</title>\"", injected, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Old title</title>", injected, StringComparison.Ordinal);
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
