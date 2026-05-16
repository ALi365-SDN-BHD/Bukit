using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionAutoSummaryTests
{
    [Fact]
    public void ExtractFromHtml_StripsTagsDecodesEntitiesAndCollapsesWhitespace()
    {
        var summary = NotionAutoSummary.ExtractFromHtml(
            "<p>Hello&nbsp;<strong>Notion</strong></p>\n<p>Second &amp; line</p>",
            maxLength: 100);

        Assert.Equal("Hello Notion Second & line", summary);
    }

    [Fact]
    public void ExtractFromHtml_TruncatesAtWordBoundary()
    {
        var summary = NotionAutoSummary.ExtractFromHtml(
            "<p>Alpha beta gamma delta epsilon</p>",
            maxLength: 18);

        Assert.Equal("Alpha beta gamma…", summary);
    }

    [Fact]
    public void ExtractFromHtml_WithSmallBoundary_UsesHardCut()
    {
        var summary = NotionAutoSummary.ExtractFromHtml(
            "<p>Supercalifragilistic</p>",
            maxLength: 8);

        Assert.Equal("Supercal…", summary);
    }

    [Fact]
    public void ExtractFromHtml_WithEmptyInputOrNonPositiveLength_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NotionAutoSummary.ExtractFromHtml("", 100));
        Assert.Equal(string.Empty, NotionAutoSummary.ExtractFromHtml("<p>Body</p>", 0));
    }
}
