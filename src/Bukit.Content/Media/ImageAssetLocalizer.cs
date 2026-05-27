using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Content.Media;

public sealed class ImageAssetLocalizer : IImageAssetLocalizer, IDisposable
{
    private const string IndexFileName = ".media-index.json";
    private const string UserAgentValue = "Bukit/1.0";
    private const long DefaultMaxFileSize = 50L * 1024 * 1024; // 50 MB
    private const int IndexPersistThreshold = 20;

    private static readonly IReadOnlyDictionary<string, string> ContentTypeToExt =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp",
            ["image/svg+xml"] = ".svg",
            ["image/avif"] = ".avif",
            ["image/bmp"] = ".bmp"
        };

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".avif", ".bmp",
            ".ico", ".tiff", ".tif", ".img"
        };

    private readonly MediaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly bool _ownsHttpClient;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<string>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<MediaFailure> _failures = new();
    private readonly object _indexLock = new();
    private Dictionary<string, string> _diskIndex = new(StringComparer.Ordinal);
    private volatile bool _indexLoaded;
    private bool _indexDirty;
    private int _pendingIndexChanges;

    /// <summary>Returns all image URLs that failed to localize during this session.</summary>
    public IReadOnlyList<MediaFailure> Failures => _failures.ToArray();

    public ImageAssetLocalizer(MediaConfig config, ILogger? logger = null)
    {
        _config = config;
        _logger = logger;

        var handler = new SocketsHttpHandler();
        if (config.BlockPrivateNetworks)
        {
            handler.ConnectCallback = SsrfSafeConnectAsync;
        }

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentValue);
        _ownsHttpClient = true;
    }

    internal ImageAssetLocalizer(MediaConfig config, HttpClient httpClient, ILogger? logger = null)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
        _ownsHttpClient = false;
    }

    public async Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return _config.DefaultImageUrl;
        }

        var source = sourceUrl.Trim();

        // Defensive: decode HTML entities that may leak from HTML-sourced URLs.
        // &amp; is never valid in a real URL; its presence always indicates HTML encoding.
        if (source.Contains("&amp;", StringComparison.Ordinal))
        {
            source = System.Net.WebUtility.HtmlDecode(source);
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger?.Warn($"event=media.skip_non_http source={UrlRedactor.Redact(source)}");
            return source;
        }

        var normalizedKey = NormalizeSourceUrlForKey(uri);

        if (!_config.DownloadToLocal)
        {
            _logger?.Warn($"event=media.skip_download_disabled source={UrlRedactor.Redact(source)}");
            return source;
        }

        if (string.IsNullOrWhiteSpace(_config.DownloadDir) || string.IsNullOrWhiteSpace(_config.UrlBase))
        {
            _logger?.Warn($"event=media.skip_missing_config source={UrlRedactor.Redact(source)} downloadDir={_config.DownloadDir} urlBase={_config.UrlBase}");
            return _config.DefaultImageUrl;
        }

        if (_cache.TryGetValue(normalizedKey, out var cached))
        {
            return cached;
        }

        var root = _config.DownloadDir.Trim();
        Directory.CreateDirectory(root);
        EnsureIndexLoaded(root);

        if (TryGetUrlFromIndex(root, normalizedKey, out var indexedUrl))
        {
            _cache.TryAdd(normalizedKey, indexedUrl);
            return indexedUrl;
        }

        var hashPrefix = BuildHashPrefix(normalizedKey);
        var existingName = FindExistingFileByHash(root, hashPrefix);
        if (existingName is not null)
        {
            RememberIndex(normalizedKey, existingName);
            var existingUrl = CombineUrl(_config.UrlBase, existingName);
            _cache.TryAdd(normalizedKey, existingUrl);
            return existingUrl;
        }

        // Single-flight: deduplicate concurrent downloads for the same URL
        var downloadTask = _inflight.GetOrAdd(normalizedKey,
            _ => DownloadCoreAsync(normalizedKey, uri, source, root, cancellationToken));
        try
        {
            return await downloadTask;
        }
        finally
        {
            _inflight.TryRemove(KeyValuePair.Create(normalizedKey, downloadTask));
        }
    }

    // ── Download core ───────────────────────────────────────────────────

    private async Task<string> DownloadCoreAsync(
        string normalizedKey, Uri uri, string source, string root,
        CancellationToken cancellationToken)
    {
        // Pre-flight SSRF check for injected HttpClient (owned client uses ConnectCallback)
        if (_config.BlockPrivateNetworks && !_ownsHttpClient &&
            await IsPrivateHostAsync(uri.Host, cancellationToken))
        {
            _logger?.Warn($"event=media.ssrf_blocked source={UrlRedactor.Redact(source)}");
            return RecordFailure(source, "SSRF blocked (private/reserved address)");
        }

        var maxRetries = _config.MaxRetries is >= 0 ? _config.MaxRetries.Value : 0;
        var maxFileSize = _config.MaxFileSizeBytes is > 0
            ? _config.MaxFileSizeBytes.Value
            : DefaultMaxFileSize;
        var retryBaseDelay = _config.RetryBaseDelayMs is > 0 ? _config.RetryBaseDelayMs.Value : 0;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (_config.TimeoutMs is > 0)
                {
                    cts.CancelAfter(_config.TimeoutMs.Value);
                }

                using var response = await _httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    if (attempt >= maxRetries)
                    {
                        _logger?.Warn($"event=media.download_failed status={(int)response.StatusCode} source={UrlRedactor.Redact(source)}");
                        return RecordFailure(source, $"HTTP {(int)response.StatusCode}");
                    }

                    await DelayBeforeRetryAsync(attempt, retryBaseDelay, cancellationToken);
                    continue;
                }

                // Content-Type validation: reject non-image responses
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (!IsAllowedContentType(contentType))
                {
                    _logger?.Warn(
                        $"event=media.content_type_rejected type={contentType ?? "(null)"} source={UrlRedactor.Redact(source)}");
                    return RecordFailure(source, $"Content-Type rejected: {contentType ?? "(null)"}");
                }

                // Size-limited streaming read
                var bytes = await ReadWithLimitAsync(response.Content, maxFileSize, cts.Token);
                if (bytes is null)
                {
                    _logger?.Warn($"event=media.download_too_large limit={maxFileSize} source={UrlRedactor.Redact(source)}");
                    return RecordFailure(source, $"File too large (limit: {maxFileSize} bytes)");
                }

                if (bytes.Length == 0)
                {
                    if (attempt >= maxRetries)
                    {
                        _logger?.Warn($"event=media.download_empty source={UrlRedactor.Redact(source)}");
                        return RecordFailure(source, "Empty response body");
                    }

                    await DelayBeforeRetryAsync(attempt, retryBaseDelay, cancellationToken);
                    continue;
                }

                var ext = ResolveExtension(uri, contentType);
                var fileName = BuildStableFileName(normalizedKey, ext);
                var localPath = Path.Combine(root, fileName);
                if (!File.Exists(localPath))
                {
                    await File.WriteAllBytesAsync(localPath, bytes, cts.Token);
                }

                var publicUrl = CombineUrl(_config.UrlBase, fileName);
                RememberIndex(normalizedKey, fileName);
                _cache.TryAdd(normalizedKey, publicUrl);
                return publicUrl;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // propagate user-initiated cancellation
            }
            catch (Exception ex)
            {
                if (attempt >= maxRetries)
                {
                    _logger?.Warn(
                        $"event=media.download_error source={source} error={ex.GetType().Name}");
                    return RecordFailure(source, $"{ex.GetType().Name}: {ex.Message}");
                }

                await DelayBeforeRetryAsync(attempt, retryBaseDelay, cancellationToken);
            }
        }
    }

    private string RecordFailure(string sourceUrl, string reason)
    {
        _failures.Add(new MediaFailure(sourceUrl, reason));
        return _config.DefaultImageUrl;
    }

    // ── Streaming read with size limit ──────────────────────────────────

    private static async Task<byte[]?> ReadWithLimitAsync(
        HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        // Check Content-Length header first for early rejection
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength.Value > maxBytes)
        {
            return null;
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        long totalRead = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalRead += read;
            if (totalRead > maxBytes)
            {
                return null;
            }

            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    // ── Content-Type validation ─────────────────────────────────────────

    private static bool IsAllowedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            // Allow unknown content types to avoid breaking edge cases;
            // the extension whitelist provides a secondary guard.
            return true;
        }

        var ct = contentType.Trim();
        return ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(ct, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    // ── SSRF protection ─────────────────────────────────────────────────

    private static async ValueTask<Stream> SsrfSafeConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        var safeAddress = Array.Find(addresses, static a => !IsPrivateAddress(a))
                          ?? throw new HttpRequestException(
                              $"SSRF blocked: all resolved addresses for '{host}' are private/reserved.");

        var socket = new Socket(safeAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(safeAddress, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task<bool> IsPrivateHostAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            if (IPAddress.TryParse(host, out var directIp))
            {
                return IsPrivateAddress(directIp);
            }

            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses.Length > 0 && Array.Exists(addresses, IsPrivateAddress);
        }
        catch
        {
            // DNS resolution failed; let the HTTP request proceed and fail naturally
            return false;
        }
    }

    internal static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] switch
            {
                0 => true,                                 // 0.0.0.0/8
                10 => true,                                // 10.0.0.0/8
                127 => true,                               // 127.0.0.0/8
                169 => b[1] == 254,                        // 169.254.0.0/16 link-local / cloud metadata
                172 => b[1] >= 16 && b[1] <= 31,           // 172.16.0.0/12
                192 => b[1] == 168,                        // 192.168.0.0/16
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        }

        return false;
    }

    // ── Retry backoff ───────────────────────────────────────────────────

    private static async Task DelayBeforeRetryAsync(
        int attempt, int baseDelayMs, CancellationToken cancellationToken)
    {
        if (baseDelayMs <= 0)
        {
            return;
        }

        var delayMs = Math.Min(baseDelayMs * (1 << Math.Min(attempt, 10)), 30_000);
        await Task.Delay(delayMs, cancellationToken);
    }

    // ── Extension resolution with whitelist ─────────────────────────────

    private static string ResolveExtension(Uri uri, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            ContentTypeToExt.TryGetValue(contentType.Trim(), out var mapped))
        {
            return mapped;
        }

        var ext = Path.GetExtension(uri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 6 && AllowedExtensions.Contains(ext))
        {
            return ext.ToLowerInvariant();
        }

        return ".img";
    }

    // ── Stable file naming ──────────────────────────────────────────────

    private static string BuildStableFileName(string normalizedKey, string ext)
    {
        return $"{BuildHashPrefix(normalizedKey)}{ext}";
    }

    private static string BuildHashPrefix(string normalizedKey)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedKey));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private static string? FindExistingFileByHash(string directory, string hashPrefix)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, $"{hashPrefix}.*"))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith('.') && AllowedExtensions.Contains(Path.GetExtension(name)))
                {
                    return name;
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Directory may not exist yet
        }

        return null;
    }

    private static string NormalizeSourceUrlForKey(Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        path = path.Replace('\\', '/');
        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        return $"{scheme}://{host}{port}{path}";
    }

    // ── Index management ────────────────────────────────────────────────

    private static bool IsSafeFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
               && fileName.IndexOfAny(['/', '\\']) < 0
               && !fileName.Contains("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(fileName);
    }

    private bool TryGetUrlFromIndex(string root, string normalizedKey, out string url)
    {
        url = string.Empty;
        lock (_indexLock)
        {
            if (!_diskIndex.TryGetValue(normalizedKey, out var fileName))
            {
                return false;
            }

            if (!IsSafeFileName(fileName))
            {
                _logger?.Warn(
                    $"event=media.index_path_traversal key={normalizedKey} fileName={fileName}");
                _diskIndex.Remove(normalizedKey);
                _indexDirty = true;
                return false;
            }

            var fullPath = Path.Combine(root, fileName);
            if (!File.Exists(fullPath))
            {
                _diskIndex.Remove(normalizedKey);
                _indexDirty = true;
                return false;
            }

            url = CombineUrl(_config.UrlBase, fileName);
            return true;
        }
    }

    private void RememberIndex(string normalizedKey, string fileName)
    {
        bool shouldPersist;
        lock (_indexLock)
        {
            if (_diskIndex.TryGetValue(normalizedKey, out var existing) &&
                string.Equals(existing, fileName, StringComparison.Ordinal))
            {
                return;
            }

            _diskIndex[normalizedKey] = fileName;
            _indexDirty = true;
            _pendingIndexChanges++;
            shouldPersist = _pendingIndexChanges >= IndexPersistThreshold;
        }

        if (shouldPersist)
        {
            PersistIndex();
        }
    }

    private void EnsureIndexLoaded(string root)
    {
        if (_indexLoaded)
        {
            return;
        }

        lock (_indexLock)
        {
            if (_indexLoaded)
            {
                return;
            }

            var path = Path.Combine(root, IndexFileName);
            if (!File.Exists(path))
            {
                _indexLoaded = true;
                return;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                var rootEl = doc.RootElement;
                JsonElement entries;

                if (rootEl.ValueKind == JsonValueKind.Object &&
                    rootEl.TryGetProperty("entries", out var e) &&
                    e.ValueKind == JsonValueKind.Object)
                {
                    entries = e;
                }
                else if (rootEl.ValueKind == JsonValueKind.Object)
                {
                    entries = rootEl;
                }
                else
                {
                    _indexLoaded = true;
                    return;
                }

                foreach (var prop in entries.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var v = prop.Value.GetString();
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        continue;
                    }

                    var trimmed = v.Trim();
                    if (!IsSafeFileName(trimmed))
                    {
                        _logger?.Warn($"event=media.index_unsafe_entry key={prop.Name} value={trimmed}");
                        continue;
                    }

                    _diskIndex[prop.Name] = trimmed;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"event=media.index_corrupt path={path} error={ex.GetType().Name}");
                _diskIndex = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            finally
            {
                _indexLoaded = true;
            }
        }
    }

    private void PersistIndex()
    {
        var root = (_config.DownloadDir ?? string.Empty).Trim();
        if (root.Length == 0)
        {
            return;
        }

        lock (_indexLock)
        {
            if (!_indexLoaded || !_indexDirty)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, IndexFileName);
                using var fs = File.Create(path);
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false });
                writer.WriteStartObject();
                writer.WriteNumber("version", 1);
                writer.WritePropertyName("entries");
                writer.WriteStartObject();
                foreach (var kv in _diskIndex.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(kv.Key, kv.Value);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.Flush();
                _indexDirty = false;
                _pendingIndexChanges = 0;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"event=media.index_write_failed error={ex.GetType().Name}");
            }
        }
    }

    private static string CombineUrl(string baseUrl, string fileName)
    {
        var trimmedBase = (baseUrl ?? string.Empty).Trim();
        if (trimmedBase.Length == 0)
        {
            return "/" + fileName;
        }

        if (!trimmedBase.StartsWith('/'))
        {
            trimmedBase = "/" + trimmedBase;
        }

        return $"{trimmedBase.TrimEnd('/')}/{fileName}";
    }

    public void Dispose()
    {
        PersistIndex();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
