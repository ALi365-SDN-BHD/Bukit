using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared.Notion;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class DefaultNotionPageFetcher : INotionPageFetcher
{
    public async Task<NotionFetchedPage?> FetchAsync(NotionApiClient client, string pageId, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await client.GetAsync(NotionApiUrls.Pages(pageId), cancellationToken);
            var page = doc.RootElement;

            var notionUrl = page.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
            notionUrl = string.IsNullOrWhiteSpace(notionUrl) ? string.Empty : notionUrl.Trim();

            var props = page.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
            var title = ExtractTitle(props);
            title = string.IsNullOrWhiteSpace(title) ? pageId : title.Trim();

            var slug = Slugify(title);
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = pageId.Replace("-", string.Empty, StringComparison.Ordinal);
            }

            var fields = NotionPropertyParser.ExtractAllFields(props);
            fields = InjectPageCoverAndIcon(fields, page);
            return new NotionFetchedPage(pageId, title, slug, notionUrl, fields);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[warn] pages-index: failed to fetch Notion page '{pageId}': {ex.Message}");
            return null;
        }
    }

    private static IReadOnlyDictionary<string, ContentField> InjectPageCoverAndIcon(
        IReadOnlyDictionary<string, ContentField> fields, JsonElement page)
    {
        var coverUrl = ExtractPageFileUrl(page, "cover");
        var iconUrl = ExtractPageFileUrl(page, "icon");

        if (string.IsNullOrWhiteSpace(coverUrl) && string.IsNullOrWhiteSpace(iconUrl))
        {
            return fields;
        }

        var mutable = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(coverUrl) && !mutable.ContainsKey("cover"))
        {
            mutable["cover"] = new ContentField("file", coverUrl);
        }

        if (!string.IsNullOrWhiteSpace(iconUrl) && !mutable.ContainsKey("icon"))
        {
            mutable["icon"] = new ContentField("file", iconUrl);
        }

        return mutable;
    }

    private static string? ExtractPageFileUrl(JsonElement page, string propertyName)
    {
        if (!page.TryGetProperty(propertyName, out var container) || container.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fileType = container.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;

        if (fileType == "external" &&
            container.TryGetProperty("external", out var ext) &&
            ext.ValueKind == JsonValueKind.Object &&
            ext.TryGetProperty("url", out var eu) &&
            eu.ValueKind == JsonValueKind.String)
        {
            return eu.GetString();
        }

        if (fileType == "file" &&
            container.TryGetProperty("file", out var file) &&
            file.ValueKind == JsonValueKind.Object &&
            file.TryGetProperty("url", out var fu) &&
            fu.ValueKind == JsonValueKind.String)
        {
            return fu.GetString();
        }

        return null;
    }

    private static string ExtractTitle(JsonElement props)
    {
        if (props.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var prop in props.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!prop.Value.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (!string.Equals(typeEl.GetString(), "title", StringComparison.Ordinal))
            {
                continue;
            }

            if (!prop.Value.TryGetProperty("title", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return ExtractPlainTextArray(arr);
        }

        return string.Empty;
    }

    private static string ExtractPlainTextArray(JsonElement arr)
    {
        if (arr.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.TryGetProperty("plain_text", out var t) && t.ValueKind == JsonValueKind.String)
            {
                var s = t.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(s.Trim());
                }
            }
        }

        return sb.ToString();
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var lastDash = false;
        foreach (var ch in text.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
            {
                if (!lastDash && sb.Length > 0)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }
        }

        return sb.ToString().Trim('-');
    }
}
