using System.Text;

namespace Bukit.Shared.Notion;

public static class HtmlToNotionBlockConverter
{
    public static string ToBlocksJson(string html)
    {
        var blocks = Convert(html);
        return NotionBlockJsonWriter.SerializeBlocks(blocks);
    }

    public static List<NotionBlock> Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var tokens = HtmlTokenizer.Tokenize(html);
        var (blocks, _) = ParseBlocks(tokens, 0);
        return blocks;
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
                    var notionUrl = ToNotionExternalUrl(src);
                    if (!string.IsNullOrWhiteSpace(notionUrl))
                        blocks.Add(new ImageBlock(notionUrl, alt));
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

                if (tagName == "div" && HasClass(attrs, "callout"))
                {
                    i++;
                    var textContent = CollectTextUntilClose(tokens, ref i, "div");
                    if (!string.IsNullOrWhiteSpace(textContent))
                        blocks.Add(new CalloutBlock(textContent));
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
                    codeText = NormalizeLineEndings(codeText);
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

                if (tagName == "img")
                {
                    var src = GetAttribute(attrs, "src");
                    var alt = GetAttribute(attrs, "alt");
                    var notionUrl = ToNotionExternalUrl(src);
                    if (!string.IsNullOrWhiteSpace(notionUrl))
                        blocks.Add(new ImageBlock(notionUrl, alt));
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
                            new RichTextSegment(linkText, LinkUrl: ToNotionLinkUrl(href))
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

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n").Replace('\r', '\n');

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
                    segments.Add(new RichTextSegment(linkText, LinkUrl: ToNotionLinkUrl(href)));
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

    private static string? ToNotionLinkUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return uri.Scheme is "http" or "https" or "mailto" or "tel" ? trimmed : null;
    }

    private static string? ToNotionExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return uri.Scheme is "http" or "https" ? trimmed : null;
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

        if (i < tokens.Count &&
            tokens[i].Type == HtmlTokenizer.HtmlTokenType.CloseTag &&
            (tokens[i].TagName == "div" || tokens[i].TagName == "p" || tokens[i].TagName == "section"))
        {
            i++;
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
