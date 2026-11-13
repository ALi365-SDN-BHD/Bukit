using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Shared;
using Bukit.Content.Notion.Snapshot;
namespace Bukit.Content.Notion;

internal static class NotionCacheManager
{
    internal const int CacheFormat = 2;

    // Notion truncates last_edited_time to the minute; a read is complete once the
    // minute has ended, plus a margin for clock skew between Notion servers.
    private static readonly TimeSpan s_settleDelay = TimeSpan.FromSeconds(65);

    internal static PageHtmlCache? CreatePageHtmlCache(NotionContentSourceOptions options)
    {
        // A Notion offline snapshot owns the body cache: recorded (readwrite) or read offline
        // (readonly), whatever cacheMode/cacheDir the site configures.
        var snapshot = options.Snapshot;
        var mode = snapshot is null ? NormalizeCacheMode(options.CacheMode) : snapshot.Offline ? "readonly" : "readwrite";
        if (mode == "off")
        {
            return null;
        }

        var cacheDir = snapshot?.CacheDirectory ?? options.CacheDir;
        if (string.IsNullOrWhiteSpace(cacheDir))
        {
            return null;
        }

        var root = cacheDir.Trim();
        var pagesDir = Path.Combine(root, "pages");
        if (mode == "readwrite")
        {
            try
            {
                Directory.CreateDirectory(pagesDir);
            }
            catch (Exception exception) when (snapshot is not null && exception is IOException or UnauthorizedAccessException)
            {
                // .NET I/O messages carry the path; the snapshot location is never printed.
                throw new NotionSnapshotException("Notion snapshot entry could not be written.", exception);
            }
        }

        return new PageHtmlCache(mode, root, pagesDir)
        {
            OfflineSnapshot = snapshot is { Offline: true },
            PageUsed = snapshot is { Offline: false } ? snapshot.MarkBodyUsed : null
        };
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

    internal static Task<string> GetOrRenderPageHtmlAsync(
        NotionBlocksRenderer renderer,
        PageHtmlCache? cache,
        string pageId,
        string? lastEditedTime,
        CancellationToken cancellationToken,
        ILogger? logger = null)
        => GetOrRenderPageHtmlAsync(() => renderer, cache, pageId, lastEditedTime, cancellationToken, logger);

    /// <summary>
    /// Returns the cached body or renders the page; <paramref name="rendererFactory"/> runs only
    /// when the page must be rendered, so readonly and offline reads never need a transport.
    /// </summary>
    internal static async Task<string> GetOrRenderPageHtmlAsync(
        Func<NotionBlocksRenderer> rendererFactory,
        PageHtmlCache? cache,
        string pageId,
        string? lastEditedTime,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        if (cache is null)
        {
            return await rendererFactory().RenderPageAsync(pageId, cancellationToken);
        }

        var html = await GetOrRenderCachedPageHtmlAsync(rendererFactory, cache, pageId, lastEditedTime, logger, cancellationToken);
        cache.PageUsed?.Invoke(pageId);
        return html;
    }

    private static async Task<string> GetOrRenderCachedPageHtmlAsync(
        Func<NotionBlocksRenderer> rendererFactory,
        PageHtmlCache cache,
        string pageId,
        string? lastEditedTime,
        ILogger? logger,
        CancellationToken cancellationToken)
    {

        var cachePath = Path.Combine(cache.PagesDir, $"{pageId}.json");
        if (File.Exists(cachePath))
        {
            try
            {
                var json = await File.ReadAllBytesAsync(cachePath, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (TryReuseCachedHtml(cache, doc.RootElement, lastEditedTime) is { } cachedHtml)
                {
                    return cachedHtml;
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

        if (cache.OfflineSnapshot)
        {
            throw NotionSnapshotException.Missing($"rendered body for page '{pageId}' (edited after the formal build, or never rendered by it)");
        }

        if (cache.Mode == "readonly")
        {
            throw new ContentException($"Notion cache miss in readonly mode for page: {pageId}");
        }

        var renderer = rendererFactory();
        var html = await renderer.RenderPageAsync(pageId, cancellationToken);
        if (cache.Mode == "readwrite")
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", CacheFormat);
                if (lastEditedTime is null)
                {
                    writer.WriteNull("lastEditedTime");
                }
                else
                {
                    writer.WriteString("lastEditedTime", lastEditedTime);
                }
                writer.WriteNumber("renderer", NotionBlocksRenderer.ContractVersion);
                if (renderer.LastPageFetchedAt is { } pageFetchedAt)
                {
                    writer.WriteString("fetchedAt", pageFetchedAt);
                }

                writer.WriteString("html", html);
                WriteLinksExpiry(writer, renderer, html);
                writer.WriteEndObject();
            }

            try
            {
                await AtomicNotionCacheWriter.WriteJsonAsync(cachePath, buffer.WrittenMemory.ToArray(), cancellationToken);
            }
            catch (Exception exception) when (cache.PageUsed is not null && exception is IOException or UnauthorizedAccessException)
            {
                // Recording a snapshot: fail without printing the snapshot location.
                throw new NotionSnapshotException("Notion snapshot entry could not be written.", exception);
            }
        }

        return html;
    }

    /// <summary>
    /// Returns the cached HTML when it can be reused. Readonly mode reuses any readable
    /// format, expired links included (offline freeze). Readwrite mode needs the current
    /// format and renderer contract, unexpired links, and a fetch after the edit minute settled.
    /// </summary>
    private static string? TryReuseCachedHtml(PageHtmlCache cache, JsonElement root, string? lastEditedTime)
    {
        var cachedLastEdited = root.TryGetProperty("lastEditedTime", out var let) && let.ValueKind == JsonValueKind.String ? let.GetString() : null;
        var cachedHtml = root.TryGetProperty("html", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
        if (cachedHtml is null || !string.Equals(cachedLastEdited, lastEditedTime, StringComparison.Ordinal))
        {
            return null;
        }

        var version = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : 0;
        int? cachedRenderer = root.TryGetProperty("renderer", out var r) && r.TryGetInt32(out var rv) ? rv : null;
        if (cache.Mode == "readonly")
        {
            if (version is not (1 or CacheFormat))
            {
                return null;
            }

            if (cachedRenderer != NotionBlocksRenderer.ContractVersion)
            {
                cache.CountStaleContract();
            }

            return cachedHtml;
        }

        var linksExpireAt = ReadCachedLinksExpiry(root, cachedHtml);
        var linksExpired = linksExpireAt is { } expireAt && expireAt <= DateTimeOffset.UtcNow;
        return version == CacheFormat &&
            cachedRenderer == NotionBlocksRenderer.ContractVersion &&
            !string.IsNullOrWhiteSpace(lastEditedTime) &&
            !linksExpired &&
            ReadTimestamp(root, "fetchedAt") is { } fetched &&
            IsSettled(lastEditedTime, fetched)
            ? cachedHtml
            : null;
    }

    /// <summary>
    /// True when a read at Notion server time <paramref name="readAt"/> saw every edit of
    /// the minute in <paramref name="lastEditedTime"/>. False when the time is empty or
    /// unparseable.
    /// </summary>
    internal static bool IsSettled(string? lastEditedTime, DateTimeOffset readAt)
    {
        if (string.IsNullOrWhiteSpace(lastEditedTime) ||
            !DateTimeOffset.TryParse(lastEditedTime, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var edited))
        {
            return false;
        }

        var minute = new DateTimeOffset(edited.UtcTicks - edited.UtcTicks % TimeSpan.TicksPerMinute, TimeSpan.Zero);
        return readAt >= minute + s_settleDelay;
    }

    private static DateTimeOffset? ReadCachedLinksExpiry(JsonElement root, string? cachedHtml)
        => ReadTimestamp(root, "linksExpireAt") ?? NotionHtmlLinkExpiry.GetEarliestExpiry(cachedHtml);

    private static DateTimeOffset? ReadTimestamp(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) &&
            parsed != default)
        {
            return parsed;
        }

        return null;
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

    internal sealed record PageHtmlCache(string Mode, string RootDir, string PagesDir)
    {
        private int _staleContractCount;

        /// <summary>Readonly reads from a Notion offline snapshot: a miss names the snapshot.</summary>
        internal bool OfflineSnapshot { get; init; }

        /// <summary>Called with each page id whose body this build used (snapshot recording).</summary>
        internal Action<string>? PageUsed { get; init; }

        internal int StaleContractCount => Volatile.Read(ref _staleContractCount);

        internal void CountStaleContract() => Interlocked.Increment(ref _staleContractCount);

        /// <summary>Emits one summary warning for readonly entries from another renderer contract.</summary>
        internal void ReportStaleContracts(ILogger? logger)
        {
            var count = Interlocked.Exchange(ref _staleContractCount, 0);
            if (count > 0)
            {
                logger?.Warn(
                    $"event=notion.cache.renderer_mismatch mode=readonly entries={count} renderer={NotionBlocksRenderer.ContractVersion} " +
                    "message=Readonly cache reused bodies rendered by another Notion renderer version; rebuild the cache in readwrite mode to refresh them.");
            }
        }
    }
}
