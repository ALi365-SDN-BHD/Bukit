using Bukit.Engine.Abstractions.Content;
using System.Text.Json;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionRichTextRendererExtendedTests
{
    [Fact]
    public void Render_InlineEquation_RendersMathInline()
    {
        using var doc = JsonDocument.Parse("""
        [
          { "type": "equation", "equation": { "expression": "E=mc^2" } }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<span class=\"math-inline\">\\(E=mc^2\\)</span>", html);
    }

    [Fact]
    public void Render_InlineEquation_FallbackPlainText()
    {
        using var doc = JsonDocument.Parse("""
        [
          { "type": "equation", "plain_text": "x^2+y^2" }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<span class=\"math-inline\">\\(x^2+y^2\\)</span>", html);
    }

    [Fact]
    public void Render_TextWithHyperlink_RendersAnchorTag()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Click here",
            "href": "https://example.com"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("href=\"https://example.com\"", html);
        Assert.Contains("Click here</a>", html);
    }

    [Fact]
    public void Render_TextWithLinkProperty_RendersAnchorTag()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Visit",
            "text": { "link": { "url": "https://example.org" } }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("href=\"https://example.org\"", html);
        Assert.Contains("Visit</a>", html);
    }

    [Fact]
    public void Render_UnderlineAnnotation_WrapsWithUTag()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "underline text",
            "annotations": { "underline": true }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<u>underline text</u>", html);
    }

    [Fact]
    public void Render_StrikethroughAnnotation_WrapsWithSTag()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "deleted text",
            "annotations": { "strikethrough": true }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<s>deleted text</s>", html);
    }

    [Fact]
    public void Render_BackgroundColor_WrapsWithSpan()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "highlighted",
            "annotations": { "color": "blue_background" }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("background-color", html);
        Assert.Contains("highlighted", html);
    }

    [Fact]
    public void Render_CombinedAnnotations_BoldItalicUnderline()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "important",
            "annotations": {
              "bold": true,
              "italic": true,
              "underline": true
            }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<u><em><strong>important</strong></em></u>", html);
    }

    [Fact]
    public void Render_EmptyArray_ReturnsEmptyString()
    {
        using var doc = JsonDocument.Parse("[]");

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Render_NonArrayValue_ReturnsEmptyString()
    {
        var element = default(JsonElement);

        var html = NotionRichTextRenderer.Render(element);

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Render_EmptyPlainText_Excluded()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": ""
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Render_ForegroundColor_WrapsWithSpan()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "colored",
            "annotations": { "color": "red" }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("color:#E03E3E", html);
        Assert.Contains("colored", html);
    }

    [Fact]
    public void Render_Mention_WithHref_RendersAnchor()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "mention",
            "plain_text": "Test Page",
            "href": "https://notion.so/page-1",
            "mention": { "type": "page" }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("https://notion.so/page-1", html);
        Assert.Contains("Test Page</a>", html);
        Assert.Contains("data-mention-type=\"page\"", html);
    }

    [Fact]
    public void Render_JavascriptUrl_ExcludedFromAnchor()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Click me",
            "href": "javascript:alert(1)"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.DoesNotContain("<a", html);
        Assert.Contains("Click me", html);
    }

    [Fact]
    public void Render_DataUrl_ExcludedFromAnchor()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "See data",
            "text": { "link": { "url": "data:text/html,evil" } }
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.DoesNotContain("<a", html);
        Assert.Contains("See data", html);
    }

    [Fact]
    public void Render_ExternalLink_HasNoopenerRel()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "External",
            "href": "https://example.com"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("rel=\"noopener noreferrer\"", html);
        Assert.Contains("https://example.com", html);
    }

    [Fact]
    public void Render_InternalLink_NoRel()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Internal",
            "href": "/about"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("<a href=\"/about\">Internal</a>", html);
        Assert.DoesNotContain("rel=", html);
    }

    [Fact]
    public void Render_MailtoLink_PassesThrough()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Email us",
            "href": "mailto:user@example.com"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("mailto:user@example.com", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }

    [Fact]
    public void Render_NullHref_NoAnchor()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Just text"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.DoesNotContain("<a", html);
        Assert.Contains("Just text", html);
    }
}
