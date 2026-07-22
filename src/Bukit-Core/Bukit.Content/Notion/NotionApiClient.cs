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
        var semantics = IsDatabaseQueryUrl(url)
            ? NotionRequestSemantics.IdempotentRead
            : NotionRequestSemantics.NonReplayableWrite;
        return await SendAsync(request, semantics, cancellationToken);
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

    internal NotionClient Transport => _client;

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<JsonDocument> SendAsync(
        HttpRequestMessage request,
        NotionRequestSemantics semantics,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client.SendAsync(
                request,
                semantics,
                cancellationToken);
        }
        catch (NotionApiException exception)
        {
            throw new ContentException(exception.Message, exception);
        }
    }

    private static bool IsDatabaseQueryUrl(string url)
    {
        var baseUri = new Uri(Bukit.Notion.NotionApiUrls.Base);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != baseUri.Port)
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 4 &&
               string.Equals(segments[0], Bukit.Notion.NotionApiUrls.ApiVersion, StringComparison.Ordinal) &&
               string.Equals(segments[1], "databases", StringComparison.Ordinal) &&
               segments[2].Length > 0 &&
               string.Equals(segments[3], "query", StringComparison.Ordinal);
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
