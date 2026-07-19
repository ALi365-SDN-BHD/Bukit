using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Content.Media;

public sealed class ImageAssetLocalizer : IImageAssetLocalizer, IDisposable
{
    private const string UserAgentValue = "Bukit/1.0";
    private const long DefaultMaxFileSize = 50L * 1024 * 1024;

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

    internal static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".avif", ".bmp",
            ".ico", ".tiff", ".tif", ".img"
        };

    private readonly MediaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly bool _ownsHttpClient;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<MediaFailure> _failures = new();
    private readonly MediaIndexManager _indexManager;
    private int _disposeState;

    public IReadOnlyList<MediaFailure> Failures => _failures.ToArray();

    public ImageAssetLocalizer(MediaConfig config, ILogger? logger = null)
    {
        _config = config;
        _logger = logger;
        _lifetimeToken = _lifetimeCancellation.Token;
        _indexManager = new MediaIndexManager(
            config.DownloadDir ?? string.Empty, config.UrlBase ?? string.Empty, logger);

        var handler = new SocketsHttpHandler();
        if (config.BlockPrivateNetworks)
        {
            handler.ConnectCallback = SsrfGuard.SsrfSafeConnectAsync;
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
        _lifetimeToken = _lifetimeCancellation.Token;
        _indexManager = new MediaIndexManager(
            config.DownloadDir ?? string.Empty, config.UrlBase ?? string.Empty, logger);
        _ownsHttpClient = false;
    }

    public async Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return _config.DefaultImageUrl;
        }

        var source = sourceUrl.Trim();

        if (source.Contains("&amp;", StringComparison.Ordinal))
        {
            source = System.Net.WebUtility.HtmlDecode(source);
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            if (IsLocalAssetReference(source))
            {
                _logger?.Debug($"event=media.skip_local source={UrlRedactor.Redact(source)}");
                return source;
            }

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
        _indexManager.EnsureIndexLoaded(root);

        if (_indexManager.TryGetUrlFromIndex(root, normalizedKey, out var indexedUrl))
        {
            _cache.TryAdd(normalizedKey, indexedUrl);
            return indexedUrl;
        }

        var hashPrefix = BuildHashPrefix(normalizedKey);
        var existingName = _indexManager.FindExistingFileByHash(root, hashPrefix);
        if (existingName is not null)
        {
            _indexManager.RememberIndex(normalizedKey, existingName);
            var existingUrl = _indexManager.CombineUrl(existingName);
            _cache.TryAdd(normalizedKey, existingUrl);
            return existingUrl;
        }

        var newDownload = new Lazy<Task<string>>(
            () => DownloadCoreAsync(normalizedKey, uri, source, root, _lifetimeToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var download = _inflight.GetOrAdd(normalizedKey, newDownload);
        var downloadTask = download.Value;
        if (ReferenceEquals(download, newDownload))
        {
            _ = RemoveInflightWhenCompletedAsync(normalizedKey, download, downloadTask);
        }

        try
        {
            return await downloadTask.WaitAsync(cancellationToken);
        }
        finally
        {
            if (downloadTask.IsCompleted)
            {
                _inflight.TryRemove(KeyValuePair.Create(normalizedKey, download));
            }
        }
    }

    private async Task RemoveInflightWhenCompletedAsync(
        string normalizedKey,
        Lazy<Task<string>> download,
        Task<string> downloadTask)
    {
        try
        {
            await downloadTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The original task retains its terminal state for all callers.
        }
        finally
        {
            _inflight.TryRemove(KeyValuePair.Create(normalizedKey, download));
        }
    }

    private async Task<string> DownloadCoreAsync(
        string normalizedKey, Uri uri, string source, string root,
        CancellationToken cancellationToken)
    {
        if (_config.BlockPrivateNetworks && !_ownsHttpClient &&
            await SsrfGuard.IsPrivateHostAsync(uri.Host, cancellationToken))
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

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (!IsAllowedContentType(contentType))
                {
                    _logger?.Warn(
                        $"event=media.content_type_rejected type={contentType ?? "(null)"} source={UrlRedactor.Redact(source)}");
                    return RecordFailure(source, $"Content-Type rejected: {contentType ?? "(null)"}");
                }

                var ext = ResolveExtension(uri, contentType);
                var fileName = BuildStableFileName(normalizedKey, ext);
                var localPath = Path.Combine(root, fileName);
                var tempPath = BuildTempFilePath(root, fileName);
                var bytesWritten = await WriteWithLimitAsync(response.Content, tempPath, maxFileSize, cts.Token);
                if (bytesWritten is null)
                {
                    _logger?.Warn($"event=media.download_too_large limit={maxFileSize} source={UrlRedactor.Redact(source)}");
                    return RecordFailure(source, $"File too large (limit: {maxFileSize} bytes)");
                }

                if (bytesWritten.Value == 0)
                {
                    DeleteFileBestEffort(tempPath);
                    if (attempt >= maxRetries)
                    {
                        _logger?.Warn($"event=media.download_empty source={UrlRedactor.Redact(source)}");
                        return RecordFailure(source, "Empty response body");
                    }

                    await DelayBeforeRetryAsync(attempt, retryBaseDelay, cancellationToken);
                    continue;
                }

                MoveTempFileIntoPlace(tempPath, localPath);

                var publicUrl = _indexManager.CombineUrl(fileName);
                _indexManager.RememberIndex(normalizedKey, fileName);
                _cache.TryAdd(normalizedKey, publicUrl);
                return publicUrl;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= maxRetries)
                {
                    _logger?.Warn(
                        $"event=media.download_error source={UrlRedactor.Redact(source)} {BuildDownloadErrorDiagnostic(ex)}");
                    return RecordFailure(source, $"{ex.GetType().Name}: {ex.Message}");
                }

                await DelayBeforeRetryAsync(attempt, retryBaseDelay, cancellationToken);
            }
        }
    }

    private static string BuildDownloadErrorDiagnostic(Exception exception)
    {
        var diagnostic = $"error={exception.GetType().Name}";
        var root = exception.GetBaseException();
        if (ReferenceEquals(root, exception))
        {
            return diagnostic;
        }

        var rootType = root.GetType().FullName ?? root.GetType().Name;
        return $"{diagnostic} root_error={rootType} root_message=\"{JsonEncodedText.Encode(root.Message)}\"";
    }

    private static bool IsLocalAssetReference(string source)
    {
        if (source.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (source.StartsWith("/", StringComparison.Ordinal) ||
            source.StartsWith("./", StringComparison.Ordinal) ||
            source.StartsWith("../", StringComparison.Ordinal))
        {
            return true;
        }

        return !Uri.TryCreate(source, UriKind.Absolute, out _);
    }

    private string RecordFailure(string sourceUrl, string reason)
    {
        _failures.Add(new MediaFailure(UrlRedactor.Redact(sourceUrl), reason));
        return _config.DefaultImageUrl;
    }

    private static async Task<long?> WriteWithLimitAsync(
        HttpContent content, string tempPath, long maxBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength.Value > maxBytes)
        {
            return null;
        }

        var completed = false;
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[8192];
        long totalRead = 0;
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += read;
                if (totalRead > maxBytes)
                {
                    return null;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            completed = true;
            return totalRead;
        }
        finally
        {
            if (!completed)
            {
                DeleteFileBestEffort(tempPath);
            }
        }
    }

    private static string BuildTempFilePath(string root, string fileName)
    {
        return Path.Combine(root, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static void MoveTempFileIntoPlace(string tempPath, string localPath)
    {
        try
        {
            File.Move(tempPath, localPath);
        }
        catch (IOException) when (File.Exists(localPath))
        {
            DeleteFileBestEffort(tempPath);
        }
        catch
        {
            DeleteFileBestEffort(tempPath);
            throw;
        }
    }

    private static void DeleteFileBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsAllowedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        var ct = contentType.Trim();
        return ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(ct, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

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

    private static string BuildStableFileName(string normalizedKey, string ext)
    {
        return $"{BuildHashPrefix(normalizedKey)}{ext}";
    }

    private static string BuildHashPrefix(string normalizedKey)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedKey));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        _indexManager.PersistIndex();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _lifetimeCancellation.Dispose();
    }
}
