using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Shared;
namespace Bukit.Content.Notion;

internal static class NotionCacheManager
{
    internal static PageHtmlCache? CreatePageHtmlCache(NotionContentSourceOptions options)
    {
        var mode = NormalizeCacheMode(options.CacheMode);
        if (mode == "off")
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.CacheDir))
        {
            return null;
        }

        var root = options.CacheDir!.Trim();
        var pagesDir = Path.Combine(root, "pages");
        if (mode == "readwrite") Directory.CreateDirectory(pagesDir);
        return new PageHtmlCache(mode, root, pagesDir);
    }

    internal static string NormalizeCacheMode(string? mode)
    {
        return (mode ?? "off").Trim().ToLowerInvariant() switch
        {
            "readonly" => "readonly",
            "readwrite" => "readwrite",
            _ => "off"
        };
    }

    internal static async Task<string> GetOrRenderPageHtmlAsync(
        NotionBlocksRenderer renderer,
        PageHtmlCache? cache,
        string pageId,
        string? lastEditedTime,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        if (cache is null)
        {
            return await renderer.RenderPageAsync(pageId, cancellationToken);
        }

        var cachePath = Path.Combine(cache.PagesDir, $"{pageId}.json");
        if (File.Exists(cachePath))
        {
            try
            {
                var json = await File.ReadAllBytesAsync(cachePath, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var version = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : 0;
                var cachedLastEdited = root.TryGetProperty("lastEditedTime", out var let) && let.ValueKind == JsonValueKind.String ? let.GetString() : null;
                var cachedHtml = root.TryGetProperty("html", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
                var linksExpireAt = ReadCachedLinksExpiry(root, cachedHtml);
                var linksExpired = linksExpireAt is { } expireAt && expireAt <= DateTimeOffset.UtcNow;

                if (version == 1 &&
                    string.Equals(cachedLastEdited, lastEditedTime, StringComparison.Ordinal) &&
                    cachedHtml is not null &&
                    (!linksExpired || cache.Mode == "readonly"))
                {
                    return cachedHtml!;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.Warn($"event=notion.cache.read_failed pageId={pageId} message={ex.Message}");
            }
        }

        if (cache.Mode == "readonly")
        {
            throw new ContentException($"Notion cache miss in readonly mode for page: {pageId}");
        }

        var html = await renderer.RenderPageAsync(pageId, cancellationToken);
        if (cache.Mode == "readwrite")
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", 1);
                if (lastEditedTime is null)
                {
                    writer.WriteNull("lastEditedTime");
                }
                else
                {
                    writer.WriteString("lastEditedTime", lastEditedTime);
                }
                writer.WriteString("html", html);
                WriteLinksExpiry(writer, renderer, html);
                writer.WriteEndObject();
            }

            await AtomicNotionCacheWriter.WriteJsonAsync(cachePath, buffer.WrittenMemory.ToArray(), cancellationToken);
        }

        return html;
    }

    private static DateTimeOffset? ReadCachedLinksExpiry(JsonElement root, string? cachedHtml)
    {
        DateTimeOffset? linksExpireAt = null;
        if (root.TryGetProperty("linksExpireAt", out var expiry) && expiry.ValueKind == JsonValueKind.String)
        {
            DateTimeOffset.TryParse(expiry.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedExpiry);
            if (parsedExpiry != default)
            {
                linksExpireAt = parsedExpiry;
            }
        }

        return linksExpireAt ?? NotionHtmlLinkExpiry.GetEarliestExpiry(cachedHtml);
    }

    private static void WriteLinksExpiry(Utf8JsonWriter writer, NotionBlocksRenderer renderer, string html)
    {
        var apiExpiry = renderer.LastFileLinkExpiresAt;
        var urlExpiry = NotionHtmlLinkExpiry.GetEarliestExpiry(html);
        var linksExpireAt = apiExpiry is { } api && urlExpiry is { } url
            ? (api <= url ? api : url)
            : apiExpiry ?? urlExpiry;
        if (linksExpireAt is { } expiry)
        {
            writer.WriteString("linksExpireAt", expiry);
        }
    }

    internal sealed record PageHtmlCache(string Mode, string RootDir, string PagesDir);
}
