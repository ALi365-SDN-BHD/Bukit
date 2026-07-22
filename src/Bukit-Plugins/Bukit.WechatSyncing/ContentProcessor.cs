using System.Text;
using System.Text.RegularExpressions;
using NotionColorPalette = Bukit.Notion.Rendering.NotionColorPalette;

namespace Bukit.WechatSyncing;

/// <summary>
/// Processes HTML content for WeChat compatibility:
/// - Converts &lt;figure&gt; tags to &lt;p&gt; tags
/// - Converts &lt;div class="callout"&gt; to &lt;section&gt; with inline styles
/// - Expands &lt;details&gt;/&lt;summary&gt; (toggle) to visible content, preserving heading tags and block colors
/// - Converts &lt;div class="to-do"&gt; to text with checkbox markers (checked items get strikethrough)
/// - Converts &lt;div class="video-embed"&gt; iframes to links
/// - Adds inline styles to &lt;table&gt; for WeChat rendering
/// - Converts &lt;div class="math-block"&gt; to plain text fallback
/// - Converts or removes &lt;nav class="notion-toc"&gt; elements
/// - Converts internal links (link-to-page, child-page, child-database) to plain text
/// - Converts callout-children and to-do-children container divs to inline styles
/// - Strips notion-synced-block wrapper class
/// - Converts Notion color classes (notion-xxx) to inline styles
/// - Adds inline styles to &lt;pre&gt;/&lt;code&gt; for code blocks and standalone inline &lt;code&gt;
/// - Adds inline styles to &lt;blockquote&gt; elements
/// - Adds inline styles to &lt;hr&gt; dividers
/// - Adds inline styles to &lt;a&gt; links (WeChat standard color)
/// - Decodes HTML named entities (smart quotes, etc.)
/// - Cleans up lazy-load attributes on images
/// </summary>
internal static class ContentProcessor
{
    // ── Figure → P conversion ───────────────────────────────────────────

    /// <summary>
    /// Converts <c>&lt;figure&gt;</c> elements to <c>&lt;p&gt;</c> elements for WeChat compatibility.
    /// Extracts the image and optional caption, wraps them in separate <c>&lt;p&gt;</c> tags.
    /// If a <c>&lt;figcaption&gt;</c> exists, its text is also copied to the image <c>alt</c> attribute
    /// when alt is missing.
    /// </summary>
    internal static string ConvertFiguresToParagraphs(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Match <figure ...> ... </figure> blocks (non-greedy)
        return Regex.Replace(html, @"<figure\b[^>]*>([\s\S]*?)</figure>", match =>
        {
            var inner = match.Groups[1].Value;

            // Extract the <img ...> tag (first occurrence)
            var imgMatch = Regex.Match(inner, @"<img\b[^>]*/?>", RegexOptions.IgnoreCase);
            if (!imgMatch.Success)
            {
                // No image found, just wrap content in <p>
                return $"<p>{inner.Trim()}</p>";
            }

            var imgTag = imgMatch.Value;

            // Extract <figcaption> text
            var captionMatch = Regex.Match(inner, @"<figcaption\b[^>]*>([\s\S]*?)</figcaption>", RegexOptions.IgnoreCase);
            var captionText = captionMatch.Success ? WechatSyncHelpers.StripHtml(captionMatch.Groups[1].Value, 500).Trim() : string.Empty;

            // If img has no alt and we have a caption, add it
            if (!string.IsNullOrWhiteSpace(captionText) &&
                !Regex.IsMatch(imgTag, @"\balt\s*=", RegexOptions.IgnoreCase))
            {
                // Insert alt before the closing > or />
                imgTag = Regex.Replace(imgTag, @"(/?)>$", $" alt=\"{EscapeHtmlAttribute(captionText)}\"$1>");
            }

            var sb = new StringBuilder();
            sb.Append("<p>").Append(imgTag).Append("</p>");

            if (!string.IsNullOrWhiteSpace(captionText))
            {
                sb.Append("<p>").Append(System.Net.WebUtility.HtmlEncode(captionText)).Append("</p>");
            }

            return sb.ToString();
        }, RegexOptions.IgnoreCase);
    }

    // ── HTML entity decoding ────────────────────────────────────────────

    /// <summary>
    /// Decodes HTML named entities commonly found in WordPress content, such as
    /// smart quotes (&amp;ldquo;, &amp;rdquo;, etc.), numeric entities, and
    /// double-encoded entities (&amp;amp;ldquo;).
    /// Skips text inside <c>&lt;pre&gt;</c>, <c>&lt;code&gt;</c>, <c>&lt;script&gt;</c>,
    /// and <c>&lt;style&gt;</c> tags.
    /// </summary>
    internal static string DecodeHtmlEntities(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Process text nodes outside of pre/code/script/style tags
        // We use a regex to split by these protected blocks and process only the text parts
        var result = Regex.Replace(html, @"(<(?:pre|code|script|style)\b[^>]*>[\s\S]*?</(?:pre|code|script|style)>)|([^<]+)", match =>
        {
            // Group 1: protected block - return as-is
            if (match.Groups[1].Success)
            {
                return match.Value;
            }

            // Group 2: text content - decode entities
            if (match.Groups[2].Success)
            {
                return DecodeQuoteEntities(match.Groups[2].Value);
            }

            return match.Value;
        }, RegexOptions.IgnoreCase);

        return result;
    }

    /// <summary>
    /// Decodes named quote entities and their numeric equivalents.
    /// Handles double-encoded entities (e.g. <c>&amp;amp;ldquo;</c>).
    /// </summary>
    internal static string DecodeQuoteEntities(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        var result = text;
        for (var i = 0; i < 3; i++)
        {
            var prev = result;

            // Named entities
            result = result
                .Replace("&ldquo;", "\u201C")   // left double quotation mark
                .Replace("&rdquo;", "\u201D")   // right double quotation mark
                .Replace("&lsquo;", "\u2018")   // left single quotation mark
                .Replace("&rsquo;", "\u2019")   // right single quotation mark
                .Replace("&quot;", "\"")
                .Replace("&apos;", "'");

            // Numeric entities
            result = result
                .Replace("&#8220;", "\u201C")
                .Replace("&#8221;", "\u201D")
                .Replace("&#8216;", "\u2018")
                .Replace("&#8217;", "\u2019")
                .Replace("&#34;", "\"")
                .Replace("&#39;", "'");

            // Double-encoded entities (e.g. &amp;ldquo;)
            result = result
                .Replace("&amp;ldquo;", "\u201C")
                .Replace("&amp;rdquo;", "\u201D")
                .Replace("&amp;lsquo;", "\u2018")
                .Replace("&amp;rsquo;", "\u2019")
                .Replace("&amp;quot;", "\"")
                .Replace("&amp;apos;", "'");

            // Common HTML entities
            result = result
                .Replace("&amp;amp;", "&amp;")
                .Replace("&amp;lt;", "&lt;")
                .Replace("&amp;gt;", "&gt;");

            if (result == prev)
            {
                break;
            }
        }

        return result;
    }

    // ── Lazy-load attribute cleanup ─────────────────────────────────────

    /// <summary>
    /// Removes lazy-load related attributes from all <c>&lt;img&gt;</c> tags.
    /// This ensures WeChat reads the <c>src</c> attribute directly.
    /// </summary>
    internal static string CleanLazyLoadAttributes(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<img\b([^>]*)(/?)>", match =>
        {
            var attrs = match.Groups[1].Value;
            var selfClose = match.Groups[2].Value;

            // Remove lazy-load attributes
            attrs = Regex.Replace(attrs, @"\s+(?:data-(?:src|original|actualsrc|lazy-src|lazyload|lazy)|srcset|loading|decoding)\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", string.Empty, RegexOptions.IgnoreCase);

            return $"<img{attrs}{selfClose}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Callout → styled section conversion ────────────────────────────

    /// <summary>
    /// Converts <c>&lt;div class="callout"&gt;</c> elements to <c>&lt;section&gt;</c>
    /// with inline styles for WeChat compatibility. Supports callout background color
    /// via the <c>notion-{color}_background</c> class. The class order is flexible –
    /// both <c>class="callout notion-xxx"</c> and <c>class="notion-xxx callout"</c> match.
    /// </summary>
    internal static string ConvertCalloutsToBlockquotes(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Match callout structure: the div must contain "callout" in its class but
        // the notion-xxx token may appear before or after "callout".
        return Regex.Replace(
            html,
            @"<div\s+class=""([^""]*\bcallout\b[^""]*)"">\s*(?:<span\s+class=""callout-icon"">([\s\S]*?)</span>)?\s*<div\s+class=""callout-content"">([\s\S]*)</div>\s*</div>",
            match =>
        {
            var fullClass = match.Groups[1].Value;
            var iconRaw = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
            var contentHtml = match.Groups[3].Value.Trim();

            // Extract Notion color from class list (order-independent)
            string? notionColor = null;
            var colorMatch = Regex.Match(fullClass, @"\bnotion-([a-z_]+)\b");
            if (colorMatch.Success)
            {
                notionColor = colorMatch.Groups[1].Value;
            }

            // Determine background color from Notion color class
            var bgColor = NotionColorPalette.DefaultBg;
            if (!string.IsNullOrWhiteSpace(notionColor))
            {
                bgColor = NotionColorToBackground(notionColor);
            }

            // Extract icon text
            var iconText = string.IsNullOrWhiteSpace(iconRaw)
                ? string.Empty
                : WechatSyncHelpers.StripHtml(iconRaw, 50).Trim();

            var iconSpan = string.IsNullOrWhiteSpace(iconText)
                ? string.Empty
                : $"<span style=\"margin-right:8px;font-size:1.3em\">{iconText}</span>";

            return $"<section style=\"display:flex;padding:16px;border-radius:4px;background:{bgColor};margin:8px 0\">{iconSpan}<span>{contentHtml}</span></section>";
        },
            RegexOptions.IgnoreCase);
    }

    // ── Toggle (details/summary) → expanded content ─────────────────────

    /// <summary>
    /// Expands <c>&lt;details&gt;&lt;summary&gt;</c> elements to visible content,
    /// since WeChat does not support the HTML5 details/summary elements.
    /// The summary becomes a bold paragraph, followed by the toggle content.
    /// If the summary contains a heading tag (toggleable heading), the heading
    /// is preserved instead of wrapping in strong.
    /// Block-level Notion color classes on <c>&lt;details&gt;</c> are preserved
    /// as inline styles on a wrapper div.
    /// </summary>
    internal static string ExpandToggleBlocks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<details\b([^>]*)>\s*<summary>([\s\S]*?)</summary>([\s\S]*?)</details>", match =>
        {
            var detailsAttrs = match.Groups[1].Value;
            var summary = match.Groups[2].Value.Trim();
            var content = match.Groups[3].Value.Trim();

            // Extract notion color class from details attributes if present
            string colorStyle = string.Empty;
            var colorMatch = Regex.Match(detailsAttrs, @"class=""[^""]*\bnotion-([a-z_]+)\b[^""]*""");
            if (colorMatch.Success)
            {
                var notionColor = colorMatch.Groups[1].Value;
                if (notionColor.EndsWith("_background", StringComparison.OrdinalIgnoreCase))
                {
                    colorStyle = $" style=\"background-color:{NotionColorToBackground(notionColor)}\"";
                }
                else
                {
                    colorStyle = $" style=\"color:{NotionColorToForeground(notionColor)}\"";
                }
            }

            // Check if summary contains a heading tag (toggleable heading)
            var headingMatch = Regex.Match(summary, @"^<(h[1-6])\b[^>]*>[\s\S]*?</\1>$", RegexOptions.IgnoreCase);

            var sb = new StringBuilder();
            if (headingMatch.Success)
            {
                // Preserve the heading tag for toggleable headings
                sb.Append(summary);
            }
            else
            {
                sb.Append("<p><strong>").Append(summary).Append("</strong></p>");
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                sb.Append(content);
            }

            var result = sb.ToString();

            // If there was a notion color class, wrap in a colored div
            if (!string.IsNullOrEmpty(colorStyle))
            {
                result = $"<div{colorStyle}>{result}</div>";
            }

            return result;
        }, RegexOptions.IgnoreCase);
    }

    // ── To-do → text with checkbox markers ──────────────────────────────

    /// <summary>
    /// Converts <c>&lt;div class="to-do"&gt;</c> elements to simple text paragraphs
    /// with Unicode checkbox markers (☑/☐) for WeChat compatibility.
    /// Checked items are rendered with strikethrough style.
    /// The input attribute order (type, disabled, checked) is flexible.
    /// </summary>
    internal static string ConvertToDosToParagraphs(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Match to-do div containing an <input> with flexible attribute order + <span>
        return Regex.Replace(html, @"<div\s+class=""([^""]*\bto-do\b[^""]*)"">\s*<input\b([^>]*)/?>\s*<span>([\s\S]*?)</span>[\s\S]*?</div>", match =>
        {
            var inputAttrs = match.Groups[2].Value;
            var isChecked = Regex.IsMatch(inputAttrs, @"\bchecked\b", RegexOptions.IgnoreCase);
            var marker = isChecked ? "\u2611" : "\u2610"; // ☑ or ☐
            var text = match.Groups[3].Value.Trim();

            if (isChecked)
            {
                return $"<p><s style=\"color:{NotionColorPalette.GrayFg}\">{marker} {text}</s></p>";
            }

            return $"<p>{marker} {text}</p>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Video embed → link ──────────────────────────────────────────────

    /// <summary>
    /// Converts <c>&lt;div class="video-embed"&gt;&lt;iframe&gt;</c> elements to
    /// clickable links, since WeChat does not support iframe embeds.
    /// Also converts standalone <c>&lt;iframe&gt;</c> elements within <c>&lt;figure&gt;</c>.
    /// </summary>
    internal static string ConvertVideoEmbedsToLinks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Convert <div class="video-embed"><iframe src="URL" ...></iframe></div>
        var result = Regex.Replace(html, @"<div\s+class=""video-embed"">\s*<iframe\s+src=""([^""]+)""[^>]*>\s*</iframe>\s*</div>", match =>
        {
            var url = match.Groups[1].Value;
            var displayUrl = System.Net.WebUtility.HtmlEncode(url);
            return $"<p><a href=\"{displayUrl}\">\u25B6 \u89C6\u9891\u94FE\u63A5</a></p>";
        }, RegexOptions.IgnoreCase);

        return result;
    }

    // ── Iframe → link ───────────────────────────────────────────────────

    /// <summary>
    /// Converts remaining <c>&lt;iframe&gt;</c> elements to clickable links.
    /// This covers standalone iframes and iframes inside figure blocks that
    /// are not wrapped by <c>div.video-embed</c>.
    /// </summary>
    internal static string ConvertIframesToLinks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<iframe\b[^>]*\bsrc=""([^""]+)""[^>]*>\s*</iframe>", match =>
        {
            var url = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(url))
            {
                return match.Value;
            }

            var displayUrl = System.Net.WebUtility.HtmlEncode(url);
            return $"<p><a href=\"{displayUrl}\">\u25B6 \u5185\u5D4C\u5185\u5BB9\u94FE\u63A5</a></p>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Video tag → link ────────────────────────────────────────────────

    /// <summary>
    /// Converts <c>&lt;video&gt;</c> tags to clickable links because WeChat article
    /// content does not reliably support native HTML5 video playback.
    /// </summary>
    internal static string ConvertVideoTagsToLinks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<video\b([^>]*)>([\s\S]*?)</video>|<video\b([^>]*)/?>", match =>
        {
            var attrs = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
            var srcMatch = Regex.Match(attrs, @"\bsrc=""([^""]+)""", RegexOptions.IgnoreCase);
            if (!srcMatch.Success)
            {
                return match.Value;
            }

            var url = srcMatch.Groups[1].Value;
            var displayUrl = System.Net.WebUtility.HtmlEncode(url);
            return $"<p><a href=\"{displayUrl}\">\u25B6 \u89C6\u9891\u94FE\u63A5</a></p>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Audio tag → link ─────────────────────────────────────────────────

    /// <summary>
    /// Converts <c>&lt;audio&gt;</c> tags to clickable links because WeChat article
    /// content does not support native HTML5 audio playback.
    /// </summary>
    internal static string ConvertAudioTagsToLinks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<audio\b([^>]*)>([\s\S]*?)</audio>|<audio\b([^>]*)/?>", match =>
        {
            var attrs = match.Groups[1].Success && match.Groups[1].Length > 0
                ? match.Groups[1].Value
                : match.Groups[3].Value;

            // Try to extract src from attributes
            var srcMatch = Regex.Match(attrs, @"\bsrc=""([^""]+)""", RegexOptions.IgnoreCase);
            if (!srcMatch.Success)
            {
                // Try to find <source src="..."> inside the audio tag body
                var body = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
                srcMatch = Regex.Match(body, @"<source\b[^>]*\bsrc=""([^""]+)""", RegexOptions.IgnoreCase);
            }

            if (!srcMatch.Success)
            {
                return match.Value;
            }

            var url = srcMatch.Groups[1].Value;
            var displayUrl = System.Net.WebUtility.HtmlEncode(url);
            return $"<p><a href=\"{displayUrl}\">\uD83C\uDFB5 \u97F3\u9891\u94FE\u63A5</a></p>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Inline math fallback ────────────────────────────────────────────

    /// <summary>
    /// Converts inline equation spans (<c>span.math-inline</c>) to simple inline code.
    /// </summary>
    internal static string ConvertInlineMathToCode(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<span\s+class=""math-inline"">\s*\\\(([\s\S]*?)\\\)\s*</span>", match =>
        {
            var expression = match.Groups[1].Value.Trim();
            expression = System.Net.WebUtility.HtmlDecode(expression);
            var encoded = System.Net.WebUtility.HtmlEncode(expression);
            return $"<code>{encoded}</code>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Table inline styles ─────────────────────────────────────────────

    /// <summary>
    /// Adds inline styles to <c>&lt;table&gt;</c>, <c>&lt;th&gt;</c>, and <c>&lt;td&gt;</c>
    /// elements for proper rendering in WeChat, which requires inline styles.
    /// </summary>
    internal static string AddTableInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string tableStyle = "border-collapse:collapse;width:100%;margin:8px 0";
        const string cellStyle = "border:1px solid #e3e2e0;padding:6px 10px;text-align:left";
        const string headerCellStyle = "border:1px solid #e3e2e0;padding:6px 10px;text-align:left;font-weight:600;background:#f7f6f3";

        // Add style to <table> tags that don't already have a style attribute
        var result = Regex.Replace(html, @"<table(?!\s+[^>]*style\s*=)(\s[^>]*)?>", match =>
        {
            var attrs = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return $"<table style=\"{tableStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);

        // Add style to <th> tags
        result = Regex.Replace(result, @"<th(?!\s+[^>]*style\s*=)(\s[^>]*)?>", match =>
        {
            var attrs = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return $"<th style=\"{headerCellStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);

        // Add style to <td> tags
        result = Regex.Replace(result, @"<td(?!\s+[^>]*style\s*=)(\s[^>]*)?>", match =>
        {
            var attrs = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return $"<td style=\"{cellStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);

        return result;
    }

    // ── Math block fallback ─────────────────────────────────────────────

    /// <summary>
    /// Converts <c>&lt;div class="math-block"&gt;</c> elements to a plain-text
    /// representation with a code block, since WeChat does not support
    /// MathJax/KaTeX rendering.
    /// </summary>
    internal static string ConvertMathBlocksToCode(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<div\s+class=""math-block"">([\s\S]*?)</div>", match =>
        {
            var expression = match.Groups[1].Value.Trim();
            // Remove the \[ and \] delimiters if present
            expression = Regex.Replace(expression, @"^\\\[|\\\]$", string.Empty).Trim();
            // Decode HTML entities back for display
            expression = System.Net.WebUtility.HtmlDecode(expression);
            var encoded = System.Net.WebUtility.HtmlEncode(expression);

            return $"<pre><code>{encoded}</code></pre>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Full content processing pipeline ────────────────────────────────

    /// <summary>
    /// Runs the full content processing pipeline:
    ///  1. Convert figure tags to p tags
    ///  2. Convert callout blocks to styled sections
    ///  3. Expand toggle (details/summary) blocks (preserves heading tags and block color)
    ///  4. Convert to-do blocks to paragraphs
    ///  5. Convert video embeds to links
    ///  6. Convert remaining iframes to links
    ///  7. Convert video tags to links
    ///  8. Convert audio tags to links
    ///  9. Convert columns to vertical stack
    /// 10. Add table inline styles
    /// 11. Convert math blocks to code blocks
    /// 12. Convert inline math to code
    /// 13. Convert nav TOC elements (remove or convert to div)
    /// 14. Convert internal links (link-to-page, child-page, child-database) to text
    /// 15. Convert children container divs (callout-children, to-do-children) to inline styles
    /// 16. Strip synced-block wrapper class
    /// 17. Convert bookmark links to inline-styled block links
    /// 18. Convert file/pdf classes to inline margin
    /// 19. Strip mention class
    /// 20. Convert notion color classes to inline styles (whitelist-based)
    /// 21. Add code block and inline code styles
    /// 22. Add blockquote inline styles
    /// 23. Add hr inline styles
    /// 24. Add img inline styles (responsive sizing)
    /// 25. Add heading inline styles (margin-top)
    /// 26. Add link inline styles
    /// 27. Decode HTML entities
    /// 28. Optionally clean lazy-load attributes
    /// </summary>
    internal static string ProcessContent(string html, bool preserveLazyLoadAttributes = false)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        var result = ConvertFiguresToParagraphs(html);
        result = ConvertCalloutsToBlockquotes(result);
        result = ExpandToggleBlocks(result);
        result = ConvertToDosToParagraphs(result);
        result = ConvertVideoEmbedsToLinks(result);
        result = ConvertIframesToLinks(result);
        result = ConvertVideoTagsToLinks(result);
        result = ConvertAudioTagsToLinks(result);
        result = ConvertColumnsToVerticalStack(result);
        result = AddTableInlineStyles(result);
        result = ConvertMathBlocksToCode(result);
        result = ConvertInlineMathToCode(result);
        result = ConvertNavTocToDiv(result);
        result = ConvertInternalLinksToText(result);
        result = ConvertChildrenDivsToInlineStyles(result);
        result = StripSyncedBlockClass(result);
        result = ConvertBookmarksToInlineStyles(result);
        result = ConvertFilePdfClassesToInlineStyles(result);
        result = StripMentionClass(result);
        result = ConvertNotionColorClassesToInlineStyles(result);
        result = AddCodeBlockInlineStyles(result);
        result = AddBlockquoteInlineStyles(result);
        result = AddHrInlineStyles(result);
        result = AddImgInlineStyles(result);
        result = AddHeadingInlineStyles(result);
        result = AddLinkInlineStyles(result);
        result = DecodeHtmlEntities(result);
        if (!preserveLazyLoadAttributes)
        {
            result = CleanLazyLoadAttributes(result);
        }
        return result;
    }

    // ── Columns → vertical stack ──────────────────────────────────────

    /// <summary>
    /// Converts <c>&lt;div class="notion-columns"&gt;</c> grid layouts to vertical
    /// stacking by injecting <c>display:block</c>. WeChat does not support CSS grid.
    /// Individual <c>notion-column</c> divs are also set to <c>display:block;width:100%</c>.
    /// </summary>
    internal static string ConvertColumnsToVerticalStack(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Replace notion-columns div: inject display:block to override grid
        var result = Regex.Replace(html,
            @"<div\s+class=""notion-columns""(\s[^>]*)?>",
            match =>
            {
                var extra = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
                return $"<div class=\"notion-columns\" style=\"display:block\"{extra}>";
            }, RegexOptions.IgnoreCase);

        // Replace notion-column div: inject display:block;width:100%
        result = Regex.Replace(result,
            @"<div\s+class=""notion-column""(\s[^>]*)?>",
            match =>
            {
                var extra = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
                return $"<div class=\"notion-column\" style=\"display:block;width:100%\"{extra}>";
            }, RegexOptions.IgnoreCase);

        return result;
    }

    // ── Notion color class → inline style conversion ───────────────────

    /// <summary>
    /// Converts <c>class="notion-{color}"</c> attributes to inline styles for WeChat
    /// compatibility. Handles both foreground colors (e.g. <c>notion-blue</c>) and
    /// background colors (e.g. <c>notion-yellow_background</c>).
    /// Uses a whitelist of the 18 valid Notion color names to avoid false positives
    /// on non-color classes like <c>notion-columns</c> or <c>notion-file</c>.
    /// When the element already has a <c>style</c> attribute, the new style is appended
    /// instead of creating a duplicate attribute.
    /// </summary>
    internal static string ConvertNotionColorClassesToInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Whitelist of the 18 valid Notion color names (9 foreground + 9 background)
        const string colorPattern =
            @"\bnotion-(gray|brown|orange|yellow|green|blue|purple|pink|red" +
            @"|gray_background|brown_background|orange_background|yellow_background" +
            @"|green_background|blue_background|purple_background|pink_background|red_background)\b";

        // Match an opening tag that contains a class with a notion-xxx token.
        // Capture the entire opening tag so we can inspect / rewrite all attributes.
        return Regex.Replace(html, @"<(\w+)\b([^>]*)>", match =>
        {
            var tagName = match.Groups[1].Value;
            var attrs = match.Groups[2].Value;

            // Only process if there is a notion-xxx class
            var classMatch = Regex.Match(attrs, @"\bclass=""([^""]*)""", RegexOptions.IgnoreCase);
            if (!classMatch.Success)
            {
                return match.Value;
            }

            var fullClass = classMatch.Groups[1].Value;
            var notionMatch = Regex.Match(fullClass, colorPattern);
            if (!notionMatch.Success)
            {
                return match.Value;
            }

            var notionColor = notionMatch.Groups[1].Value;

            // Remove only the matched notion color class token from class attribute
            var cleanedClass = Regex.Replace(fullClass, colorPattern, string.Empty).Trim();
            cleanedClass = Regex.Replace(cleanedClass, @"\s{2,}", " ").Trim();

            // Build the new CSS property
            string cssProp;
            if (notionColor.EndsWith("_background", StringComparison.OrdinalIgnoreCase))
            {
                cssProp = $"background-color:{NotionColorToBackground(notionColor)}";
            }
            else
            {
                cssProp = $"color:{NotionColorToForeground(notionColor)}";
            }

            // Rebuild the class attribute (or remove if empty)
            var newClassAttr = string.IsNullOrWhiteSpace(cleanedClass)
                ? string.Empty
                : $"class=\"{cleanedClass}\"";

            // Remove old class attribute from attrs string
            var newAttrs = attrs.Remove(classMatch.Index, classMatch.Length).Trim();

            // Check for existing style attribute and merge
            var styleMatch = Regex.Match(newAttrs, @"\bstyle=""([^""]*)""", RegexOptions.IgnoreCase);
            if (styleMatch.Success)
            {
                var existingStyle = styleMatch.Groups[1].Value.TrimEnd().TrimEnd(';');
                var mergedStyle = string.IsNullOrWhiteSpace(existingStyle)
                    ? cssProp
                    : $"{existingStyle};{cssProp}";
                newAttrs = newAttrs.Remove(styleMatch.Index, styleMatch.Length)
                                   .Insert(styleMatch.Index, $"style=\"{mergedStyle}\"");
            }
            else
            {
                newAttrs = $"{newAttrs} style=\"{cssProp}\"".Trim();
            }

            // Re-insert the class attribute if it still has value
            if (!string.IsNullOrWhiteSpace(newClassAttr))
            {
                newAttrs = $"{newClassAttr} {newAttrs}".Trim();
            }

            return $"<{tagName} {newAttrs}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Code block inline styles ─────────────────────────────────────────

    /// <summary>
    /// Adds inline styles to <c>&lt;pre&gt;</c> and <c>&lt;code&gt;</c> elements for
    /// proper rendering in WeChat, which requires inline styles.
    /// <c>&lt;code&gt;</c> inside <c>&lt;pre&gt;</c> gets font-family only, while
    /// standalone inline <c>&lt;code&gt;</c> gets background, padding, border-radius
    /// and font styling.
    /// </summary>
    internal static string AddCodeBlockInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string preStyle = "background:#f7f6f3;padding:16px;border-radius:4px;overflow-x:auto;font-size:14px";
        const string codeInPreStyle = "font-family:Menlo,Consolas,monospace";
        const string inlineCodeStyle = "background:#f7f6f3;padding:2px 4px;border-radius:3px;font-family:Menlo,Consolas,monospace;font-size:90%";

        // Step 1: Add style to <pre> tags that don't already have a style attribute
        var result = Regex.Replace(html, @"<pre(?!\s+[^>]*style\s*=)(\s[^>]*)?>", match =>
        {
            var attrs = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return $"<pre style=\"{preStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);

        // Step 2: Style <code> tags inside <pre>...</pre> blocks with font-family only
        result = Regex.Replace(result, @"(<pre\b[^>]*>)([\s\S]*?)(</pre>)", match =>
        {
            var preOpen = match.Groups[1].Value;
            var inner = match.Groups[2].Value;
            var preClose = match.Groups[3].Value;
            inner = Regex.Replace(inner, @"<code(?!\s+[^>]*style\s*=)(\s[^>]*)?>", codeMatch =>
            {
                var attrs = codeMatch.Groups[1].Success ? codeMatch.Groups[1].Value : string.Empty;
                return $"<code style=\"{codeInPreStyle}\"{attrs}>";
            }, RegexOptions.IgnoreCase);
            return $"{preOpen}{inner}{preClose}";
        }, RegexOptions.IgnoreCase);

        // Step 3: Style remaining standalone <code> tags (inline code) with full styles
        result = Regex.Replace(result, @"<code(?!\s+[^>]*style\s*=)(\s[^>]*)?>", match =>
        {
            var attrs = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return $"<code style=\"{inlineCodeStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);

        return result;
    }

    // ── Blockquote inline styles ─────────────────────────────────────────

    /// <summary>
    /// Adds inline styles to <c>&lt;blockquote&gt;</c> elements for proper rendering
    /// in WeChat, which requires inline styles.
    /// </summary>
    internal static string AddBlockquoteInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string bqStyle = "border-left:3px solid #e3e2e0;padding-left:14px;margin:8px 0;color:inherit";

        return Regex.Replace(html, @"<blockquote(?!\s+[^>]*style\s*=)(\s[^>]*)?>", match =>
        {
            var attrs = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return $"<blockquote style=\"{bqStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Nav TOC → remove or convert ──────────────────────────────────────

    /// <summary>
    /// Converts or removes <c>&lt;nav class="notion-toc"&gt;</c> elements since
    /// WeChat does not support the <c>&lt;nav&gt;</c> tag. Empty TOC placeholders
    /// are removed entirely; non-empty ones are converted to <c>&lt;div&gt;</c>.
    /// </summary>
    internal static string ConvertNavTocToDiv(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<nav\s+class=""notion-toc"">([\s\S]*?)</nav>", match =>
        {
            var inner = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(inner))
            {
                return string.Empty; // Remove empty TOC placeholder
            }

            return $"<div>{inner}</div>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Internal links → plain text ──────────────────────────────────────

    /// <summary>
    /// Converts Notion internal link elements to plain text paragraphs for WeChat
    /// compatibility. Handles <c>notion-link-to-page</c>, <c>notion-child-page</c>,
    /// and <c>notion-child-database</c> elements whose targets cannot be resolved
    /// to external URLs.
    /// </summary>
    internal static string ConvertInternalLinksToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Convert notion-link-to-page: <p class="notion-link-to-page"><a data-notion-id="...">text</a></p>
        var result = Regex.Replace(html,
            @"<p\s+class=""notion-link-to-page"">\s*<a\b[^>]*>([\s\S]*?)</a>\s*</p>", match =>
            {
                var text = match.Groups[1].Value.Trim();
                return $"<p style=\"margin:6px 0\">{text}</p>";
            }, RegexOptions.IgnoreCase);

        // Convert notion-child-page and notion-child-database: <p class="notion-child-page">text</p>
        result = Regex.Replace(result,
            @"<p\s+class=""notion-(?:child-page|child-database)"">([\s\S]*?)</p>", match =>
            {
                var text = match.Groups[1].Value.Trim();
                return $"<p style=\"margin:6px 0\">{text}</p>";
            }, RegexOptions.IgnoreCase);

        return result;
    }

    // ── Children container divs → inline styles ──────────────────────────

    /// <summary>
    /// Converts <c>callout-children</c> and <c>to-do-children</c> divs to use
    /// inline styles instead of CSS classes for WeChat compatibility.
    /// </summary>
    internal static string ConvertChildrenDivsToInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        // Convert callout-children to inline styled div
        var result = Regex.Replace(html, @"<div\s+class=""callout-children"">",
            "<div style=\"margin-top:8px\">", RegexOptions.IgnoreCase);

        // Convert to-do-children to inline styled div
        result = Regex.Replace(result, @"<div\s+class=""to-do-children"">",
            "<div style=\"margin:4px 0 4px 24px\">", RegexOptions.IgnoreCase);

        return result;
    }

    // ── Synced block wrapper → strip class ──────────────────────────────

    /// <summary>
    /// Removes the <c>notion-synced-block</c> class from wrapper divs since
    /// WeChat does not use external CSS classes.
    /// </summary>
    internal static string StripSyncedBlockClass(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<div\s+class=""notion-synced-block"">",
            "<div>", RegexOptions.IgnoreCase);
    }

    // ── Bookmark → inline styled block link ─────────────────────────────

    /// <summary>
    /// Converts <c>&lt;a class="bookmark"&gt;</c> elements to use inline styles
    /// for WeChat compatibility. Bookmark links are styled as block-level cards
    /// with border, padding, and border-radius.
    /// Also handles <c>bookmark notion-link-preview</c> class combinations.
    /// </summary>
    internal static string ConvertBookmarksToInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string bookmarkStyle = "display:block;padding:12px 14px;border:1px solid #e3e2e0;border-radius:4px;color:inherit;text-decoration:none;margin:4px 0";

        // Match <a ... class="...bookmark..." ...> and inject inline styles, removing the bookmark class
        return Regex.Replace(html, @"<a\b([^>]*\bclass=""[^""]*\bbookmark\b[^""]*""[^>]*)>", match =>
        {
            var attrs = match.Groups[1].Value;

            // Remove "bookmark" and "notion-link-preview" from class
            attrs = Regex.Replace(attrs, @"\bclass=""([^""]*)""", classMatch =>
            {
                var classes = classMatch.Groups[1].Value;
                classes = Regex.Replace(classes, @"\b(?:bookmark|notion-link-preview)\b", string.Empty);
                classes = Regex.Replace(classes, @"\s{2,}", " ").Trim();
                return string.IsNullOrWhiteSpace(classes) ? string.Empty : $"class=\"{classes}\"";
            }, RegexOptions.IgnoreCase);
            attrs = attrs.Trim();

            // Check for existing style and merge
            var styleMatch = Regex.Match(attrs, @"\bstyle=""([^""]*)""", RegexOptions.IgnoreCase);
            if (styleMatch.Success)
            {
                var existing = styleMatch.Groups[1].Value.TrimEnd().TrimEnd(';');
                var merged = string.IsNullOrWhiteSpace(existing)
                    ? bookmarkStyle
                    : $"{existing};{bookmarkStyle}";
                attrs = attrs.Remove(styleMatch.Index, styleMatch.Length)
                             .Insert(styleMatch.Index, $"style=\"{merged}\"");
            }
            else
            {
                attrs = $"style=\"{bookmarkStyle}\" {attrs}".Trim();
            }

            return $"<a {attrs}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── <img> inline styles ─────────────────────────────────────────────

    /// <summary>
    /// Adds <c>max-width:100%;height:auto</c> to all <c>&lt;img&gt;</c> tags
    /// for responsive image display in WeChat articles.
    /// </summary>
    internal static string AddImgInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string imgStyle = "max-width:100%;height:auto";

        return Regex.Replace(html, @"<img\b(?!\s+[^>]*style\s*=)([^>]*?)(/?)>", match =>
        {
            var attrs = match.Groups[1].Value;
            var selfClose = match.Groups[2].Value;
            return $"<img style=\"{imgStyle}\"{attrs}{selfClose}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Heading inline styles ───────────────────────────────────────────

    /// <summary>
    /// Adds <c>margin-top:1.2em</c> to <c>&lt;h1&gt;</c>, <c>&lt;h2&gt;</c>, and
    /// <c>&lt;h3&gt;</c> tags for consistent heading spacing in WeChat articles.
    /// Skips headings that already have a <c>style</c> attribute.
    /// </summary>
    internal static string AddHeadingInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string headingStyle = "margin-top:1.2em";

        return Regex.Replace(html, @"<(h[1-3])\b(?!\s+[^>]*style\s*=)([^>]*)>", match =>
        {
            var tag = match.Groups[1].Value;
            var attrs = match.Groups[2].Value;
            return $"<{tag} style=\"{headingStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── notion-file / notion-pdf → inline styles ────────────────────────

    /// <summary>
    /// Converts <c>class="notion-file"</c> and <c>class="notion-pdf"</c> to
    /// inline margin styles for WeChat compatibility.
    /// </summary>
    internal static string ConvertFilePdfClassesToInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string fileStyle = "margin:6px 0";

        return Regex.Replace(html, @"<p\s+class=""notion-(?:file|pdf)""([^>]*)>", match =>
        {
            var extra = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return $"<p style=\"{fileStyle}\"{extra}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── notion-mention → strip class ────────────────────────────────────

    /// <summary>
    /// Strips the <c>notion-mention</c> class from mention spans for WeChat,
    /// keeping the <c>data-mention-type</c> attribute for potential downstream use.
    /// </summary>
    internal static string StripMentionClass(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        return Regex.Replace(html, @"<span\s+class=""notion-mention""",
            "<span", RegexOptions.IgnoreCase);
    }

    // ── <hr> inline styles ──────────────────────────────────────────────

    /// <summary>
    /// Adds inline styles to <c>&lt;hr&gt;</c> elements for proper rendering
    /// in WeChat, which requires inline styles.
    /// </summary>
    internal static string AddHrInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string hrStyle = "border:none;border-top:1px solid #e3e2e0;margin:16px 0";

        return Regex.Replace(html, @"<hr\s*/?>", _ =>
        {
            return $"<hr style=\"{hrStyle}\" />";
        }, RegexOptions.IgnoreCase);
    }

    // ── <a> link inline styles ──────────────────────────────────────────

    /// <summary>
    /// Adds inline color to <c>&lt;a&gt;</c> elements for consistent link styling
    /// in WeChat articles, which may override link colors.
    /// Uses the standard WeChat article link color (#576b95).
    /// </summary>
    internal static string AddLinkInlineStyles(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }

        const string linkStyle = "color:#576b95;text-decoration:none";

        return Regex.Replace(html, @"<a\b(?!\s+[^>]*style\s*=)([^>]*)>", match =>
        {
            var attrs = match.Groups[1].Value;
            return $"<a style=\"{linkStyle}\"{attrs}>";
        }, RegexOptions.IgnoreCase);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string EscapeHtmlAttribute(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>
    /// Maps Notion color names to CSS foreground color hex values.
    /// Delegates to the shared <see cref="NotionColorPalette"/>.
    /// </summary>
    private static string NotionColorToForeground(string notionColor)
        => NotionColorPalette.ToForeground(notionColor);

    /// <summary>
    /// Maps Notion color names to CSS background color hex values.
    /// Accepts both "xxx_background" and plain "xxx" forms.
    /// Delegates to the shared <see cref="NotionColorPalette"/>.
    /// </summary>
    private static string NotionColorToBackground(string notionColor)
        => NotionColorPalette.ToBackground(notionColor);
}
