using System.Net;
using System.Text;
using System.Text.Json;

namespace Bukit.Content.Notion.BlockRenderers;

/// <summary>
/// Shared helper methods used by individual block renderers.
/// </summary>
internal static class NotionBlockHelpers
{
    internal static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    internal static string ExtractPlainText(JsonElement richTextArray)
    {
        if (richTextArray.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in richTextArray.EnumerateArray())
        {
            if (item.TryGetProperty("plain_text", out var plainTextEl) && plainTextEl.ValueKind == JsonValueKind.String)
            {
                sb.Append(plainTextEl.GetString());
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reads the <c>color</c> property from a Notion block's type container (e.g. <c>block.paragraph.color</c>)
    /// and returns the CSS class attribute fragment (e.g. <c> class="notion-blue"</c>).
    /// Returns empty string when color is "default" or absent.
    /// </summary>
    internal static string GetBlockColorClass(JsonElement typeContainer)
    {
        var color = GetString(typeContainer, "color");
        if (string.IsNullOrWhiteSpace(color) ||
            string.Equals(color, "default", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return $" class=\"notion-{WebUtility.HtmlEncode(color)}\"";
    }

    /// <summary>
    /// Returns the Notion block color value (e.g. "blue", "red_background") or null if default/absent.
    /// </summary>
    internal static string? GetBlockColor(JsonElement typeContainer)
    {
        var color = GetString(typeContainer, "color");
        if (string.IsNullOrWhiteSpace(color) ||
            string.Equals(color, "default", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return color;
    }

    /// <summary>
    /// Maps a Notion block-level color to its CSS background color hex value.
    /// Used when a block color like "blue_background" needs to be rendered as inline style.
    /// </summary>
    internal static string NotionBlockColorToCssBackground(string notionColor)
    {
        return NotionColorPalette.ToBackground(notionColor);
    }

    internal static string? ExtractFileUrl(JsonElement container)
    {
        var fileType = GetString(container, "type");
        if (fileType == "external" &&
            container.TryGetProperty("external", out var ext) &&
            ext.ValueKind == JsonValueKind.Object)
        {
            return GetString(ext, "url");
        }

        if (fileType == "file" &&
            container.TryGetProperty("file", out var file) &&
            file.ValueKind == JsonValueKind.Object)
        {
            return GetString(file, "url");
        }

        return null;
    }

    internal static bool IsYouTubeUrl(string url, out string embedUrl)
    {
        embedUrl = string.Empty;

        if (url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase))
        {
            var videoId = ExtractQueryParam(url, "v");
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                embedUrl = $"https://www.youtube.com/embed/{videoId}";
                return true;
            }
        }
        else if (url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
        {
            var idx = url.IndexOf("youtu.be/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var rest = url[(idx + "youtu.be/".Length)..];
                var qIdx = rest.IndexOf('?');
                var videoId = qIdx >= 0 ? rest[..qIdx] : rest;
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    embedUrl = $"https://www.youtube.com/embed/{videoId}";
                    return true;
                }
            }
        }
        else if (url.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase))
        {
            embedUrl = url;
            return true;
        }

        return false;
    }

    internal static string? ExtractQueryParam(string url, string paramName)
    {
        var qIdx = url.IndexOf('?');
        if (qIdx < 0)
        {
            return null;
        }

        var query = url[(qIdx + 1)..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIdx = pair.IndexOf('=');
            if (eqIdx < 0)
            {
                continue;
            }

            var key = pair[..eqIdx];
            if (string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
            {
                return WebUtility.UrlDecode(pair[(eqIdx + 1)..]);
            }
        }

        return null;
    }
}
