using System.Text;
using System.Text.Json;
using Bukit.Notion.Transport;
using Bukit.Shared;

namespace Bukit.Content.Notion;

public sealed record NotionClientStats(long RequestCount, long ThrottleWaitCount, long ThrottleWaitTotalMs);

public sealed class NotionApiClient : IDisposable
{
    private readonly NotionClient _client;

    public NotionApiClient(NotionProviderOptions options)
    {
        _client = new NotionClient(MapOptions(options));
    }

    internal NotionApiClient(
        NotionProviderOptions options,
        HttpClient http,
        Func<int, CancellationToken, Task> delayAsync)
        : this(options, http, delayAsync, static () => DateTimeOffset.UtcNow)
    {
    }

    internal NotionApiClient(
        NotionProviderOptions options,
        HttpClient http,
        Func<int, CancellationToken, Task> delayAsync,
        Func<DateTimeOffset> utcNow)
    {
        _client = new NotionClient(
            MapOptions(options),
            http,
            delayAsync,
            utcNow,
            ownsHttpClient: false);
    }

    public async Task<JsonDocument> PostAsync(
        string url,
        string json,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return await SendAsync(request, cancellationToken);
    }

    public async Task<JsonDocument> GetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetAsync(url, cancellationToken);
        }
        catch (NotionApiException exception)
        {
            throw new ContentException(exception.Message, exception);
        }
    }

    internal NotionClientStats GetStats()
    {
        var stats = _client.GetStats();
        return new NotionClientStats(
            stats.RequestCount,
            stats.ThrottleWaitCount,
            stats.ThrottleWaitTotalMs);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<JsonDocument> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client.SendAsync(
                request,
                NotionRequestSemantics.IdempotentRead,
                cancellationToken);
        }
        catch (NotionApiException exception)
        {
            throw new ContentException(exception.Message, exception);
        }
    }

    private static NotionClientOptions MapOptions(NotionProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new NotionClientOptions
        {
            Token = options.Token,
            ApiVersion = Bukit.Notion.NotionApiUrls.NotionVersion,
            RequestDelayMs = options.RequestDelayMs,
            MaxRetries = options.MaxRetries,
            MaxRps = options.MaxRps
        };
    }
}
