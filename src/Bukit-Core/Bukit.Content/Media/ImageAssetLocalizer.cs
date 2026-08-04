using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Content.Media;

public sealed class ImageAssetLocalizer : IImageAssetLocalizer, IDisposable
{
    private const string UserAgentValue = "Bukit/1.0";
    private const long DefaultMaxFileSize = 50L * 1024 * 1024;
    private const int MaxRedirects = 5;

    private static readonly IReadOnlyDictionary<string, string> ContentTypeToExt =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp",
            ["image/avif"] = ".avif",
            ["image/bmp"] = ".bmp",
            ["image/x-icon"] = ".ico",
            ["image/vnd.microsoft.icon"] = ".ico",
            ["image/ico"] = ".ico",
            ["image/tiff"] = ".tiff"
        };

    internal static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".bmp",
            ".ico", ".tiff", ".tif"
        };

    private readonly MediaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHostAddresses;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<MediaFailure> _failures = new();
    private readonly MediaIndexManager _indexManager;
    private readonly ImageContentValidator _imageValidator = new();
    private int _disposeState;

    public IReadOnlyList<MediaFailure> Failures => _failures.ToArray();

    public ImageAssetLocalizer(MediaConfig config, ILogger? logger = null)
    {
        _config = config;
        _logger = logger;
        _resolveHostAddresses = ResolveHostAddressesAsync;
        _lifetimeToken = _lifetimeCancellation.Token;
        _indexManager = new MediaIndexManager(
            config.DownloadDir ?? string.Empty, config.UrlBase ?? string.Empty, logger);

        var handler = CreateDefaultHandler(config);
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentValue);
    }

    internal static SocketsHttpHandler CreateDefaultHandler(MediaConfig config)
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        if (config.BlockPrivateNetworks)
        {
            handler.ConnectCallback = SsrfGuard.SsrfSafeConnectAsync;
        }

        return handler;
    }

    internal ImageAssetLocalizer(
        MediaConfig config,
        HttpMessageHandler handler,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHostAddresses,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(resolveHostAddresses);
        _config = config;
        _httpClient = new HttpClient(handler, disposeHandler: true);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentValue);
        _resolveHostAddresses = resolveHostAddresses;
        _logger = logger;
        _lifetimeToken = _lifetimeCancellation.Token;
        _indexManager = new MediaIndexManager(
            config.DownloadDir ?? string.Empty, config.UrlBase ?? string.Empty, logger);
    }

    internal ImageAssetLocalizer(MediaConfig config, HttpClient httpClient, ILogger? logger = null)
        => throw new NotSupportedException(
            "Injected HttpClient is not supported because redirect and connection policy cannot be enforced. Inject a single-hop HttpMessageHandler instead.");

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

        var root = _config.DownloadDir.Trim();
        Directory.CreateDirectory(root);
        _indexManager.EnsureIndexLoaded(root);

        if (_cache.TryGetValue(normalizedKey, out var cachedFileName))
        {
            if (await IsTrustedCachedFileAsync(root, cachedFileName, cancellationToken))
            {
                return _indexManager.CombineUrl(cachedFileName);
            }

            _cache.TryRemove(normalizedKey, out _);
            _indexManager.ForgetIndex(normalizedKey);
            if (!DeleteUntrustedCachedFile(root, cachedFileName))
            {
                return RecordFailure(source, "Unsafe cached media could not be removed.");
            }
        }

        if (_indexManager.TryGetFileNameFromIndex(root, normalizedKey, out var indexedFileName))
        {
            if (await IsTrustedCachedFileAsync(root, indexedFileName, cancellationToken))
            {
                _cache.TryAdd(normalizedKey, indexedFileName);
                return _indexManager.CombineUrl(indexedFileName);
            }

            _indexManager.ForgetIndex(normalizedKey);
            if (!DeleteUntrustedCachedFile(root, indexedFileName))
            {
                return RecordFailure(source, "Unsafe cached media could not be removed.");
            }
        }

        var fileIdentity = BuildFileIdentity(normalizedKey);
        var existingName = _indexManager.FindExistingFileByIdentity(root, fileIdentity);
        if (existingName is not null)
        {
            if (await IsTrustedCachedFileAsync(root, existingName, cancellationToken))
            {
                _indexManager.RememberIndex(normalizedKey, existingName);
                _cache.TryAdd(normalizedKey, existingName);
                return _indexManager.CombineUrl(existingName);
            }

            if (!DeleteUntrustedCachedFile(root, existingName))
            {
                return RecordFailure(source, "Unsafe cached media could not be removed.");
            }
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
        var maxRetries = _config.MaxRetries is >= 0 ? _config.MaxRetries.Value : 0;
        var maxFileSize = _config.MaxFileSizeBytes is > 0
            ? _config.MaxFileSizeBytes.Value
            : DefaultMaxFileSize;
        var retryBaseDelay = _config.RetryBaseDelayMs is > 0 ? _config.RetryBaseDelayMs.Value : 0;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (_config.TimeoutMs is > 0)
                {
                    cts.CancelAfter(_config.TimeoutMs.Value);
                }

                var sendResult = await SendWithRedirectsAsync(uri, cts.Token);
                using var response = sendResult.Response;

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

                var ext = ResolveExtension(sendResult.FinalUri, contentType);
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

                bool signatureMatches;
                try
                {
                    signatureMatches = await _imageValidator.ValidateAsync(
                        tempPath,
                        contentType!,
                        cts.Token);
                }
                catch
                {
                    DeleteFileBestEffort(tempPath);
                    throw;
                }

                if (!signatureMatches)
                {
                    DeleteFileBestEffort(tempPath);
                    _logger?.Warn(
                        $"event=media.signature_rejected type={contentType} source={UrlRedactor.Redact(source)}");
                    return RecordFailure(
                        source,
                        "Image content signature does not match Content-Type or is not decodable.");
                }

                FileContentIdentity expectedIdentity;
                try
                {
                    expectedIdentity = await ReadFileContentIdentityAsync(tempPath, cts.Token);
                }
                catch
                {
                    DeleteFileBestEffort(tempPath);
                    throw;
                }

                // Move winner: on collision, re-open and fully re-validate the file that
                // actually won the race before treating it as our cached result.
                if (!MoveTempFileIntoPlace(tempPath, localPath, out var winnerIsTrusted))
                {
                    DeleteFileBestEffort(tempPath);
                    _logger?.Warn(
                        $"event=media.move_conflict_unverified source={UrlRedactor.Redact(source)}");
                    return RecordFailure(source, "Media file move conflicted with an unverifiable winner.");
                }

                if (!winnerIsTrusted)
                {
                    // Another writer won the race; verify MIME/signature/decode,
                    // then require the exact downloaded content identity.
                    bool winnerValid;
                    try
                    {
                        winnerValid = await _imageValidator.ValidateAsync(
                            localPath,
                            contentType!,
                            cts.Token);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        winnerValid = false;
                    }

                    if (!winnerValid)
                    {
                        _logger?.Warn(
                            $"event=media.winner_rejected type={contentType} source={UrlRedactor.Redact(source)}");
                        return RecordFailure(source, "Media winner file failed content validation.");
                    }

                    bool winnerIdentityMatches;
                    try
                    {
                        var winnerIdentity = await ReadFileContentIdentityAsync(localPath, cts.Token);
                        winnerIdentityMatches = winnerIdentity.Length == expectedIdentity.Length &&
                            CryptographicOperations.FixedTimeEquals(
                                winnerIdentity.Sha256,
                                expectedIdentity.Sha256);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        winnerIdentityMatches = false;
                    }

                    if (!winnerIdentityMatches)
                    {
                        _logger?.Warn(
                            $"event=media.winner_identity_rejected source={UrlRedactor.Redact(source)}");
                        return RecordFailure(
                            source,
                            "Media winner file did not match downloaded content identity.");
                    }
                }

                var publicUrl = _indexManager.CombineUrl(fileName);
                _indexManager.RememberIndex(normalizedKey, fileName);
                _cache.TryAdd(normalizedKey, fileName);
                return publicUrl;
            }
            catch (UnsafeMediaTargetException ex)
            {
                _logger?.Warn(
                    $"event=media.ssrf_blocked source={UrlRedactor.Redact(source)} error={ex.GetType().Name}");
                return RecordFailure(source, "SSRF blocked (unsafe or unverifiable target)");
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

    private async Task<MediaSendResult> SendWithRedirectsAsync(
        Uri initialUri,
        CancellationToken cancellationToken)
    {
        var currentUri = initialUri;
        for (var redirectCount = 0; ; redirectCount++)
        {
            if (!await IsRequestTargetAllowedAsync(currentUri, cancellationToken))
            {
                throw new UnsafeMediaTargetException("Media request target failed SSRF validation.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!IsRedirectStatus(response.StatusCode) || response.Headers.Location is null)
            {
                return new MediaSendResult(response, currentUri);
            }

            if (redirectCount >= MaxRedirects)
            {
                response.Dispose();
                throw new UnsafeMediaTargetException($"Media request exceeded {MaxRedirects} redirects.");
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (!Uri.TryCreate(currentUri, location, out var redirectUri))
            {
                throw new UnsafeMediaTargetException("Media redirect target is invalid.");
            }

            currentUri = redirectUri;
        }
    }

    private async Task<bool> IsRequestTargetAllowedAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (!_config.BlockPrivateNetworks)
        {
            return true;
        }

        if (IPAddress.TryParse(uri.DnsSafeHost, out var literalAddress))
        {
            return !SsrfGuard.IsPrivateAddress(literalAddress);
        }

        try
        {
            var addresses = await _resolveHostAddresses(uri.DnsSafeHost, cancellationToken);
            return addresses.Length > 0 && addresses.All(static address => !SsrfGuard.IsPrivateAddress(address));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static Task<IPAddress[]> ResolveHostAddressesAsync(
        string host,
        CancellationToken cancellationToken)
        => Dns.GetHostAddressesAsync(host, cancellationToken);

    private static bool IsRedirectStatus(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

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

    private static async Task<FileContentIdentity> ReadFileContentIdentityAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = stream.Length;
        var sha256 = await SHA256.HashDataAsync(stream, cancellationToken);
        return new FileContentIdentity(length, sha256);
    }

    private static bool MoveTempFileIntoPlace(string tempPath, string localPath, out bool winnerIsTrusted)
    {
        winnerIsTrusted = true;
        try
        {
            File.Move(tempPath, localPath);
            return true;
        }
        catch (IOException) when (File.Exists(localPath))
        {
            // Another writer won the race; the winner needs re-validation
            winnerIsTrusted = false;
            DeleteFileBestEffort(tempPath);
            return true;
        }
        catch
        {
            DeleteFileBestEffort(tempPath);
            return false;
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
        return !string.IsNullOrWhiteSpace(contentType) &&
               ContentTypeToExt.ContainsKey(contentType.Trim());
    }

    private async Task<bool> IsTrustedCachedFileAsync(
        string root,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!MediaIndexManager.IsSafeFileName(fileName) ||
            !TryGetContentTypeForExtension(Path.GetExtension(fileName), out var contentType))
        {
            return false;
        }

        try
        {
            return await _imageValidator.ValidateAsync(
                Path.Combine(root, fileName),
                contentType,
                cancellationToken);
        }
        catch (IOException ex)
        {
            _logger?.Warn($"event=media.cache_read_failed fileName={fileName} error={ex.GetType().Name}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Warn($"event=media.cache_read_failed fileName={fileName} error={ex.GetType().Name}");
            return false;
        }
    }

    private static bool TryGetContentTypeForExtension(string extension, out string contentType)
    {
        contentType = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            ".tif" or ".tiff" => "image/tiff",
            _ => string.Empty
        };
        return contentType.Length > 0;
    }

    private static bool DeleteUntrustedCachedFile(string root, string fileName)
    {
        if (!MediaIndexManager.IsSafeFileName(fileName))
        {
            return false;
        }

        var path = Path.Combine(root, fileName);
        DeleteFileBestEffort(path);
        return !File.Exists(path);
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
        return $"{BuildFileIdentity(normalizedKey)}{ext}";
    }

    private static string BuildFileIdentity(string normalizedKey)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedKey));
        return Convert.ToHexStringLower(hash);
    }

    private static string NormalizeSourceUrlForKey(Uri uri)
    {
        var requestTarget = uri.GetComponents(
            UriComponents.HttpRequestUrl,
            UriFormat.UriEscaped);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestTarget));
        return $"v3:{Convert.ToHexStringLower(hash)}";
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        _indexManager.PersistIndex();
        _httpClient.Dispose();

        _lifetimeCancellation.Dispose();
    }

    private sealed record MediaSendResult(HttpResponseMessage Response, Uri FinalUri);

    private readonly record struct FileContentIdentity(long Length, byte[] Sha256);

    private sealed class UnsafeMediaTargetException(string message) : Exception(message);
}
