using Bukit.Shared.Notion;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class HtmlToNotionBlockConverterTests
{
    [Fact]
    public void Convert_TransformsCommonHtmlStructuresIntoBlocks()
    {
        const string html = """
            <section>
              <h2>Intro</h2>
              <p>Hello <strong>bold</strong><em>italic</em><a href="https://example.com">site</a></p>
              <ul><li>First</li><li>Second</li></ul>
              <blockquote>Quoted</blockquote>
              <pre>line1
            line2</pre>
            </section>
            """;

        var blocks = HtmlToNotionBlockConverter.Convert(html);

        Assert.Collection(blocks,
            block =>
            {
                var heading = Assert.IsType<Heading2Block>(block);
                Assert.Equal("Intro", heading.Text);
            },
            block =>
            {
                var paragraph = Assert.IsType<ParagraphBlock>(block);
                Assert.Collection(paragraph.Segments,
                    segment =>
                    {
                        Assert.Equal("Hello", segment.Text);
                        Assert.False(segment.Bold);
                        Assert.False(segment.Italic);
                        Assert.Null(segment.LinkUrl);
                    },
                    segment =>
                    {
                        Assert.Equal("bold", segment.Text);
                        Assert.True(segment.Bold);
                    },
                    segment =>
                    {
                        Assert.Equal("italic", segment.Text);
                        Assert.True(segment.Italic);
                    },
                    segment =>
                    {
                        Assert.Equal("site", segment.Text);
                        Assert.Equal("https://example.com", segment.LinkUrl);
                    });
            },
            block =>
            {
                var item = Assert.IsType<BulletedListItemBlock>(block);
                Assert.Equal("First", Assert.Single(item.Segments).Text);
            },
            block =>
            {
                var item = Assert.IsType<BulletedListItemBlock>(block);
                Assert.Equal("Second", Assert.Single(item.Segments).Text);
            },
            block =>
            {
                var quote = Assert.IsType<QuoteBlock>(block);
                Assert.Equal("Quoted", Assert.Single(quote.Segments).Text);
            },
            block =>
            {
                var code = Assert.IsType<CodeBlock>(block);
                Assert.Equal("line1\nline2", code.Code);
                Assert.Equal("plain text", code.Language);
            });
    }

    [Fact]
    public void Convert_ConvertsFaqItemAndContinuesWithFollowingBlocks()
    {
        const string html = """
            <div class="faq-item">
              <h3>What is Bukit?</h3>
              <p>A static site builder.</p>
            </div>
            <p>After FAQ</p>
            """;

        var blocks = HtmlToNotionBlockConverter.Convert(html);

        Assert.Collection(blocks,
            block =>
            {
                var toggle = Assert.IsType<ToggleBlock>(block);
                Assert.Equal("What is Bukit?", toggle.Heading);
                var answer = Assert.Single(toggle.Children);
                var paragraph = Assert.IsType<ParagraphBlock>(answer);
                Assert.Equal("A static site builder.", Assert.Single(paragraph.Segments).Text);
            },
            block =>
            {
                var paragraph = Assert.IsType<ParagraphBlock>(block);
                Assert.Equal("After FAQ", Assert.Single(paragraph.Segments).Text);
            });
    }

    [Fact]
    public void Convert_ConvertsCalloutDivIntoCalloutBlock()
    {
        var blocks = HtmlToNotionBlockConverter.Convert("<div class=\"callout\">Pay attention</div>");

        var callout = Assert.Single(blocks);
        var block = Assert.IsType<CalloutBlock>(callout);
        Assert.Equal("Pay attention", block.Text);
        Assert.Equal("📝", block.Icon);
    }

    [Fact]
    public void Convert_ConvertsSelfClosingImageIntoImageBlock()
    {
        var blocks = HtmlToNotionBlockConverter.Convert(
            "<img src=\"https://example.com/hero.png\" alt=\"Hero image\" />");

        var image = Assert.Single(blocks);
        var block = Assert.IsType<ImageBlock>(image);
        Assert.Equal("https://example.com/hero.png", block.Url);
        Assert.Equal("Hero image", block.Caption);
    }

    [Fact]
    public void Convert_ConvertsOrderedListAndStandaloneAnchor()
    {
        const string html = """
            <ol><li>First</li><li>Second</li></ol>
            <a href="https://example.com/docs">Docs</a>
            """;

        var blocks = HtmlToNotionBlockConverter.Convert(html);

        Assert.Collection(blocks,
            block =>
            {
                var item = Assert.IsType<NumberedListItemBlock>(block);
                Assert.Equal("First", Assert.Single(item.Segments).Text);
            },
            block =>
            {
                var item = Assert.IsType<NumberedListItemBlock>(block);
                Assert.Equal("Second", Assert.Single(item.Segments).Text);
            },
            block =>
            {
                var paragraph = Assert.IsType<ParagraphBlock>(block);
                var segment = Assert.Single(paragraph.Segments);
                Assert.Equal("Docs", segment.Text);
                Assert.Equal("https://example.com/docs", segment.LinkUrl);
            });
    }

    [Theory]
    [InlineData("contact.html")]
    [InlineData("/contact/")]
    [InlineData("//example.com/contact")]
    public void Convert_RendersUnsupportedNotionLinksAsPlainText(string href)
    {
        var blocks = HtmlToNotionBlockConverter.Convert(
            $"""<p>Go to <a href="{href}">Contact</a></p>""");

        var paragraph = Assert.Single(blocks);
        var block = Assert.IsType<ParagraphBlock>(paragraph);
        Assert.Collection(block.Segments,
            segment => Assert.Equal("Go to", segment.Text),
            segment =>
            {
                Assert.Equal("Contact", segment.Text);
                Assert.Null(segment.LinkUrl);
            });
    }

    [Theory]
    [InlineData("assets/images/hero.png")]
    [InlineData("/assets/images/hero.png")]
    [InlineData("//cdn.example.com/hero.png")]
    public void Convert_SkipsUnsupportedNotionImageUrls(string src)
    {
        var blocks = HtmlToNotionBlockConverter.Convert(
            $"""<img src="{src}" alt="Hero" />""");

        Assert.Empty(blocks);
    }
}
