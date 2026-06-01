using System.Text;
using System.Text.Json;

namespace Bukit.Shared.Notion;
public static class HtmlToNotionBlockConverter
{
    public static string ToBlocksJson(string html)
    {
        var blocks = Convert(html);
        return SerializeBlocks(blocks);
    }

    public static List<NotionBlock> Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var tokens = HtmlTokenizer.Tokenize(html);
        var (blocks, _) = ParseBlocks(tokens, 0);
        return blocks;
    }

    private static string SerializeBlocks(List<NotionBlock> blocks)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartArray();
        foreach (var block in blocks)
            WriteBlock(writer, block);
        writer.WriteEndArray();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteBlock(Utf8JsonWriter writer, NotionBlock block)
    {
        switch (block)
        {
            case Heading1Block h1:
                WriteHeadingBlock(writer, "heading_1", h1.Text);
                break;
            case Heading2Block h2:
                WriteHeadingBlock(writer, "heading_2", h2.Text);
                break;
            case Heading3Block h3:
                WriteHeadingBlock(writer, "heading_3", h3.Text);
                break;
            case ParagraphBlock p:
                WriteRichTextBlock(writer, "paragraph", p.Segments);
                break;
            case BulletedListItemBlock li:
                WriteRichTextBlock(writer, "bulleted_list_item", li.Segments);
                break;
            case NumberedListItemBlock li:
                WriteRichTextBlock(writer, "numbered_list_item", li.Segments);
                break;
            case QuoteBlock q:
                WriteRichTextBlock(writer, "quote", q.Segments);
                break;
            case ImageBlock img:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "image");
                writer.WriteStartObject("image");
                writer.WriteString("type", "external");
                writer.WriteStartObject("external");
                writer.WriteString("url", img.Url);
                writer.WriteEndObject();
                if (!string.IsNullOrWhiteSpace(img.Caption))
                {
                    writer.WriteStartArray("caption");
                    WriteTextObject(writer, img.Caption);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case ToggleBlock toggle:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "toggle");
                writer.WriteStartObject("toggle");
                writer.WriteStartArray("rich_text");
                WriteTextObject(writer, toggle.Heading);
                writer.WriteEndArray();
                if (toggle.Children.Count > 0)
                {
                    writer.WriteStartArray("children");
                    foreach (var child in toggle.Children)
                        WriteBlock(writer, child);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case CodeBlock code:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "code");
                writer.WriteStartObject("code");
                writer.WriteStartArray("rich_text");
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteStartObject("text");
                writer.WriteString("content", TruncateBlockText(code.Code));
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteStartObject("language");
                writer.WriteString("name", code.Language);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case CalloutBlock callout:
                writer.WriteStartObject();
                writer.WriteString("object", "block");
                writer.WriteString("type", "callout");
                writer.WriteStartObject("callout");
                writer.WriteStartArray("rich_text");
                WriteTextObject(writer, callout.Text);
                writer.WriteEndArray();
                writer.WriteStartObject("icon");
                writer.WriteString("type", "emoji");
                writer.WriteString("emoji", callout.Icon);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
        }
    }

    private static void WriteHeadingBlock(Utf8JsonWriter writer, string type, string text)
    {
        writer.WriteStartObject();
        writer.WriteString("object", "block");
        writer.WriteString("type", type);
        writer.WriteStartObject(type);
        writer.WriteStartArray("rich_text");
        WriteTextObject(writer, TruncateBlockText(text));
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteRichTextBlock(Utf8JsonWriter writer, string type, List<RichTextSegment> segments)
    {
        writer.WriteStartObject();
        writer.WriteString("object", "block");
        writer.WriteString("type", type);
        writer.WriteStartObject(type);
        writer.WriteStartArray("rich_text");
        foreach (var seg in segments)
            WriteRichTextSegment(writer, seg);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteRichTextSegment(Utf8JsonWriter writer, RichTextSegment seg)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteStartObject("text");
        writer.WriteString("content", TruncateBlockText(seg.Text));
        if (seg.LinkUrl != null)
        {
            writer.WriteStartObject("link");
            writer.WriteString("url", seg.LinkUrl);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        if (seg.Bold)
            writer.WriteStartObject("annotations");
        if (seg.Bold || seg.Italic)
        {
            writer.WriteStartObject("annotations");
            writer.WriteBoolean("bold", seg.Bold);
            writer.WriteBoolean("italic", seg.Italic);
            writer.WriteBoolean("strikethrough", false);
            writer.WriteBoolean("underline", false);
            writer.WriteBoolean("code", false);
            writer.WriteString("color", "default");
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static string TruncateBlockText(string text)
        => text.Length <= 2000 ? text : text[..1997] + "...";

    private static void WriteTextObject(Utf8JsonWriter writer, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteStartObject("text");
        writer.WriteString("content", TruncateBlockText(value));
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string? GetAttribute(string attrs, string attrName)
    {
        var lower = attrs.ToLowerInvariant();
        var search = attrName.ToLowerInvariant() + "=";
        var startIdx = lower.IndexOf(search, StringComparison.Ordinal);
        if (startIdx < 0) return null;

        startIdx += search.Length;
        var quote = attrs[startIdx];
        if (quote is '"' or '\'')
        {
            startIdx++;
            var endIdx = attrs.IndexOf(quote, startIdx);
            return endIdx >= 0 ? attrs[startIdx..endIdx] : null;
        }

        var spaceIdx = attrs.IndexOf(' ', startIdx);
        return spaceIdx >= 0 ? attrs[startIdx..spaceIdx] : attrs[startIdx..];
    }

    private static bool HasClass(string attrs, string className)
    {
        var classVal = GetAttribute(attrs, "class");
        if (classVal == null) return false;
        var classes = classVal.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return classes.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractText(string html)
    {
        var result = new StringBuilder();
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (!inTag) result.Append(ch);
        }
        return HtmlTokenizer.DecodeHtmlEntities(result.ToString().Trim());
    }

    private static (List<NotionBlock> Blocks, int NextIndex) ParseBlocks(
        List<HtmlTokenizer.HtmlToken> tokens, int startIndex)
    {
        var blocks = new List<NotionBlock>();
        var i = startIndex;

        while (i < tokens.Count)
        {
            var token = tokens[i];

            if (token.Type == HtmlTokenizer.HtmlTokenType.CloseTag)
            {
                return (blocks, i + 1);
            }

            if (token.Type == HtmlTokenizer.HtmlTokenType.Text)
            {
                blocks.Add(new ParagraphBlock(token.TextContent));
                i++;
                continue;
            }

            if (token.Type == HtmlTokenizer.HtmlTokenType.SelfClosingTag)
            {
                if (token.TagName == "img")
                {
                    var src = GetAttribute(token.Attributes, "src");
                    var alt = GetAttribute(token.Attributes, "alt");
                    if (!string.IsNullOrWhiteSpace(src))
                        blocks.Add(new ImageBlock(src, alt));
                }
                i++;
                continue;
            }

            if (token.Type == HtmlTokenizer.HtmlTokenType.OpenTag)
            {
                var tagName = token.TagName;
                var attrs = token.Attributes;

                if (tagName == "br" || tagName == "hr")
                {
                    i++;
                    continue;
                }

                if (IsHeadingTag(tagName))
                {
                    i++;
                    var textContent = CollectTextUntilClose(tokens, ref i, tagName);
                    if (!string.IsNullOrWhiteSpace(textContent))
                    {
                        blocks.Add(CreateHeadingBlock(tagName, textContent));
                    }
                    continue;
                }

                if (tagName == "p" || tagName == "div" || tagName == "span")
                {
                    var hasFaqClass = HasClass(attrs, "faq-item");
                    if (hasFaqClass)
                    {
                        i++;
                        var faqBlocks = CollectFaqBlocks(tokens, ref i);
                        if (faqBlocks.Question != null && faqBlocks.ToggleBlock != null)
                        {
                            blocks.Add(faqBlocks.ToggleBlock);
                        }
                        continue;
                    }

                    i++;
                    var segments = CollectRichText(tokens, ref i, tagName);
                    if (segments.Count > 0)
                        blocks.Add(new ParagraphBlock(segments));
                    else
                        i++;
                    continue;
                }

                if (tagName == "ul" || tagName == "ol")
                {
                    var isOrdered = tagName == "ol";
                    i++;
                    while (i < tokens.Count)
                    {
                        if (tokens[i].Type == HtmlTokenizer.HtmlTokenType.CloseTag &&
                            tokens[i].TagName == tagName)
                        {
                            i++;
                            break;
                        }
                        if (tokens[i].Type == HtmlTokenizer.HtmlTokenType.OpenTag &&
                            tokens[i].TagName == "li")
                        {
                            i++;
                            var liSegments = CollectRichText(tokens, ref i, "li");
                            if (liSegments.Count > 0)
                            {
                                blocks.Add(isOrdered
                                    ? new NumberedListItemBlock(liSegments)
                                    : new BulletedListItemBlock(liSegments));
                            }
                        }
                        else
                        {
                            i++;
                        }
                    }
                    continue;
                }

                if (tagName == "blockquote")
                {
                    i++;
                    var textContent = CollectTextUntilClose(tokens, ref i, "blockquote");
                    if (!string.IsNullOrWhiteSpace(textContent))
                        blocks.Add(new QuoteBlock(textContent));
                    continue;
                }

                if (tagName == "pre")
                {
                    i++;
                    var codeText = CollectRawTextUntilClose(tokens, ref i, "pre");
                    string? lang = null;
                    if (!string.IsNullOrWhiteSpace(codeText) && codeText.Length > 0)
                    {
                        var langMatch = System.Text.RegularExpressions.Regex.Match(codeText, @"^class\s*=\s*[""']([^""']*)[""']");
                        if (langMatch.Success)
                        {
                            lang = langMatch.Groups[1].Value;
                            codeText = codeText[langMatch.Length..].TrimStart();
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(codeText))
                        blocks.Add(new CodeBlock(codeText, lang ?? "plain text"));
                    continue;
                }

                if (tagName == "div" && HasClass(attrs, "callout"))
                {
                    i++;
                    var textContent = CollectTextUntilClose(tokens, ref i, "div");
                    if (!string.IsNullOrWhiteSpace(textContent))
                        blocks.Add(new CalloutBlock(textContent));
                    continue;
                }

                if (tagName == "img")
                {
                    var src = GetAttribute(attrs, "src");
                    var alt = GetAttribute(attrs, "alt");
                    if (!string.IsNullOrWhiteSpace(src))
                        blocks.Add(new ImageBlock(src, alt));
                    i++;
                    continue;
                }

                if (tagName is "main" or "article" or "section" or "header" or
                    "footer" or "nav" or "aside" or "figure" or "figcaption")
                {
                    i++;
                    var (children, nextIdx) = ParseBlocks(tokens, i);
                    blocks.AddRange(children);
                    i = nextIdx;
                    continue;
                }

                if (tagName == "a")
                {
                    var href = GetAttribute(attrs, "href");
                    var linkText = CollectTextUntilClose(tokens, ref i, "a");
                    if (!string.IsNullOrWhiteSpace(linkText))
                    {
                        var segments = new List<RichTextSegment>
                        {
                            new RichTextSegment(linkText, LinkUrl: href)
                        };
                        blocks.Add(new ParagraphBlock(segments));
                    }
                    else
                    {
                        i++;
                    }
                    continue;
                }

                i++;
            }
        }

        return (blocks, i);
    }

    private static bool IsHeadingTag(string tagName)
        => tagName.Length == 2 && tagName[0] == 'h' && tagName[1] >= '1' && tagName[1] <= '6';

    private static NotionBlock CreateHeadingBlock(string tagName, string text)
    {
        return tagName switch
        {
            "h1" => new Heading1Block(text),
            "h2" => new Heading2Block(text),
            _ => new Heading3Block(text)
        };
    }

    private static string CollectTextUntilClose(
        List<HtmlTokenizer.HtmlToken> tokens, ref int i, string closeTag)
    {
        var sb = new StringBuilder();
        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Type == HtmlTokenizer.HtmlTokenType.CloseTag && t.TagName == closeTag)
            {
                i++;
                break;
            }
            if (t.Type == HtmlTokenizer.HtmlTokenType.Text)
            {
                sb.Append(t.TextContent);
                sb.Append(' ');
            }
            if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag && t.TagName == "img")
            {
                var alt = GetAttribute(t.Attributes, "alt");
                if (!string.IsNullOrWhiteSpace(alt))
                    sb.Append($"[Image: {alt}] ");
            }
            if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag && t.TagName == "br")
            {
                sb.AppendLine();
            }
            i++;
        }
        return sb.ToString().Trim();
    }

    private static string CollectRawTextUntilClose(
        List<HtmlTokenizer.HtmlToken> tokens, ref int i, string closeTag)
    {
        var sb = new StringBuilder();
        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Type == HtmlTokenizer.HtmlTokenType.CloseTag && t.TagName == closeTag)
            {
                i++;
                break;
            }
            if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag &&
                (t.TagName == "code" || t.TagName == "span"))
            {
                var attrs = t.Attributes;
                if (!string.IsNullOrWhiteSpace(t.Attributes) && sb.Length == 0)
                    sb.Append(attrs);
                continue;
            }
            if (t.Type == HtmlTokenizer.HtmlTokenType.CloseTag &&
                (t.TagName == "code" || t.TagName == "span"))
            {
                continue;
            }
            if (t.Type == HtmlTokenizer.HtmlTokenType.Text)
            {
                sb.Append(t.TextContent);
                sb.AppendLine();
            }
            i++;
        }
        return sb.ToString().Trim();
    }

    private static List<RichTextSegment> CollectRichText(
        List<HtmlTokenizer.HtmlToken> tokens, ref int i, string closeTag)
    {
        var segments = new List<RichTextSegment>();

        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Type == HtmlTokenizer.HtmlTokenType.CloseTag && t.TagName == closeTag)
            {
                i++;
                break;
            }

            if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag && t.TagName == "a")
            {
                var href = GetAttribute(t.Attributes, "href");
                i++;
                var linkText = CollectTextUntilClose(tokens, ref i, "a");
                if (!string.IsNullOrWhiteSpace(linkText))
                    segments.Add(new RichTextSegment(linkText, LinkUrl: href));
            }
            else if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag &&
                     (t.TagName == "strong" || t.TagName == "b"))
            {
                i++;
                var text = CollectTextUntilClose(tokens, ref i, t.TagName);
                if (!string.IsNullOrWhiteSpace(text))
                    segments.Add(new RichTextSegment(text, Bold: true));
            }
            else if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag &&
                     (t.TagName == "em" || t.TagName == "i"))
            {
                i++;
                var text = CollectTextUntilClose(tokens, ref i, t.TagName);
                if (!string.IsNullOrWhiteSpace(text))
                    segments.Add(new RichTextSegment(text, Italic: true));
            }
            else if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag &&
                     IsHeadingTag(t.TagName))
            {
                i++;
                var text = CollectTextUntilClose(tokens, ref i, t.TagName);
                if (!string.IsNullOrWhiteSpace(text))
                    segments.Add(new RichTextSegment(text, Bold: true));
            }
            else if (t.Type == HtmlTokenizer.HtmlTokenType.Text)
            {
                segments.Add(new RichTextSegment(t.TextContent));
                i++;
            }
            else
            {
                i++;
            }
        }

        return segments;
    }

    private sealed record FaqParseResult(
        ToggleBlock? ToggleBlock,
        string? Question);

    private static FaqParseResult CollectFaqBlocks(
        List<HtmlTokenizer.HtmlToken> tokens, ref int i)
    {
        string? question = null;
        string? answer = null;

        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Type == HtmlTokenizer.HtmlTokenType.CloseTag &&
                (t.TagName == "div" || t.TagName == "p" || t.TagName == "section"))
            {
                break;
            }

            if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag &&
                (t.TagName == "h3" || t.TagName == "h4"))
            {
                i++;
                question = CollectTextUntilClose(tokens, ref i, t.TagName);
            }
            else if (t.Type == HtmlTokenizer.HtmlTokenType.OpenTag && t.TagName == "p")
            {
                i++;
                var text = CollectTextUntilClose(tokens, ref i, "p");
                if (!string.IsNullOrWhiteSpace(text) && question != null)
                    answer = text;
            }
            else
            {
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(question))
            return new FaqParseResult(null, null);

        var children = new List<NotionBlock>();
        if (!string.IsNullOrWhiteSpace(answer))
            children.Add(new ParagraphBlock(answer));

        return new FaqParseResult(
            new ToggleBlock(question, children),
            question);
    }
}
