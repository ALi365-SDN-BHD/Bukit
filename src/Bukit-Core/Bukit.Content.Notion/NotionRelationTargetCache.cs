using Bukit.Engine.Abstractions.Content;
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

    internal static NotionRelationTargetCache? Create(string? mode, string? rootDir, string? scope = null)
    {
        var normalizedMode = (mode ?? "off").Trim().ToLowerInvariant();
        if (normalizedMode == "off" || string.IsNullOrWhiteSpace(rootDir))
        {
            return null;
        }

        var relationsDir = Path.Combine(rootDir.Trim(), "relations");
        if (!string.IsNullOrWhiteSpace(scope))
        {
            relationsDir = Path.Combine(relationsDir, ToSafePathSegment(scope));
        }
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
            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 2)
            {
                return null;
            }
            var title = GetString(root, "title");
            var slug = GetString(root, "slug");
            var type = GetString(root, "type");
            var url = GetNullableString(root, "url");
            var image = GetNullableString(root, "image");
            var sameAs = GetStringList(root, "sameAs");

            if (string.IsNullOrWhiteSpace(cachedPageId) ||
                string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(slug) ||
                string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            return new RelationTargetInfo(cachedPageId, title, slug, type, url, image, sameAs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 2);
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
            if (target.Image is null)
            {
                writer.WriteNull("image");
            }
            else
            {
                writer.WriteString("image", target.Image);
            }
            writer.WritePropertyName("sameAs");
            writer.WriteStartArray();
            foreach (var sameAs in target.SameAs ?? Array.Empty<string>())
            {
                writer.WriteStringValue(sameAs);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }

        await AtomicNotionCacheWriter.WriteJsonAsync(path, buffer.WrittenMemory.ToArray(), cancellationToken);
    }

    private string GetCachePath(string pageId)
    {
        return Path.Combine(_relationsDir, $"{pageId}.json");
    }

    private static string ToSafePathSegment(string scope)
        => string.Concat(scope.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

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

    private static IReadOnlyList<string> GetStringList(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }
}
