using Xunit;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for HtmlHeadScanner HTML head detection and tag scanning logic.
/// </summary>
public sealed class HtmlHeadScannerTests
{
    // ── TryFindHead ─────────────────────────────────────────────────

    [Fact]
    public void TryFindHead_ValidHead_ReturnsRange()
    {
        var html = "<html><head><title>T</title></head><body></body></html>";
        var result = HtmlHeadScanner.TryFindHead(html, out var range);
        Assert.True(result);
        Assert.True(range.Start >= 0);
        Assert.True(range.ContentStart > range.Start);
        Assert.True(range.ContentEnd > range.ContentStart);
        Assert.True(range.End > range.ContentEnd);
    }

    [Fact]
    public void TryFindHead_EmptyHtml_ReturnsFalse()
    {
        Assert.False(HtmlHeadScanner.TryFindHead("", out _));
        Assert.False(HtmlHeadScanner.TryFindHead("   ", out _));
        Assert.False(HtmlHeadScanner.TryFindHead(null!, out _));
    }

    [Fact]
    public void TryFindHead_NoHeadTag_ReturnsFalse()
    {
        Assert.False(HtmlHeadScanner.TryFindHead("<html><body></body></html>", out _));
    }

    [Fact]
    public void TryFindHead_UnclosedHead_ReturnsFalse()
    {
        Assert.False(HtmlHeadScanner.TryFindHead("<html><head><title>T</title>", out _));
    }

    // ── FindStartTag ────────────────────────────────────────────────

    [Fact]
    public void FindStartTag_FindsHeadTag()
    {
        var html = "<html><head><title>T</title></head></html>";
        var index = HtmlHeadScanner.FindStartTag(html, "head", 0, html.Length);
        Assert.True(index >= 0);
        var end = HtmlHeadScanner.FindTagEnd(html, index);
        Assert.Equal("<head>", html[index..(end + 1)]);
    }

    [Fact]
    public void FindStartTag_NotFound_ReturnsNegative()
    {
        var html = "<html><body></body></html>";
        Assert.True(HtmlHeadScanner.FindStartTag(html, "head", 0, html.Length) < 0);
    }

    [Fact]
    public void FindStartTag_InsideScript_Ignored()
    {
        var html = "<script>var x = '<head>';</script><head></head>";
        var index = HtmlHeadScanner.FindStartTag(html, "head", 0, html.Length);
        Assert.True(index > html.IndexOf("</script>"));
    }

    // ── IsStartTag ──────────────────────────────────────────────────

    [Theory]
    [InlineData("<head>", "head", true)]
    [InlineData("<head attr=\"x\">", "head", true)]
    [InlineData("</head>", "head", false)]
    [InlineData("<body>", "head", false)]
    public void IsStartTag_VariousTags(string tag, string name, bool expected)
    {
        Assert.Equal(expected, HtmlHeadScanner.IsStartTag(tag, name));
    }

    // ── GetRawTextElementName ───────────────────────────────────────

    [Theory]
    [InlineData("<script>", "script")]
    [InlineData("<style type=\"text/css\">", "style")]
    [InlineData("<title>", "title")]
    [InlineData("<textarea>", "textarea")]
    [InlineData("<div>", null)]
    public void GetRawTextElementName_VariousTags(string tag, string? expected)
    {
        Assert.Equal(expected, HtmlHeadScanner.GetRawTextElementName(tag));
    }

    // ── IsCommentStart ──────────────────────────────────────────────

    [Fact]
    public void IsCommentStart_CommentTag_ReturnsTrue()
    {
        Assert.True(HtmlHeadScanner.IsCommentStart("<!-- comment -->", 0));
    }

    [Fact]
    public void IsCommentStart_NormalTag_ReturnsFalse()
    {
        Assert.False(HtmlHeadScanner.IsCommentStart("<head>", 0));
    }

    // ── FindCommentEnd ──────────────────────────────────────────────

    [Fact]
    public void FindCommentEnd_ClosedComment_ReturnsPositionAfterClose()
    {
        var html = "<!-- comment --><head>";
        var marker = html.IndexOf("-->", StringComparison.Ordinal);
        var end = HtmlHeadScanner.FindCommentEnd(html, 0, html.Length);
        Assert.Equal(marker + 3, end);
        Assert.Equal("<head>", html.Substring(end));
    }

    // ── FindClosingTagStart ─────────────────────────────────────────

    [Fact]
    public void FindClosingTagStart_FindsClosingTag()
    {
        var html = "<head><title>T</title></head>";
        var close = HtmlHeadScanner.FindClosingTagStart(html, "head", 1, html.Length);
        Assert.True(close > 0);
        var end = HtmlHeadScanner.FindTagEnd(html, close);
        Assert.Equal("</head>", html[close..(end + 1)]);
    }

    // ── HtmlTagAttributeReader ──────────────────────────────────────

    [Fact]
    public void GetAttribute_ValuelessAttribute_DoesNotSkipFollowingAttribute()
    {
        const string tag = "<meta itemprop charset=\"utf-8\">";

        Assert.Equal(string.Empty, HtmlTagAttributeReader.GetAttribute(tag, "itemprop"));
        Assert.Equal("utf-8", HtmlTagAttributeReader.GetAttribute(tag, "charset"));
    }

    [Fact]
    public void FindEncodingDeclarationEnd_FindsCharsetAfterValuelessAttribute()
    {
        const string html = "<html><head><meta itemprop charset=\"utf-8\"><title>x</title></head><body></body></html>";

        Assert.True(HtmlHeadScanner.TryFindHead(html, out var head));
        Assert.Equal(
            html.IndexOf("<title>", StringComparison.Ordinal),
            HtmlHeadScanner.FindEncodingDeclarationEnd(html, head));
    }

    // ── FindTagEnd ──────────────────────────────────────────────────

    [Fact]
    public void FindTagEnd_ReturnsIndexOfClosingBracket()
    {
        var html = "<head><title>T</title></head>";
        var end = HtmlHeadScanner.FindTagEnd(html, 0);
        Assert.Equal(html.IndexOf('>'), end);
    }

    // ── FindTagEnd quoting ──────────────────────────────────────────

    [Theory]
    [InlineData("<meta content=\"a > b\">", 21)]
    [InlineData("<meta content = 'x>y'>", 21)]
    [InlineData("<meta name=description content=\"He said \"hi\">", 44)]
    public void FindTagEnd_QuotedValues_SkipsAngleBracketsInsideValue(string tag, int expected)
        => Assert.Equal(expected, HtmlHeadScanner.FindTagEnd(tag, 0));

    [Fact]
    public void FindTagEnd_StrayQuoteOutsideValue_DoesNotSwallowFollowingTags()
    {
        var html = "<meta data-x\"><title>T</title>";
        Assert.Equal(13, HtmlHeadScanner.FindTagEnd(html, 0));
    }

    [Fact]
    public void TitleInspector_StrayQuoteInEarlierTag_StillFindsTitle()
    {
        var html = "<html><head><meta property=og:title content=Say\"hi><title>Real</title></head><body></body></html>";
        var inspection = HtmlDocumentTitleInspector.Inspect(html);
        Assert.Equal(1, inspection.Count);
        Assert.Equal("Real", inspection.PrimaryTitle);
    }
}
