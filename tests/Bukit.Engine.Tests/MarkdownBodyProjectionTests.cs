using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MarkdownBodyProjectionTests
{
    [Theory]
    [InlineData("https://example.org/company", "https://example.org/company")]
    [InlineData("../company?q=one&amp;lang=中文#团队", "../company?q=one&lang=中文#团队")]
    [InlineData("/目录/公司", "/目录/公司")]
    [InlineData("#团队", "#团队")]
    [InlineData("?a=1&amp;b=2", "?a=1&b=2")]
    [InlineData("https://example.org/a(b))", "https://example.org/a(b))")]
    [InlineData("/a b&lt;c&gt;\\d", "/a%20b%3Cc%3E%5Cd")]
    [InlineData("/x?value=&amp;copy;", "/x?value=&copy;")]
    [InlineData("mailto:info@example.org", "mailto:info@example.org")]
    [InlineData("tel:+60123456789", "tel:+60123456789")]
    [InlineData("//example.org/a", "//example.org/a")]
    public void Links_RoundTripTargetsAndReadableLabels(string href, string expected)
    {
        var html = $"Before <a href=\"{href}\"><strong>官网</strong> [部门] * ` \\ &amp; more</a> after";
        var markdown = MarkdownBodyProjection.FromHtml(html);
        var parsed = Markdown.Parse(markdown);
        var link = Assert.Single(parsed.Descendants<LinkInline>());
        Assert.Equal(expected, link.Url);
        Assert.False(link.IsImage);
        Assert.Empty(parsed.Descendants<HtmlInline>());
        var rendered = Markdown.ToHtml(markdown);
        Assert.Contains("官网 [部门] * ` \\ &amp; more", rendered);
        Assert.Contains("Before", rendered);
        Assert.Contains("after", rendered);
        Assert.DoesNotContain(href, SearchIndexBuilder.StripHtmlToText(html));
    }

    [Theory]
    [InlineData("<A HREF='/ok'>label</A>")]
    [InlineData("<a href=/ok>label</a>")]
    [InlineData("<a title='a > b' data-href='/wrong' href='/ok'>label</a>")]
    [InlineData("<a title=\"href='/wrong'\" href='/ok'>label</a>")]
    [InlineData("<a href='/ok' href='javascript:bad'>label</a>")]
    public void Attributes_UseOnlyTheActualFirstHref(string html)
        => Assert.Equal("/ok", Assert.Single(Markdown.Parse(MarkdownBodyProjection.FromHtml(html)).Descendants<LinkInline>()).Url);

    [Theory]
    [InlineData("../other?q=1&amp;b=2", "/site/blog/other?q=1&b=2")]
    [InlineData("#details", "/site/blog/article/#details")]
    [InlineData("?lang=zh", "/site/blog/article/?lang=zh")]
    public void RelativeLinks_ResolveAgainstTheSourcePage(string href, string expected)
    {
        var markdown = MarkdownBodyProjection.FromHtml($"<a href='{href}'>Off<strong>icial</strong> <span class='name'>site</span></a>", "/site/blog/article/");
        Assert.Equal(expected, Assert.Single(Markdown.Parse(markdown).Descendants<LinkInline>()).Url);
        Assert.Contains(">Official site</a>", Markdown.ToHtml(markdown));
    }

    [Theory]
    [InlineData("<a>label</a>")]
    [InlineData("<a href=''>label</a>")]
    [InlineData("<a href>label</a>")]
    [InlineData("<a data-href='/hidden'>label</a>")]
    [InlineData("<a title=\"href='/hidden'\">label</a>")]
    [InlineData("<a href='javascript:alert(1)'>label</a>")]
    [InlineData("<a href='JaVaScRiPt&#58;alert(1)'>label</a>")]
    [InlineData("<a href='java&#x09;script:alert(1)'>label</a>")]
    [InlineData("<a href='vbscript:bad'>label</a>")]
    [InlineData("<a href='data:text/html,bad'>label</a>")]
    [InlineData("<a href='file:///etc/passwd'>label</a>")]
    [InlineData("<a href='custom:bad'>label</a>")]
    [InlineData("<a href='javascript:bad' href='/safe'>label</a>")]
    [InlineData("<a href='/unclosed'>label")]
    [InlineData("<a href='/empty'><img src='/image.png'></a>label")]
    public void UnsafeMissingAndMalformedTargets_RemainText(string html)
    {
        var markdown = MarkdownBodyProjection.FromHtml(html);
        Assert.Contains("label", markdown);
        Assert.Empty(Markdown.Parse(markdown).Descendants<LinkInline>());
    }

    [Fact]
    public void HiddenAndEncodedMarkup_CannotProduceExecutableLinksOrHtml()
    {
        const string html = "<script><a href='/script'>hidden</a></script><!-- <a href='/comment'>hidden</a> -->" +
            "<style><a href='/style'>hidden</a></style><textarea><a href='/raw'>text</a></textarea>" +
            "&lt;script&gt;alert(1)&lt;/script&gt; [bad](javascript:alert(1)) " +
            "<a href='javascript:bad'>[evil](https://evil.example)</a> <a href='/ok'>visible</a>";
        var markdown = MarkdownBodyProjection.FromHtml(html);
        Assert.Equal("/ok", Assert.Single(Markdown.Parse(markdown).Descendants<LinkInline>()).Url);
        Assert.DoesNotContain("<script", Markdown.ToHtml(markdown));
        Assert.DoesNotContain("href=\"javascript:", Markdown.ToHtml(markdown));
    }

    [Fact]
    public void NestedMalformedAnchors_PreserveTextAndOnlyTheInnermostTarget()
    {
        var html = string.Concat(Enumerable.Repeat("<a href='/outer'>outer", 1000)) + "<a href='/inner'>inner</a> tail";
        var markdown = MarkdownBodyProjection.FromHtml(html);
        Assert.Equal("/inner", Assert.Single(Markdown.Parse(markdown).Descendants<LinkInline>()).Url);
        Assert.Contains("outer", markdown);
        Assert.EndsWith("tail", markdown);
    }

    [Fact]
    public void ImagesAndAdjacentLinks_PreserveBodyOrderWithoutCreatingImages()
    {
        const string html = "first<img src='/one.png'>second!<a href='/one'>one</a><img src='/two.png'>third<a href='/two'>two</a>last";
        var markdown = MarkdownBodyProjection.FromHtml(html);
        var links = Markdown.Parse(markdown).Descendants<LinkInline>().ToArray();
        Assert.Equal(new[] { "/one", "/two" }, links.Select(x => x.Url));
        Assert.All(links, link => Assert.False(link.IsImage));
        var last = -1;
        foreach (var word in new[] { "first", "second", "one", "third", "two", "last" })
        {
            var index = markdown.IndexOf(word, last + 1, StringComparison.Ordinal);
            Assert.True(index > last);
            last = index;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("<p>Hello</p><div>World</div>")]
    [InlineData("<h3>中文</h3><p>普通正文。</p><img src='/image.png'>")]
    public void PlainBody_PreservesExistingText(string html)
        => Assert.Equal(SearchIndexBuilder.StripHtmlToText(html), MarkdownBodyProjection.FromHtml(html));
}
