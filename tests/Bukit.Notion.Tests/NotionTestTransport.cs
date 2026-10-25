using System.Net;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

internal sealed class NotionTestTransport(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    internal Queue<HttpResponseMessage> Responses { get; } = new(responses);
    internal List<NotionRequestSnapshot> Requests { get; } = [];
    internal List<int> Delays { get; } = [];
    internal DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

    internal NotionClient CreateClient(NotionClientOptions options)
        => new(options, this, DelayAsync, () => UtcNow);

    internal static HttpResponseMessage Json(
        string body = "{}",
        HttpStatusCode status = HttpStatusCode.OK,
        string? retryAfter = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
        if (retryAfter is not null)
        {
            response.Headers.Add("Retry-After", retryAfter);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = Assert.IsType<Uri>(request.RequestUri);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("api.notion.com", uri.Host);
        Assert.Equal(443, uri.Port);

        Requests.Add(new NotionRequestSnapshot(
            request.Method,
            uri,
            request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Content?.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)));

        return Responses.TryDequeue(out var response)
            ? response
            : throw new InvalidOperationException("No queued fake Notion response remains.");
    }

    private Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(milliseconds);
        UtcNow = UtcNow.AddMilliseconds(milliseconds);
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            while (Responses.TryDequeue(out var response))
            {
                response.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}

internal sealed record NotionRequestSnapshot(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, string[]> Headers,
    string? Body,
    IReadOnlyDictionary<string, string[]> ContentHeaders);
