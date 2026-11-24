using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bukit.CloudBase.Models;

namespace Bukit.CloudBase.Transport;

internal sealed class CloudBaseClient : IDisposable
{
    /// <summary>Test seam: when set, the transport uses this handler instead of the network.</summary>
    internal static readonly AsyncLocal<Func<HttpMessageHandler>?> TransportOverride = new();

    private const int SignBatchSize = 500;
    private const int ErrorBodyBytes = 8 * 1024;
    private const int BackoffBaseMs = 500;
    private const int BackoffMaxMs = 8_000;
    private const int RetryAfterMaxMs = 30_000;
    internal const string SignMismatchError = "sign-mismatch";

    private readonly CloudBaseClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Func<int, CancellationToken, Task> _delayAsync;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly System.Threading.Lock _throttleLock = new();
    private DateTimeOffset _nextPermitAt = DateTimeOffset.MinValue;
    private int _disposed;

    internal CloudBaseClient(CloudBaseClientOptions options)
        : this(
            options,
            CreateDefaultHttpClient(options),
            static (milliseconds, cancellationToken) => Task.Delay(milliseconds, cancellationToken),
            static () => DateTimeOffset.UtcNow)
    {
    }

    private static HttpClient CreateDefaultHttpClient(CloudBaseClientOptions options)
    {
        var client = TransportOverride.Value?.Invoke() is { } handler
            ? new HttpClient(handler, disposeHandler: true)
            : new HttpClient();
        client.BaseAddress = options.GatewayOrigin;

        // Each attempt carries its own deadline (CloudBaseClientOptions.RequestTimeout).
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    internal CloudBaseClient(CloudBaseClientOptions options, HttpMessageHandler handler)
        : this(
            options,
            CreateHttpClient(options, handler),
            static (milliseconds, cancellationToken) => Task.Delay(milliseconds, cancellationToken),
            static () => DateTimeOffset.UtcNow)
    {
    }

    internal CloudBaseClient(
        CloudBaseClientOptions options,
        HttpMessageHandler handler,
        Func<int, CancellationToken, Task> delayAsync,
        Func<DateTimeOffset> utcNow)
        : this(options, CreateHttpClient(options, handler), delayAsync, utcNow)
    {
    }

    private CloudBaseClient(
        CloudBaseClientOptions options,
        HttpClient httpClient,
        Func<int, CancellationToken, Task> delayAsync,
        Func<DateTimeOffset> utcNow)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(delayAsync);
        ArgumentNullException.ThrowIfNull(utcNow);
        options.Validate();

        _options = options;
        _httpClient = httpClient;
        _delayAsync = delayAsync;
        _utcNow = utcNow;
    }

    private static HttpClient CreateHttpClient(CloudBaseClientOptions options, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = options.GatewayOrigin,
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    internal async Task<int?> HeadCountAsync(string table, CancellationToken cancellationToken = default)
    {
        EnsureIdentifier(table);
        var response = await SendWithRetryAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Head, $"{CloudBaseEndpoints.RestPrefix}{table}");
                request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
                return request;
            },
            ErrorBodyBytes,
            cancellationToken).ConfigureAwait(false);
        return ParseContentRange(response.ContentRange);
    }

    internal async Task<CloudBaseQueryResult> QueryAsync(
        CloudBaseQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentifier(query.Table);
        EnsureIdentifier(query.IdField);
        if (query.SortField is not null)
        {
            EnsureIdentifier(query.SortField);
        }

        if (query.FilterField is not null)
        {
            EnsureIdentifier(query.FilterField);
        }

        var rows = new List<JsonElement>();
        int? total = null;
        var offset = 0;
        while (true)
        {
            var limit = query.PageSize;
            if (query.MaxItems is { } maxItems)
            {
                limit = Math.Min(limit, maxItems - rows.Count);
            }

            if (limit <= 0)
            {
                break;
            }

            var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, BuildQueryUri(query, limit, offset)),
                _options.MaxResponseBytes,
                cancellationToken).ConfigureAwait(false);
            using var document = ParseJson(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
            }

            total ??= ParseContentRange(response.ContentRange);
            var pageRows = 0;
            foreach (var row in document.RootElement.EnumerateArray())
            {
                rows.Add(row.Clone());
                pageRows++;
            }

            offset += pageRows;
            if (pageRows == 0)
            {
                break;
            }

            // With a known total, a short page may only mean the gateway caps rows per
            // response, so keep paging until the total is reached or a page comes back empty.
            if (total is { } totalCount ? offset >= totalCount : pageRows < limit)
            {
                break;
            }
        }

        return new CloudBaseQueryResult(rows, total);
    }

    internal async Task<IReadOnlyList<CloudBaseSignedObject>> SignObjectsAsync(
        string bucket,
        IReadOnlyList<string> objectNames,
        int expiresIn,
        CancellationToken cancellationToken = default)
    {
        if (!CloudBaseEndpoints.BucketPattern().IsMatch(bucket))
        {
            throw new ArgumentException("storageBucket is not a valid CloudBase bucket name.", nameof(bucket));
        }

        if (expiresIn is < 60 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresIn));
        }

        ArgumentNullException.ThrowIfNull(objectNames);

        var results = new CloudBaseSignedObject[objectNames.Count];
        var pending = new List<(int Index, string Name)>();
        for (var index = 0; index < objectNames.Count; index++)
        {
            var name = objectNames[index];
            if (IsValidObjectName(name))
            {
                pending.Add((index, name));
            }
            else
            {
                results[index] = new CloudBaseSignedObject(name ?? string.Empty, null, "invalid-object-name");
            }
        }

        foreach (var batch in pending.Chunk(SignBatchSize))
        {
            var payload = new CloudBaseSignRequestPayload(
                expiresIn,
                [.. batch.Select(item => item.Name)]);
            var body = JsonSerializer.Serialize(payload, CloudBaseJsonContext.Default.CloudBaseSignRequestPayload);
            var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"{CloudBaseEndpoints.SignPrefix}{bucket}")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                },
                _options.MaxResponseBytes,
                cancellationToken).ConfigureAwait(false);
            using var document = ParseJson(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
            }

            MapSignedBatch(document.RootElement, batch, bucket, results);
        }

        return results;
    }

    /// <summary>
    /// Maps one sign response onto its batch by <c>path</c>. An item the gateway answered
    /// ambiguously (an unknown or repeated path, or no usable answer at all) fails on its own
    /// with <see cref="SignMismatchError"/>; the rest of the batch keeps its URLs.
    /// </summary>
    private void MapSignedBatch(
        JsonElement items,
        (int Index, string Name)[] batch,
        string bucket,
        CloudBaseSignedObject[] results)
    {
        // The first slot of each requested name; repeated names share its answer.
        var slotsByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var slot = 0; slot < batch.Length; slot++)
        {
            slotsByName.TryAdd(batch[slot].Name, slot);
        }

        // A path-less item can only be trusted positionally when the response lines up.
        var positional = items.GetArrayLength() == batch.Length;
        var answers = new CloudBaseSignedObject?[batch.Length];
        var conflicted = new bool[batch.Length];
        var itemIndex = 0;
        foreach (var item in items.EnumerateArray())
        {
            var path = GetString(item, "path");
            int slot;
            if (path is null)
            {
                slot = positional ? itemIndex : -1;
            }
            else if (!slotsByName.TryGetValue(path, out slot))
            {
                slot = -1;
            }

            itemIndex++;
            if (slot < 0)
            {
                continue;
            }

            if (answers[slot] is not null)
            {
                // Two answers for one object: trusting either could attach another object's URL.
                conflicted[slot] = true;
                continue;
            }

            answers[slot] = ParseSignedObject(batch[slot].Name, bucket, item);
        }

        for (var slot = 0; slot < batch.Length; slot++)
        {
            var (index, name) = batch[slot];
            var first = slotsByName[name];
            results[index] = answers[first] is { } answer && !conflicted[first]
                ? answer
                : new CloudBaseSignedObject(name, null, SignMismatchError);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _httpClient.Dispose();
        }
    }

    private CloudBaseSignedObject ParseSignedObject(string name, string bucket, JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return new CloudBaseSignedObject(name, null, SignMismatchError);
        }

        if (item.TryGetProperty("error", out var error) &&
            error.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(error.GetString()))
        {
            return new CloudBaseSignedObject(name, null, error.GetString());
        }

        var signedUrl = GetString(item, "signedURL");
        var fullSignedUrl = GetString(item, "fullSignedURL");
        var url = !string.IsNullOrWhiteSpace(fullSignedUrl)
            ? fullSignedUrl
            : ComposeGatewayUrl(signedUrl);
        if (!IsAcceptableSignedUrl(url, bucket))
        {
            return new CloudBaseSignedObject(name, null, "invalid-signed-url");
        }

        return new CloudBaseSignedObject(name, url, null);
    }

    private string? ComposeGatewayUrl(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || !relative.StartsWith('/'))
        {
            return null;
        }

        return new Uri(_options.GatewayOrigin, relative).AbsoluteUri;
    }

    private bool IsAcceptableSignedUrl(string? url, string bucket)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) &&
            uri.IsDefaultPort &&
            string.Equals(uri.Host, _options.GatewayHost, StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith($"{CloudBaseEndpoints.SignPrefix}{bucket}/", StringComparison.Ordinal);
    }

    private static string? GetString(JsonElement item, string propertyName)
        => item.ValueKind == JsonValueKind.Object &&
            item.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool IsValidObjectName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in name.Split('/'))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureIdentifier(string value)
    {
        if (!CloudBaseEndpoints.IdentifierPattern().IsMatch(value))
        {
            throw new ArgumentException("Expected a CloudBase identifier matching ^[A-Za-z_][A-Za-z0-9_]{0,62}$.");
        }
    }

    private string BuildQueryUri(CloudBaseQueryRequest query, int limit, int offset)
    {
        var builder = new StringBuilder(CloudBaseEndpoints.RestPrefix).Append(query.Table).Append('?');
        var first = true;
        if (query.Select is { Count: > 0 } select)
        {
            builder.Append("select=").Append(string.Join(",", select.Select(Uri.EscapeDataString)));
            first = false;
        }

        if (query.FilterField is { } filterField)
        {
            if (!first)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(filterField))
                .Append("=eq.")
                .Append(Uri.EscapeDataString(query.FilterValue ?? string.Empty));
            first = false;
        }

        if (!first)
        {
            builder.Append('&');
        }

        var order = query.SortField is { } sortField
            ? $"{sortField}.{query.SortDirection},{query.IdField}.asc"
            : $"{query.IdField}.asc";
        builder.Append("order=").Append(Uri.EscapeDataString(order))
            .Append("&limit=").Append(limit.ToString(CultureInfo.InvariantCulture))
            .Append("&offset=").Append(offset.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string? GetContentRange(HttpResponseMessage response)
        => response.Content.Headers.TryGetValues("Content-Range", out var values) ||
            response.Headers.TryGetValues("Content-Range", out values)
                ? values.FirstOrDefault() ?? string.Empty
                : null;

    private static int? ParseContentRange(string? header)
    {
        if (header is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(header) || !header.Contains('/', StringComparison.Ordinal))
        {
            throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
        }

        var range = header[(header.IndexOf('/') + 1)..];
        if (!int.TryParse(range, NumberStyles.None, CultureInfo.InvariantCulture, out var total) || total < 0)
        {
            throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
        }

        return total;
    }

    /// <summary>
    /// Sends a request and reads its whole body inside one retry loop, so a stalled gateway
    /// (per-attempt deadline) or a connection dropped mid-body is retried and then mapped to
    /// <see cref="CloudBaseErrorKind.RequestFailed"/> like any other transport failure.
    /// </summary>
    private async Task<BufferedResponse> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        int maxBodyBytes,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);
            using var request = requestFactory();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.PublishableKey);
            if (request.Method == HttpMethod.Get)
            {
                request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_options.RequestTimeout);
            int? retryAfterMs = null;
            try
            {
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                    .ConfigureAwait(false);
                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500;
                if (retryable && attempt < _options.MaxRetries)
                {
                    retryAfterMs = GetRetryAfterMs(response);
                }
                else
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw await CreateApiExceptionAsync(response, deadline.Token, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    // A HEAD response has no body, whatever Content-Length it declares.
                    var body = request.Method == HttpMethod.Head
                        ? []
                        : await ReadLimitedBytesAsync(response.Content, maxBodyBytes, deadline.Token)
                            .ConfigureAwait(false);
                    return new BufferedResponse(body, GetContentRange(response));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
            {
                // A cancelled caller can surface as a transport error; report the cancellation, not a failure.
                cancellationToken.ThrowIfCancellationRequested();
                if (attempt >= _options.MaxRetries)
                {
                    throw new CloudBaseApiException(CloudBaseErrorKind.RequestFailed, inner: ex);
                }
            }

            if (retryAfterMs is { } waitMs)
            {
                await _delayAsync(waitMs, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await DelayBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The server-requested wait from <c>Retry-After</c> (seconds or an HTTP date), capped so a
    /// hostile or broken value cannot stall the build; null when absent or already past.
    /// </summary>
    private int? GetRetryAfterMs(HttpResponseMessage response)
    {
        var wait = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - _utcNow(),
            _ => (TimeSpan?)null
        };
        if (wait is not { } value || value <= TimeSpan.Zero)
        {
            return null;
        }

        return (int)Math.Ceiling(Math.Min(value.TotalMilliseconds, RetryAfterMaxMs));
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        var intervalMs = 1000.0 / _options.MaxRps;
        int waitMs;
        lock (_throttleLock)
        {
            var now = _utcNow();
            if (_nextPermitAt < now)
            {
                _nextPermitAt = now;
            }

            waitMs = (int)Math.Ceiling((_nextPermitAt - now).TotalMilliseconds);
            _nextPermitAt = _nextPermitAt.AddMilliseconds(intervalMs);
        }

        if (waitMs > 0)
        {
            await _delayAsync(waitMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task DelayBackoffAsync(int attempt, CancellationToken cancellationToken)
    {
        var delayMs = Math.Min(BackoffMaxMs, BackoffBaseMs * (1 << Math.Min(attempt, 5)));
        return _delayAsync(delayMs, cancellationToken);
    }

    /// <param name="readToken">The attempt deadline, which bounds reading the error body.</param>
    /// <param name="callerToken">The caller's token; only its cancellation is propagated.</param>
    private static async Task<CloudBaseApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken readToken,
        CancellationToken callerToken)
    {
        var code = await ReadErrorCodeAsync(response, readToken, callerToken).ConfigureAwait(false);
        var kind = (int)response.StatusCode switch
        {
            401 => CloudBaseErrorKind.Unauthorized,
            403 => CloudBaseErrorKind.Forbidden,
            404 => CloudBaseErrorKind.NotFound,
            429 => CloudBaseErrorKind.RateLimited,
            _ => CloudBaseErrorKind.RequestFailed
        };
        return new CloudBaseApiException(kind, (int)response.StatusCode, code);
    }

    private static async Task<string?> ReadErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken readToken,
        CancellationToken callerToken)
    {
        try
        {
            var bytes = await ReadLimitedBytesAsync(response.Content, ErrorBodyBytes, readToken)
                .ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("code", out var code) &&
                code.ValueKind == JsonValueKind.String
                    ? code.GetString()
                    : null;
        }
        catch (Exception ex) when (
            ex is CloudBaseApiException or JsonException or IOException or HttpRequestException ||
            (ex is OperationCanceledException && !callerToken.IsCancellationRequested))
        {
            // The status code already decides the error kind; the provider code is best effort,
            // so a deadline hit while reading it must not turn a 401 into a retried failure.
            return null;
        }
    }

    private static JsonDocument ParseJson(byte[] bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException ex)
        {
            throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid, inner: ex);
        }
    }

    private static async Task<byte[]> ReadLimitedBytesAsync(
        HttpContent content,
        int limit,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is { } declared && declared > limit)
        {
            throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private readonly record struct BufferedResponse(byte[] Body, string? ContentRange);
}
