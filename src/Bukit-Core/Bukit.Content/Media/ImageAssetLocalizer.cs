using Bukit.Engine.Abstractions.Content;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Bukit.Shared;
using static Bukit.Content.Media.MediaTempFiles;

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
    private readonly ConcurrentDictionary<string, Lazy<Task<DownloadOutcome>>> _inflight = new(StringComparer.Ordinal);
    // Full request targets whose download already failed in this localizer's lifetime; not
    // retried. Keyed by the signed target, not the media identity, so a fresh signature for the
    // same object is still tried. A localizer lives for one build: ImageLocalizeStage creates it
    // and the build disposes its body store.
    private readonly ConcurrentDictionary<string, byte> _failedTargets = new(StringComparer.Ordinal);
    // One own-URL retry at a time per media identity, after a shared download was refused.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _retryGates = new(StringComparer.Ordinal);
    // A queue keeps failures in the order they happened, for the build summary.
    private readonly ConcurrentQueue<MediaFailure> _failures = new();
    private readonly Lock _failureReportLock = new();
    private int _reportedFailureCount;
    private readonly MediaIndexManager _indexManager;
    private readonly ImageContentValidator _imageValidator = new();
    private readonly bool _keepOriginalOnFailure;
    private readonly MediaHostFilter _hostFilter;
    private int _disposeState;

    public IReadOnlyList<MediaFailure> Failures => _failures.ToArray();

    public ImageAssetLocalizer(MediaConfig config, ILogger? logger = null)
    {
        _keepOriginalOnFailure = ParseKeepOnFailure(config);
        _hostFilter = new MediaHostFilter(config.DownloadHosts, config.SkipHosts);
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
        _keepOriginalOnFailure = ParseKeepOnFailure(config);
        _hostFilter = new MediaHostFilter(config.DownloadHosts, config.SkipHosts);
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

        // Under onFailure: keep, failures return this text unchanged (before HTML decoding).
        var original = sourceUrl.Trim();
        var source = original;

        if (source.Contains("&amp;", StringComparison.Ordinal))
        {
            source = System.Net.WebUtility.HtmlDecode(source);
        }

        if (ResolveWithoutDownload(source, out var uri) is { } notDownloaded)
        {
            return notDownloaded;
        }

        var normalizedKey = NormalizeSourceUrlForKey(uri);
        var requestTarget = SignedUrlIdentity.RequestTarget(uri);

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
                AddFailure(source, "Unsafe cached media could not be removed.");
                return FailureResult(original);
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
                AddFailure(source, "Unsafe cached media could not be removed.");
                return FailureResult(original);
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
                AddFailure(source, "Unsafe cached media could not be removed.");
                return FailureResult(original);
            }
        }

        if (await FindLegacySignedFileAsync(root, uri, requestTarget, cancellationToken) is { } legacyName)
        {
            _indexManager.RememberIndex(normalizedKey, legacyName);
            _cache.TryAdd(normalizedKey, legacyName);
            return _indexManager.CombineUrl(legacyName);
        }

        // Checked after the success lookups: once any reference to an object has downloaded, an
        // expired signature for the same object reuses the local file instead of failing.
        if (_failedTargets.ContainsKey(requestTarget))
        {
            return FailedReference(source, original);
        }

        var newDownload = new Lazy<Task<DownloadOutcome>>(
            () => DownloadCoreAsync(normalizedKey, uri, source, root, singleAttempt: false, _lifetimeToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var download = _inflight.GetOrAdd(normalizedKey, newDownload);
        var downloadTask = download.Value;
        if (ReferenceEquals(download, newDownload))
        {
            _ = RemoveInflightWhenCompletedAsync(normalizedKey, download, downloadTask);
        }
        else
        {
            _logger?.Debug($"{SharedDownloadEvent} source={UrlRedactor.Redact(source)}");
        }

        try
        {
            var outcome = await downloadTask.WaitAsync(cancellationToken);
            if (outcome.LocalUrl is not null)
            {
                return outcome.LocalUrl;
            }

            // Callers sharing a download may spell the URL differently; a failure uses this caller's own source.
            if (string.Equals(outcome.RequestTarget, requestTarget, StringComparison.Ordinal))
            {
                return FailureResult(original);
            }

            // This caller's URL differs only by signature parameters. A refusal may just mean the
            // other signature expired, so this caller's own URL gets one try; any other failure is
            // about the object itself, and this caller's URL is recorded as failed without a request.
            if (outcome.StatusCode is 400 or 401 or 403)
            {
                return await RetryWithOwnUrlAsync(normalizedKey, uri, requestTarget, source, original, root, cancellationToken);
            }

            RecordFailedTarget(requestTarget, source, outcome.Reason);
            return FailureResult(original);
        }
        finally
        {
            if (downloadTask.IsCompleted)
            {
                _inflight.TryRemove(KeyValuePair.Create(normalizedKey, download));
            }
        }
    }

    // Serialized per media identity: the first waiter tries its own URL once; later waiters
    // usually find the result already cached, or their own URL already failed.
    private async Task<string> RetryWithOwnUrlAsync(
        string normalizedKey, Uri uri, string requestTarget, string source, string original, string root,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        var gate = _retryGates.GetOrAdd(normalizedKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(linked.Token);
        try
        {
            if (_cache.TryGetValue(normalizedKey, out var cachedFileName) &&
                await IsTrustedCachedFileAsync(root, cachedFileName, linked.Token))
            {
                return _indexManager.CombineUrl(cachedFileName);
            }

            if (_failedTargets.ContainsKey(requestTarget))
            {
                return FailedReference(source, original);
            }

            var outcome = await DownloadCoreAsync(normalizedKey, uri, source, root, singleAttempt: true, linked.Token);
            return outcome.LocalUrl ?? FailureResult(original);
        }
        finally
        {
            gate.Release();
        }
    }

    // Already failed (and reported) in this build: skip the retries and timeouts, but warn so
    // every page that still references the broken URL is visible.
    private string FailedReference(string source, string original)
    {
        _logger?.Warn($"event=media.failed_reference source={UrlRedactor.Redact(source)}");
        return FailureResult(original);
    }

    // Returns the URL to use when this source is not downloaded, or null when it should be.
    private string? ResolveWithoutDownload(string source, out Uri uri)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out uri!) ||
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

        if (!_config.DownloadToLocal)
        {
            _logger?.Warn($"event=media.skip_download_disabled source={UrlRedactor.Redact(source)}");
            return source;
        }

        if (!_hostFilter.ShouldDownload(uri))
        {
            _logger?.Debug($"event=media.skip_host source={UrlRedactor.Redact(source)}");
            return source;
        }

        if (string.IsNullOrWhiteSpace(_config.DownloadDir) || string.IsNullOrWhiteSpace(_config.UrlBase))
        {
            _logger?.Warn($"event=media.skip_missing_config source={UrlRedactor.Redact(source)} downloadDir={_config.DownloadDir} urlBase={_config.UrlBase}");
            return _config.DefaultImageUrl;
        }

        return null;
    }

    private async Task RemoveInflightWhenCompletedAsync(
        string normalizedKey,
        Lazy<Task<DownloadOutcome>> download,
        Task<DownloadOutcome> downloadTask)
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

    private async Task<DownloadOutcome> DownloadCoreAsync(
        string normalizedKey, Uri uri, string source, string root, bool singleAttempt,
        CancellationToken cancellationToken)
    {
        var requestTarget = SignedUrlIdentity.RequestTarget(uri);
        var maxRetries = !singleAttempt && _config.MaxRetries is >= 0 ? _config.MaxRetries.Value : 0;
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
                        return RecordDownloadFailure(requestTarget, source, $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
                    }

                    await DelayBeforeRetryAsync(attempt, retryBaseDelay, cancellationToken);
                    continue;
                }

                var headerContentType = response.Content.Headers.ContentType?.MediaType;
                // Generic binary types (e.g. Notion file storage) are identified from the file signature below.
                var identifyBySignature = IsGenericBinaryContentType(headerContentType);
                if (!identifyBySignature && !IsAllowedContentType(headerContentType))
                {
                    _logger?.Warn(
                        $"event=media.content_type_rejected type={headerContentType ?? "(null)"} source={UrlRedactor.Redact(source)}");
                    return RecordDownloadFailure(requestTarget, source, $"Content-Type rejected: {headerContentType ?? "(null)"}");
                }

                var tempPath = BuildTempFilePath(root, BuildFileIdentity(normalizedKey));
                var write = await WriteWithLimitAsync(
                    response.Content,
                    tempPath,
                    maxFileSize,
                    identifyBySignature ? null : headerContentType!.Trim(),
                    cts.Token);
                if (write.Status == WriteStatus.TooLarge)
                {
                    _logger?.Warn($"event=media.download_too_large limit={maxFileSize} source={UrlRedactor.Redact(source)}");
                    return RecordDownloadFailure(requestTarget, source, $"File too large (limit: {maxFileSize} bytes)");
                }

                if (write.Status == WriteStatus.SignatureRejected)
                {
                    _logger?.Warn(
                        $"event=media.content_type_rejected type={headerContentType ?? "(null)"} signature=unrecognized source={UrlRedactor.Redact(source)}");
                    return RecordDownloadFailure(
                        requestTarget,
                        source,
                        $"Content-Type rejected: {headerContentType ?? "(null)"} (image signature not recognized)");
                }

                if (write.BytesWritten == 0)
                {
                    DeleteFileBestEffort(tempPath);
                    if (attempt >= maxRetries)
                    {
                        _logger?.Warn($"event=media.download_empty source={UrlRedactor.Redact(source)}");
                        return RecordDownloadFailure(requestTarget, source, "Empty response body");
                    }

                    await DelayBeforeRetryAsync(attempt, retryBaseDelay, cancellationToken);
                    continue;
                }

                var contentType = write.ContentType;
                var ext = ResolveExtension(sendResult.FinalUri, contentType);
                var fileName = BuildStableFileName(normalizedKey, ext);
                var localPath = Path.Combine(root, fileName);

                bool signatureMatches;
                try
                {
                    signatureMatches = await _imageValidator.ValidateAsync(
                        tempPath,
                        contentType,
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
                    return RecordDownloadFailure(
                        requestTarget,
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
                    return RecordDownloadFailure(requestTarget, source, "Media file move conflicted with an unverifiable winner.");
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
                        return RecordDownloadFailure(requestTarget, source, "Media winner file failed content validation.");
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
                        return RecordDownloadFailure(
                            requestTarget,
                            source,
                            "Media winner file did not match downloaded content identity.");
                    }
                }

                var publicUrl = _indexManager.CombineUrl(fileName);
                _indexManager.RememberIndex(normalizedKey, fileName);
                _cache.TryAdd(normalizedKey, fileName);
                return new DownloadOutcome(publicUrl, requestTarget, null, null);
            }
            catch (UnsafeMediaTargetException ex)
            {
                _logger?.Warn(
                    $"event=media.ssrf_blocked source={UrlRedactor.Redact(source)} error={ex.GetType().Name}");
                return RecordDownloadFailure(requestTarget, source, "SSRF blocked (unsafe or unverifiable target)");
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
                    return RecordDownloadFailure(requestTarget, source, $"{ex.GetType().Name}: {ex.Message}");
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

    private void AddFailure(string sourceUrl, string reason)
        => _failures.Enqueue(new MediaFailure(UrlRedactor.Redact(sourceUrl), reason));

    /// <summary>Debug event logged when a caller joins a download that is already running.</summary>
    internal const string SharedDownloadEvent = "event=media.download_shared";

    /// <summary>
    /// Logs a warning summary of failures not yet reported. Called after the front-matter and HTML
    /// pass, and again on dispose for later failures (lazily localized bodies).
    /// </summary>
    internal void LogFailureSummary(ILogger logger, string phase)
    {
        ArgumentNullException.ThrowIfNull(logger);
        MediaFailure[] pending;
        lock (_failureReportLock)
        {
            var all = _failures.ToArray();
            pending = all[_reportedFailureCount..];
            _reportedFailureCount = all.Length;
        }

        if (pending.Length == 0)
        {
            return;
        }

        logger.Warn($"event=media.localize_summary phase={phase} failed={pending.Length}");
        foreach (var failure in pending)
        {
            logger.Warn($"  - {failure.SourceUrl} => {failure.Reason}");
        }
    }

    // Inside the shared download: remember the target, and let each waiting caller map the
    // failure to its own result.
    private DownloadOutcome RecordDownloadFailure(string requestTarget, string sourceUrl, string reason, int? statusCode = null)
    {
        RecordFailedTarget(requestTarget, sourceUrl, reason);
        return new DownloadOutcome(null, requestTarget, statusCode, reason);
    }

    // Only the caller that first records a target reports it, so concurrent callers never
    // report the same target twice.
    private void RecordFailedTarget(string requestTarget, string sourceUrl, string? reason)
    {
        if (_failedTargets.TryAdd(requestTarget, 0))
        {
            AddFailure(sourceUrl, reason ?? "Download failed.");
        }
    }

    /// <summary>
    /// Result of a download: the local URL, or null when it failed. <paramref name="RequestTarget"/>
    /// is the target that was requested, and <paramref name="StatusCode"/> the final HTTP status
    /// when the failure was an HTTP error.
    /// </summary>
    private readonly record struct DownloadOutcome(string? LocalUrl, string RequestTarget, int? StatusCode, string? Reason);

    private string FailureResult(string sourceUrl)
        => _keepOriginalOnFailure ? sourceUrl : _config.DefaultImageUrl;

    private static bool ParseKeepOnFailure(MediaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return MediaLocalizationRules.TryParseOnFailure(config.OnFailure, out var keep)
            ? keep
            : throw new ConfigException(MediaLocalizationRules.InvalidOnFailureMessage, DiagnosticCode.ConfigInvalidValue);
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

    // Signature parameters do not change which object is fetched; unsigned URLs hash exactly as before.
    private static string NormalizeSourceUrlForKey(Uri uri) =>
        KeyForTarget(SignedUrlIdentity.IdentityTarget(uri));

    private static string KeyForTarget(string target)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(target));
        return $"v3:{Convert.ToHexStringLower(hash)}";
    }

    /// <summary>
    /// Earlier versions keyed a signed URL by its full request target, signature included. A file
    /// stored that way is reused for the same signed URL, so a readonly cache whose links have
    /// expired keeps its local images after an upgrade. Untrusted files are left alone here.
    /// </summary>
    private async Task<string?> FindLegacySignedFileAsync(
        string root,
        Uri uri,
        string requestTarget,
        CancellationToken cancellationToken)
    {
        if (string.Equals(SignedUrlIdentity.IdentityTarget(uri), requestTarget, StringComparison.Ordinal))
        {
            return null;
        }

        var legacyKey = KeyForTarget(requestTarget);
        if (_indexManager.TryGetFileNameFromIndex(root, legacyKey, out var indexed) &&
            await IsTrustedCachedFileAsync(root, indexed, cancellationToken))
        {
            return indexed;
        }

        var existing = _indexManager.FindExistingFileByIdentity(root, BuildFileIdentity(legacyKey));
        return existing is not null && await IsTrustedCachedFileAsync(root, existing, cancellationToken)
            ? existing
            : null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        if (_logger is not null)
        {
            LogFailureSummary(_logger, "deferred");
        }

        _indexManager.PersistIndex();
        _httpClient.Dispose();

        _lifetimeCancellation.Dispose();
    }

    private sealed record MediaSendResult(HttpResponseMessage Response, Uri FinalUri);

    private sealed class UnsafeMediaTargetException(string message) : Exception(message);
}
