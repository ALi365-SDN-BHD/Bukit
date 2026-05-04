using System.Text.Json;

namespace Bukit.Content.Notion;

internal sealed class NotionRelationTargetCache
{
    private readonly string _mode;
    private readonly string _relationsDir;

    private NotionRelationTargetCache(string mode, string relationsDir)
    {
        _mode = mode;
        _relationsDir = relationsDir;
    }

    internal static NotionRelationTargetCache? Create(string? mode, string? rootDir)
    {
        var normalizedMode = (mode ?? "off").Trim().ToLowerInvariant();
        if (normalizedMode == "off" || string.IsNullOrWhiteSpace(rootDir))
        {
            return null;
        }

        var relationsDir = Path.Combine(rootDir.Trim(), "relations");
        Directory.CreateDirectory(relationsDir);
        return new NotionRelationTargetCache(normalizedMode, relationsDir);
    }

    internal async Task<RelationTargetInfo?> TryReadAsync(string pageId, CancellationToken cancellationToken)
    {
        var path = GetCachePath(pageId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var cachedPageId = GetString(root, "pageId");
            var title = GetString(root, "title");
            var slug = GetString(root, "slug");
            var type = GetString(root, "type");
            var url = GetNullableString(root, "url");

            if (string.IsNullOrWhiteSpace(cachedPageId) ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(slug) ||
                string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            return new RelationTargetInfo(cachedPageId, title, slug, type, url);
        }
        catch
        {
            return null;
        }
    }

    internal async Task WriteAsync(RelationTargetInfo target, CancellationToken cancellationToken)
    {
        if (_mode != "readwrite")
        {
            return;
        }

        var path = GetCachePath(target.PageId);
        await using var stream = File.Create(path);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteNumber("version", 1);
        writer.WriteString("pageId", target.PageId);
        writer.WriteString("title", target.Title);
        writer.WriteString("slug", target.Slug);
        writer.WriteString("type", target.Type);
        if (target.Url is null)
        {
            writer.WriteNull("url");
        }
        else
        {
            writer.WriteString("url", target.Url);
        }
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    private string GetCachePath(string pageId)
    {
        return Path.Combine(_relationsDir, $"{pageId}.json");
    }

    private static string GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? GetNullableString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
