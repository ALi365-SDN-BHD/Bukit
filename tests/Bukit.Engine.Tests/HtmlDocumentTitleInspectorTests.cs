using Xunit;

namespace Bukit.Engine.Tests;

public sealed class HtmlDocumentTitleInspectorTests
{
    [Fact]
    public void Inspect_ReadsOnlyHeadTitlesAndNormalizesEntitiesAndWhitespace()
    {
        var html = """
            <!doctype html>
            <html>
            <head data-note="a>b">
              <TITLE data-source="theme">  Page &amp;
                 Site  </TITLE>
              <title> </title>
            </head>
            <body><svg><title>Icon title</title></svg></body>
            </html>
            """;

        var inspection = HtmlDocumentTitleInspector.Inspect(html);

        Assert.True(inspection.HasHead);
        Assert.Equal(2, inspection.Count);
        Assert.Equal("Page & Site", inspection.Titles[0]);
        Assert.Equal(string.Empty, inspection.Titles[1]);
        Assert.Contains("<head data-note=\"a>b\">", inspection.HeadHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspect_WhenHeadMissing_ReturnsNoTitles()
    {
        var inspection = HtmlDocumentTitleInspector.Inspect("<html><body><title>Body title</title></body></html>");

        Assert.False(inspection.HasHead);
        Assert.Empty(inspection.Titles);
        Assert.Null(inspection.HeadHtml);
    }

    [Fact]
    public void Inspect_IgnoresHeadLikeMarkupInsideCommentsAndRawText()
    {
        var html = """
            <html><head>
              <!-- example </head><title>Comment title</title> -->
              <script>const sample = "</head><title>Script title</title>";</script>
              <title>Actual title</title>
            </head><body></body></html>
            """;

        var inspection = HtmlDocumentTitleInspector.Inspect(html);

        Assert.True(inspection.HasHead);
        Assert.Equal("Actual title", Assert.Single(inspection.Titles));
        Assert.Contains("<title>Actual title</title>", inspection.HeadHtml, StringComparison.Ordinal);
    }
}
