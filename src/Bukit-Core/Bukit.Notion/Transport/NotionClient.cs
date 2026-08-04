using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Bukit.Notion.Transport;

public sealed class NotionClient : IDisposable
{
    private const int MaxRetryDelayMs = 60_000;
    private const int BaseRetryDelayMs = 1_000;
    private static readonly TimeSpan MaxHttpClientTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
    private const string UnsupportedHandlerMessage =
        "NotionClient requires a handler chain ending in SocketsHttpHandler or HttpClientHandler so automatic redirects can be disabled.";
    private readonly NotionClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Func<int, CancellationToken, Task> _delayAsync;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly bool _ownsHttpClient;
    private readonly bool _injectedTransport;
    private readonly object _throttleLock = new();
    private DateTimeOffset _nextPermitAt = DateTimeOffset.MinValue;
    private long _requestCount;
    private long _throttleWaitCount;
    private long _throttleWaitTotalMs;
    private int _disposed;

    public NotionClient(NotionClientOptions options)
        : this(
            options,
            CreateHttpClient(options),
            static (milliseconds, cancellationToken) => Task.Delay(milliseconds, cancellationToken),
            static () => DateTimeOffset.UtcNow,
            ownsHttpClient: true,
            injectedTransport: false)
    {
    }

    public NotionClient(NotionClientOptions options, HttpMessageHandler handler)
        : this(
            options,
            CreateHttpClient(options, handler),
            static (milliseconds, cancellationToken) => Task.Delay(milliseconds, cancellationToken),
            static () => DateTimeOffset.UtcNow,
            ownsHttpClient: true,
            injectedTransport: false)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    internal NotionClient(
        NotionClientOptions options,
        HttpMessageHandler handler,
        Func<int, CancellationToken, Task> delayAsync,
        Func<DateTimeOffset> utcNow)
        : this(
            options,
            CreateTrustedSingleHopHttpClient(options, handler),
            delayAsync,
            utcNow,
            ownsHttpClient: true,
            injectedTransport: false)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    [Obsolete("Use NotionClient(NotionClientOptions) or a public constructor with a supported no-redirect handler chain. Injected HttpClient instances cannot reliably disable redirect following.")]
    public NotionClient(NotionClientOptions options, HttpClient httpClient)
        : this(
            options,
            httpClient,
            static (milliseconds, cancellationToken) => Task.Delay(milliseconds, cancellationToken),
            static () => DateTimeOffset.UtcNow,
            ownsHttpClient: false,
            injectedTransport: true)
    {
    }

    internal NotionClient(
        NotionClientOptions options,
        HttpClient httpClient,
        Func<int, CancellationToken, Task> delayAsync,
        Func<DateTimeOffset> utcNow,
        bool ownsHttpClient,
        bool injectedTransport = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(delayAsync);
        ArgumentNullException.ThrowIfNull(utcNow);
        ValidateOptions(options);

        _options = options;
        _httpClient = httpClient;
        _delayAsync = delayAsync;
        _utcNow = utcNow;
        _ownsHttpClient = ownsHttpClient;
        _injectedTransport = injectedTransport;
    }

    private static void ValidateOptions(NotionClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new ArgumentException("Notion token is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ApiVersion))
        {
            throw new ArgumentException("Notion API version is required.", nameof(options));
        }
    }

    public async Task<JsonDocument> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync(request, NotionRequestSemantics.IdempotentRead, cancellationToken);
    }

    public async Task<JsonDocument> SendAsync(
        HttpRequestMessage request,
        NotionRequestSemantics semantics,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_injectedTransport)
        {
            throw new NotSupportedException(
                "NotionClient instances constructed with an arbitrary HttpClient cannot send requests " +
                "because redirect following cannot be reliably disabled. " +
                "Use NotionClient(NotionClientOptions) or a supported no-redirect handler chain instead.");
        }
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestUri is null)
        {
            throw new ArgumentException("Notion request URI is required.", nameof(request));
        }

        ValidateRequestTarget(request);

        var bufferedRequest = await BufferedRequest.CreateAsync(request, cancellationToken);
        var maxRetries = semantics == NotionRequestSemantics.IdempotentRead
            ? Math.Max(0, _options.MaxRetries)
            : 0;

        for (var attempt = 0; ; attempt++)
        {
            await MaybeThrottleAsync(cancellationToken);
            await MaybeRequestDelayAsync(cancellationToken);
            Interlocked.Increment(ref _requestCount);

            using var attemptRequest = bufferedRequest.CreateRequest();
            ApplyNotionHeaders(attemptRequest);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(
                    attemptRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw new NotionApiException(
                    NotionApiErrorKind.Transport,
                    "Notion request failed due to a transport error.",
                    attempts: attempt + 1,
                    rootErrorType: exception.GetBaseException().GetType().FullName);
            }
            catch (HttpRequestException exception)
            {
                throw new NotionApiException(
                    NotionApiErrorKind.Transport,
                    "Notion request failed due to a transport error.",
                    attempts: attempt + 1,
                    rootErrorType: exception.GetBaseException().GetType().FullName);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt >= maxRetries)
                    {
                        throw new NotionApiException(
                            NotionApiErrorKind.RateLimited,
                            $"Notion request rate limited: 429 Too Many Requests (attempts: {attempt + 1}).",
                            response.StatusCode,
                            response.ReasonPhrase,
                            attempt + 1);
                    }

                    var retryDelayMs = GetRetryDelayMs(response, attempt);
                    if (retryDelayMs > 0)
                    {
                        await _delayAsync(retryDelayMs, cancellationToken);
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new NotionApiException(
                        NotionApiErrorKind.HttpStatus,
                        $"Notion request failed: {(int)response.StatusCode} {response.ReasonPhrase}",
                        response.StatusCode,
                        response.ReasonPhrase,
                        attempt + 1);
                }

                try
                {
                    return JsonDocument.Parse(body);
                }
                catch (JsonException exception)
                {
                    throw new NotionApiException(
                        NotionApiErrorKind.InvalidJson,
                        "Notion returned invalid json.",
                        response.StatusCode,
                        response.ReasonPhrase,
                        attempt + 1,
                        exception.GetType().FullName);
                }
            }
        }
    }

    public NotionClientStats GetStats()
        => new(
            Interlocked.Read(ref _requestCount),
            Interlocked.Read(ref _throttleWaitCount),
            Interlocked.Read(ref _throttleWaitTotalMs));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient(NotionClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        HttpMessageHandler? handler;
        if (options.HttpHandlerFactory is not null)
        {
            handler = options.HttpHandlerFactory();
        }
        else
        {
            handler = CreateDefaultHandler();
        }

        try
        {
            ConfigureNoRedirect(handler);
            return CreateOwnedHttpClient(options, handler!);
        }
        catch
        {
            if (handler is not null)
            {
                DisposeOwnedHandlerGraph(handler);
            }

            throw;
        }
    }

    private static HttpClient CreateHttpClient(NotionClientOptions options, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        ConfigureNoRedirect(handler);

        return CreateOwnedHttpClient(options, handler);
    }

    private static HttpClient CreateTrustedSingleHopHttpClient(
        NotionClientOptions options,
        HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        ValidateOptions(options);

        return CreateOwnedHttpClient(options, handler);
    }

    private static HttpClient CreateOwnedHttpClient(
        NotionClientOptions options,
        HttpMessageHandler handler)
    {
        var timeout = options.Timeout > TimeSpan.Zero ? options.Timeout : Timeout.InfiniteTimeSpan;
        if (timeout > MaxHttpClientTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Notion client timeout exceeds the maximum supported HttpClient timeout.");
        }

        return new HttpClient(handler, disposeHandler: true) { Timeout = timeout };
    }

    private static void DisposeOwnedHandlerGraph(HttpMessageHandler handler)
    {
        var handlers = new List<HttpMessageHandler>();
        var visited = new HashSet<HttpMessageHandler>(ReferenceEqualityComparer.Instance);
        var current = handler;
        while (visited.Add(current))
        {
            handlers.Add(current);
            if (current is not DelegatingHandler delegatingHandler ||
                delegatingHandler.InnerHandler is not { } innerHandler)
            {
                break;
            }

            current = innerHandler;
        }

        using var detachedHandler = new DetachedHandler();
        foreach (var delegatingHandler in handlers.OfType<DelegatingHandler>())
        {
            delegatingHandler.InnerHandler = detachedHandler;
        }

        foreach (var graphHandler in handlers)
        {
            graphHandler.Dispose();
        }
    }

    private static void ConfigureNoRedirect(HttpMessageHandler? handler)
    {
        if (handler is null)
        {
            throw new NotSupportedException(UnsupportedHandlerMessage);
        }

        var visited = new HashSet<HttpMessageHandler>(ReferenceEqualityComparer.Instance);
        var current = handler;
        while (visited.Add(current))
        {
            if (current is DelegatingHandler delegatingHandler)
            {
                current = delegatingHandler.InnerHandler
                    ?? throw new NotSupportedException(UnsupportedHandlerMessage);
                continue;
            }

            if (current is SocketsHttpHandler socketsHandler)
            {
                socketsHandler.AllowAutoRedirect = false;
                return;
            }

            if (current is HttpClientHandler clientHandler)
            {
                clientHandler.AllowAutoRedirect = false;
                return;
            }

            throw new NotSupportedException(UnsupportedHandlerMessage);
        }

        throw new NotSupportedException(UnsupportedHandlerMessage);
    }

    internal static HttpMessageHandler CreateDefaultHandler()
        => new SocketsHttpHandler { AllowAutoRedirect = false };

    private sealed class DetachedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private static void ValidateRequestTarget(HttpRequestMessage request)
    {
        var requestUri = request.RequestUri!;
        if (!requestUri.IsAbsoluteUri ||
            !string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(requestUri.Host, "api.notion.com", StringComparison.OrdinalIgnoreCase) ||
            requestUri.Port != 443)
        {
            throw new ArgumentException(
                "Notion request URI must be an absolute HTTPS URL for api.notion.com on port 443.",
                nameof(requestUri));
        }

        var explicitHost = request.Headers.Host;
        if (!string.IsNullOrWhiteSpace(explicitHost) &&
            (!Uri.TryCreate($"https://{explicitHost}/", UriKind.Absolute, out var hostUri) ||
             !string.Equals(hostUri.Host, "api.notion.com", StringComparison.OrdinalIgnoreCase) ||
             hostUri.Port != 443))
        {
            throw new ArgumentException(
                "Notion request Host header must match api.notion.com on port 443.",
                nameof(request));
        }
    }

    private void ApplyNotionHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        request.Headers.Remove("Notion-Version");
        request.Headers.TryAddWithoutValidation("Notion-Version", _options.ApiVersion);
        if (!request.Headers.Accept.Any(static value =>
                string.Equals(value.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    private async Task MaybeThrottleAsync(CancellationToken cancellationToken)
    {
        var maxRps = _options.MaxRps;
        if (maxRps is null || maxRps.Value <= 0)
        {
            return;
        }

        var now = _utcNow();
        TimeSpan delay;
        lock (_throttleLock)
        {
            var scheduled = _nextPermitAt > now ? _nextPermitAt : now;
            _nextPermitAt = scheduled + TimeSpan.FromSeconds(1d / maxRps.Value);
            delay = scheduled - now;
        }

        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        var milliseconds = (int)Math.Ceiling(delay.TotalMilliseconds);
        Interlocked.Increment(ref _throttleWaitCount);
        Interlocked.Add(ref _throttleWaitTotalMs, milliseconds);
        await _delayAsync(milliseconds, cancellationToken);
    }

    private Task MaybeRequestDelayAsync(CancellationToken cancellationToken)
        => _options.RequestDelayMs > 0
            ? _delayAsync(_options.RequestDelayMs, cancellationToken)
            : Task.CompletedTask;

    private int GetRetryDelayMs(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is not null)
        {
            return ClampDelay((int)Math.Ceiling(retryAfter.Delta.Value.TotalMilliseconds));
        }

        if (retryAfter?.Date is not null)
        {
            return ClampDelay((int)Math.Ceiling((retryAfter.Date.Value - _utcNow()).TotalMilliseconds));
        }

        if (response.Headers.TryGetValues("Retry-After", out var values) &&
            int.TryParse(values.FirstOrDefault(), out var seconds))
        {
            return ClampDelay(seconds * 1000);
        }

        var fallback = attempt >= 10
            ? MaxRetryDelayMs
            : BaseRetryDelayMs * (1 << attempt);
        return ClampDelay(fallback);
    }

    private static int ClampDelay(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            return 0;
        }

        return Math.Min(milliseconds, MaxRetryDelayMs);
    }

    private sealed record BufferedRequest(
        HttpMethod Method,
        Uri RequestUri,
        Version Version,
        HttpVersionPolicy VersionPolicy,
        IReadOnlyList<KeyValuePair<string, string[]>> Headers,
        byte[]? Content,
        IReadOnlyList<KeyValuePair<string, string[]>> ContentHeaders)
    {
        internal static async Task<BufferedRequest> CreateAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = request.Content is null
                ? null
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentHeaders = request.Content?.Headers
                .Select(static header => new KeyValuePair<string, string[]>(header.Key, [.. header.Value]))
                .ToArray() ?? [];
            KeyValuePair<string, string[]>[] headers =
            [
                .. request.Headers.Select(static header =>
                    new KeyValuePair<string, string[]>(header.Key, [.. header.Value]))
            ];

            return new BufferedRequest(
                request.Method,
                request.RequestUri!,
                request.Version,
                request.VersionPolicy,
                headers,
                content,
                contentHeaders);
        }

        internal HttpRequestMessage CreateRequest()
        {
            var request = new HttpRequestMessage(Method, RequestUri)
            {
                Version = Version,
                VersionPolicy = VersionPolicy
            };
            foreach (var header in Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (Content is not null)
            {
                request.Content = new ByteArrayContent(Content);
                foreach (var header in ContentHeaders)
                {
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return request;
        }
    }
}
