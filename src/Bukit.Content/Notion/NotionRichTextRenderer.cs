using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Bukit.Content.Notion;

public static class NotionRichTextRenderer
{
    public static string Render(JsonElement richTextArray)
    {
        if (richTextArray.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in richTextArray.EnumerateArray())
        {
            // Handle inline equations (type: "equation")
            var itemType = GetStringValue(item, "type");
            if (string.Equals(itemType, "equation", StringComparison.OrdinalIgnoreCase))
            {
                var expression = string.Empty;
                if (item.TryGetProperty("equation", out var eqObj) &&
                    eqObj.ValueKind == JsonValueKind.Object)
                {
                    expression = GetStringValue(eqObj, "expression") ?? string.Empty;
                }
                else
                {
                    // Fallback: use plain_text
                    expression = GetStringValue(item, "plain_text") ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(expression))
                {
                    var encoded = WebUtility.HtmlEncode(expression);
                    sb.Append($"<span class=\"math-inline\">\\({encoded}\\)</span>");
                }

                continue;
            }

            // Handle mention types (user, page, database, date, link_preview, template_mention)
            if (string.Equals(itemType, "mention", StringComparison.OrdinalIgnoreCase))
            {
                var mentionText = GetStringValue(item, "plain_text") ?? string.Empty;
                if (string.IsNullOrEmpty(mentionText))
                {
                    continue;
                }

                var encoded = WebUtility.HtmlEncode(mentionText);
                var href = GetHref(item);
                var mentionSubtype = string.Empty;
                if (item.TryGetProperty("mention", out var mentionObj) && mentionObj.ValueKind == JsonValueKind.Object)
                {
                    mentionSubtype = GetStringValue(mentionObj, "type") ?? string.Empty;
                }

                string mentionHtml;
                if (!string.IsNullOrWhiteSpace(href))
                {
                    mentionHtml = $"<a href=\"{WebUtility.HtmlEncode(href)}\">{encoded}</a>";
                }
                else
                {
                    mentionHtml = encoded;
                }

                // Apply annotations if present
                var mentionAnn = item.TryGetProperty("annotations", out var mAnn) ? mAnn : default;
                if (mentionAnn.ValueKind == JsonValueKind.Object)
                {
                    mentionHtml = ApplyAnnotations(mentionHtml, mentionAnn);
                }

                sb.Append($"<span class=\"notion-mention\" data-mention-type=\"{WebUtility.HtmlEncode(mentionSubtype)}\">{mentionHtml}</span>");
                continue;
            }

            if (!item.TryGetProperty("plain_text", out var plainTextEl) || plainTextEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = WebUtility.HtmlEncode(plainTextEl.GetString() ?? string.Empty);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var href2 = GetHref(item);
            var annotations = item.TryGetProperty("annotations", out var ann) ? ann : default;

            if (!string.IsNullOrWhiteSpace(href2))
            {
                text = $"<a href=\"{WebUtility.HtmlEncode(href2)}\">{text}</a>";
            }

            if (annotations.ValueKind == JsonValueKind.Object)
            {
                text = ApplyAnnotations(text, annotations);
            }

            sb.Append(text);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Applies Notion rich text annotations (bold, italic, underline, strikethrough, code, color)
    /// to an HTML string fragment.
    /// </summary>
    private static string ApplyAnnotations(string text, JsonElement annotations)
    {
        if (GetBool(annotations, "code"))
        {
            text = $"<code>{text}</code>";
        }

        if (GetBool(annotations, "bold"))
        {
            text = $"<strong>{text}</strong>";
        }

        if (GetBool(annotations, "italic"))
        {
            text = $"<em>{text}</em>";
        }

        if (GetBool(annotations, "underline"))
        {
            text = $"<u>{text}</u>";
        }

        if (GetBool(annotations, "strikethrough"))
        {
            text = $"<s>{text}</s>";
        }

        // Color annotation: foreground and background colors
        var color = GetStringValue(annotations, "color");
        if (!string.IsNullOrWhiteSpace(color) &&
            !string.Equals(color, "default", StringComparison.OrdinalIgnoreCase))
        {
            text = WrapWithColor(text, color);
        }

        return text;
    }

    private static bool GetBool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v))
        {
            return false;
        }

        return v.ValueKind == JsonValueKind.True;
    }

    private static string? GetHref(JsonElement richTextItem)
    {
        if (richTextItem.TryGetProperty("href", out var hrefEl) && hrefEl.ValueKind == JsonValueKind.String)
        {
            return hrefEl.GetString();
        }

        if (richTextItem.TryGetProperty("text", out var textEl) &&
            textEl.ValueKind == JsonValueKind.Object &&
            textEl.TryGetProperty("link", out var linkEl) &&
            linkEl.ValueKind == JsonValueKind.Object &&
            linkEl.TryGetProperty("url", out var urlEl) &&
            urlEl.ValueKind == JsonValueKind.String)
        {
            return urlEl.GetString();
        }

        return null;
    }

    private static string? GetStringValue(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    /// <summary>
    /// Wraps text with a span for Notion color annotations.
    /// Notion colors: gray, brown, orange, yellow, green, blue, purple, pink, red
    /// Background variants: {color}_background (e.g. "red_background")
    /// </summary>
    private static string WrapWithColor(string text, string color)
    {
        // Map Notion color names to CSS color values
        if (color.EndsWith("_background", StringComparison.OrdinalIgnoreCase))
        {
            var baseColor = color[..^"_background".Length];
            var cssColor = NotionColorToCss(baseColor);
            return $"<span style=\"background-color:{cssColor}\">{text}</span>";
        }
        else
        {
            var cssColor = NotionColorToCss(color);
            return $"<span style=\"color:{cssColor}\">{text}</span>";
        }
    }

    private static string NotionColorToCss(string notionColor)
    {
        var result = NotionColorPalette.ToForeground(notionColor);
        // If the palette returns "inherit" (unknown color), fall back to the color name as-is
        return string.Equals(result, "inherit", StringComparison.Ordinal) ? notionColor : result;
    }
}

