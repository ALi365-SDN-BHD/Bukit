using System.Text;
using System.Text.Json;
using Bukit.Notion;
using Bukit.Notion.Transport;
using Bukit.Shared;

namespace Bukit.Content.Notion;

internal sealed class NotionContentClient : IDisposable
{
    private readonly NotionClient _client;

    internal NotionContentClient(NotionContentSourceOptions options)
        : this(new NotionClient(MapOptions(options)))
    {
    }

    internal NotionContentClient(NotionClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    internal NotionContentClient(
        NotionContentSourceOptions options,
        HttpClient httpClient,
        Func<int, CancellationToken, Task> delayAsync)
        : this(new NotionClient(
            MapOptions(options),
            httpClient,
            delayAsync,
            static () => DateTimeOffset.UtcNow,
            ownsHttpClient: false))
    {
    }

    internal NotionClient Transport => _client;

    internal async Task<JsonDocument> PostAsync(
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

    internal async Task<JsonDocument> GetAsync(string url, CancellationToken cancellationToken)
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

    internal NotionClientStats GetStats() => _client.GetStats();

    public void Dispose() => _client.Dispose();

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

    private static NotionClientOptions MapOptions(NotionContentSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new NotionClientOptions
        {
            Token = options.Token,
            ApiVersion = NotionApiUrls.NotionVersion,
            RequestDelayMs = options.RequestDelayMs,
            MaxRetries = options.MaxRetries,
            MaxRps = options.MaxRps
        };
    }
}
