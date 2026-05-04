using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bukit.Shared;

namespace Bukit.Content.Notion;

public sealed record NotionClientStats(long RequestCount, long ThrottleWaitCount, long ThrottleWaitTotalMs);

public sealed class NotionApiClient : IDisposable
{
    private const int MaxRetryDelayMs = 60_000;
    private const int BaseRetryDelayMs = 1_000;
    private readonly HttpClient _http;
    private readonly NotionProviderOptions _options;
    private readonly Func<int, CancellationToken, Task> _delayAsync;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _throttleLock = new();
    private DateTimeOffset _nextPermitAt = DateTimeOffset.MinValue;
    private long _requestCount;
    private long _throttleWaitCount;
    private long _throttleWaitTotalMs;

    public NotionApiClient(NotionProviderOptions options)
    {
        _options = options;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _delayAsync = static (ms, ct) => Task.Delay(ms, ct);
        _utcNow = static () => DateTimeOffset.UtcNow;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        _http.DefaultRequestHeaders.Add("Notion-Version", NotionApiUrls.NotionVersion);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    internal NotionApiClient(NotionProviderOptions options, HttpClient http, Func<int, CancellationToken, Task> delayAsync)
        : this(options, http, delayAsync, static () => DateTimeOffset.UtcNow)
    {
    }

    internal NotionApiClient(NotionProviderOptions options, HttpClient http, Func<int, CancellationToken, Task> delayAsync, Func<DateTimeOffset> utcNow)
    {
        _options = options;
        _http = http;
        _delayAsync = delayAsync;
        _utcNow = utcNow;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        _http.DefaultRequestHeaders.Add("Notion-Version", NotionApiUrls.NotionVersion);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<JsonDocument> PostAsync(string url, string json, CancellationToken cancellationToken)
    {
        return await SendWithRetryAsync(async ct =>
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _http.PostAsync(url, content, ct);
        }, cancellationToken);
    }

    public async Task<JsonDocument> GetAsync(string url, CancellationToken cancellationToken)
    {
        return await SendWithRetryAsync(ct => _http.GetAsync(url, ct), cancellationToken);
    }

    internal NotionClientStats GetStats()
    {
        return new NotionClientStats(
            RequestCount: Interlocked.Read(ref _requestCount),
            ThrottleWaitCount: Interlocked.Read(ref _throttleWaitCount),
            ThrottleWaitTotalMs: Interlocked.Read(ref _throttleWaitTotalMs));
    }

    private async Task<JsonDocument> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken)
    {
        var maxRetries = _options.MaxRetries < 0 ? 0 : _options.MaxRetries;
        for (var attempt = 0; ; attempt++)
        {
            await MaybeThrottleAsync(cancellationToken);
            await MaybeDelayAsync(cancellationToken);
            Interlocked.Increment(ref _requestCount);
            using var response = await sendAsync(cancellationToken);

            if ((int)response.StatusCode != 429)
            {
                return await ReadJsonAsync(response, cancellationToken);
            }

            if (attempt >= maxRetries)
            {
                throw new ContentException($"Notion request rate limited: 429 Too Many Requests (attempts: {attempt + 1}).");
            }

            var delayMs = GetRetryDelayMs(response, attempt);
            if (delayMs > 0)
            {
                await _delayAsync(delayMs, cancellationToken);
            }
        }
    }

    private Task MaybeThrottleAsync(CancellationToken cancellationToken)
    {
        var maxRps = _options.MaxRps;
        if (maxRps is null || maxRps.Value <= 0)
        {
            return Task.CompletedTask;
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
            return Task.CompletedTask;
        }

        var ms = (int)Math.Ceiling(delay.TotalMilliseconds);
        Interlocked.Increment(ref _throttleWaitCount);
        Interlocked.Add(ref _throttleWaitTotalMs, ms);
        return _delayAsync(ms, cancellationToken);
    }

    private static int GetRetryDelayMs(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is not null)
        {
            var ms = (int)Math.Ceiling(retryAfter.Delta.Value.TotalMilliseconds);
            return ClampDelay(ms);
        }

        if (retryAfter?.Date is not null)
        {
            var ms = (int)Math.Ceiling((retryAfter.Date.Value - DateTimeOffset.UtcNow).TotalMilliseconds);
            return ClampDelay(ms);
        }

        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, out var seconds))
            {
                return ClampDelay(seconds * 1000);
            }
        }

        var fallback = attempt >= 10 ? MaxRetryDelayMs : BaseRetryDelayMs * (1 << attempt);
        return ClampDelay(fallback);
    }

    private static int ClampDelay(int ms)
    {
        if (ms <= 0)
        {
            return 0;
        }

        return ms > MaxRetryDelayMs ? MaxRetryDelayMs : ms;
    }

    private async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ContentException($"Notion request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (Exception ex)
        {
            throw new ContentException("Notion returned invalid json.", ex);
        }
    }

    private Task MaybeDelayAsync(CancellationToken cancellationToken)
    {
        if (_options.RequestDelayMs <= 0)
        {
            return Task.CompletedTask;
        }

        return _delayAsync(_options.RequestDelayMs, cancellationToken);
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
