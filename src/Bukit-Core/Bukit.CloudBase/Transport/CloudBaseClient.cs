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
        client.Timeout = TimeSpan.FromMilliseconds(int.MaxValue);
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
            Timeout = TimeSpan.FromMilliseconds(int.MaxValue)
        };
    }

    internal async Task<int?> HeadCountAsync(string table, CancellationToken cancellationToken = default)
    {
        EnsureIdentifier(table);
        using var response = await SendWithRetryAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Head, $"{CloudBaseEndpoints.RestPrefix}{table}");
                request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
                return request;
            },
            cancellationToken).ConfigureAwait(false);
        return ParseContentRange(response);
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

            using var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, BuildQueryUri(query, limit, offset)),
                cancellationToken).ConfigureAwait(false);
            using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
            }

            total ??= ParseContentRange(response);
            var pageRows = 0;
            foreach (var row in document.RootElement.EnumerateArray())
            {
                rows.Add(row.Clone());
                pageRows++;
            }

            offset += pageRows;
            if (pageRows < limit)
            {
                break;
            }

            if (total is { } totalCount && offset >= totalCount)
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
            using var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Post, $"{CloudBaseEndpoints.SignPrefix}{bucket}")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                },
                cancellationToken).ConfigureAwait(false);
            using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() != batch.Length)
            {
                throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
            }

            var itemIndex = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var (resultIndex, name) = batch[itemIndex++];
                results[resultIndex] = ParseSignedObject(name, bucket, item);
            }
        }

        return results;
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
            throw new CloudBaseApiException(CloudBaseErrorKind.ResponseInvalid);
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
            string.Equals(uri.Host, _options.GatewayHost, StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith($"{CloudBaseEndpoints.SignPrefix}{bucket}/", StringComparison.Ordinal);
    }

    private static string? GetString(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
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

    private static int? ParseContentRange(HttpResponseMessage response)
    {
        if (!response.Content.Headers.TryGetValues("Content-Range", out var values) &&
            !response.Headers.TryGetValues("Content-Range", out values))
        {
            return null;
        }

        var header = values.FirstOrDefault();
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

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
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

            HttpResponseMessage response;
            try
            {
                response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= _options.MaxRetries)
                {
                    throw new CloudBaseApiException(CloudBaseErrorKind.RequestFailed, inner: ex);
                }

                await DelayBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= _options.MaxRetries)
                {
                    throw new CloudBaseApiException(CloudBaseErrorKind.RequestFailed, inner: ex);
                }

                await DelayBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500;
            if (retryable && attempt < _options.MaxRetries)
            {
                response.Dispose();
                await DelayBackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                using (response)
                {
                    throw await CreateApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
                }
            }

            return response;
        }
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

    private async Task<CloudBaseApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var code = await ReadErrorCodeAsync(response, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await ReadLimitedBytesAsync(response.Content, ErrorBodyBytes, cancellationToken)
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
        catch (CloudBaseApiException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadLimitedBytesAsync(response.Content, _options.MaxResponseBytes, cancellationToken)
            .ConfigureAwait(false);
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
}
