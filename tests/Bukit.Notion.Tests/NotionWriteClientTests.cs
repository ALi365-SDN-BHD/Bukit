#pragma warning disable CS0618 // Test-only use of obsolete injected-HttpClient constructor
using System.Net;
using Bukit.Notion.Transport;
using Bukit.Notion.Write;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionWriteClientTests
{
    [Fact]
    public async Task Operations_SendExactWireContracts()
    {
        var requests = new List<RequestSnapshot>();
        var handler = new RecordingHandler(async request =>
        {
            requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.Parameter,
                request.Headers.GetValues("Notion-Version").Single(),
                request.Content?.Headers.ContentType?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync()));
            return Json("{}");
        });
        using var transport = CanonicalBlockRendererTestSupport.CreateClient(
            new NotionClientOptions { Token = "wire-token", MaxRetries = 0 },
            handler);
        var client = new NotionWriteClient(transport);

        await client.QueryDatabaseAsync("db", "{\"query\":1}");
        await client.InspectDatabaseSchemaAsync("db");
        await client.CreateDatabaseAsync("{\"database\":1}");
        await client.CreatePageAsync("{\"create\":1}");
        await client.UpdatePageAsync("page", "{\"update\":1}");
        await client.AppendBlockChildrenAsync("page", "{\"children\":[]}");
        await client.ListBlockChildrenAsync("page", "cursor/value");
        await client.ArchiveBlockAsync("block");

        Assert.Equal(
        [
            new(HttpMethod.Post, "https://api.notion.com/v1/databases/db/query", "wire-token", NotionApiUrls.NotionVersion, "application/json", "{\"query\":1}"),
            new(HttpMethod.Get, "https://api.notion.com/v1/databases/db", "wire-token", NotionApiUrls.NotionVersion, null, null),
            new(HttpMethod.Post, "https://api.notion.com/v1/databases", "wire-token", NotionApiUrls.NotionVersion, "application/json; charset=utf-8", "{\"database\":1}"),
            new(HttpMethod.Post, "https://api.notion.com/v1/pages", "wire-token", NotionApiUrls.NotionVersion, "application/json", "{\"create\":1}"),
            new(HttpMethod.Patch, "https://api.notion.com/v1/pages/page", "wire-token", NotionApiUrls.NotionVersion, "application/json", "{\"update\":1}"),
            new(HttpMethod.Patch, "https://api.notion.com/v1/blocks/page/children", "wire-token", NotionApiUrls.NotionVersion, "application/json", "{\"children\":[]}"),
            new(HttpMethod.Get, "https://api.notion.com/v1/blocks/page/children?page_size=100&start_cursor=cursor%2Fvalue", "wire-token", NotionApiUrls.NotionVersion, null, null),
            new(HttpMethod.Delete, "https://api.notion.com/v1/blocks/block", "wire-token", NotionApiUrls.NotionVersion, null, null)
        ],
        requests);
    }

    [Fact]
    public async Task QueryDatabaseAsync_Retries429AsIdempotentRead()
    {
        var handler = new SequenceHandler(
            Json("{}", HttpStatusCode.TooManyRequests),
            Json("{\"results\":[]}"));
        using var transport = CanonicalBlockRendererTestSupport.CreateClient(
            new NotionClientOptions { Token = "token", MaxRetries = 1 },
            handler);
        var client = new NotionWriteClient(transport);

        var result = await client.QueryDatabaseAsync("db", "{}");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Mutations_DoNotReplay429()
    {
        var handler = new RepeatingHandler(HttpStatusCode.TooManyRequests);
        using var transport = CanonicalBlockRendererTestSupport.CreateClient(
            new NotionClientOptions { Token = "token", MaxRetries = 5 },
            handler);
        var client = new NotionWriteClient(transport);

        Assert.False((await client.CreateDatabaseAsync("{}")).IsSuccess);
        Assert.False((await client.CreatePageAsync("{}")).IsSuccess);
        Assert.False((await client.UpdatePageAsync("page", "{}")).IsSuccess);
        Assert.False((await client.AppendBlockChildrenAsync("page", "{}")).IsSuccess);
        Assert.False((await client.ArchiveBlockAsync("block")).IsSuccess);

        Assert.Equal(5, handler.RequestCount);
    }

    [Fact]
    public async Task MutationTransportFailure_IsSingleAttemptAndDoesNotExposeSecrets()
    {
        const string secret = "secret-response-detail";
        var handler = new ThrowingHandler(new HttpRequestException(secret));
        using var transport = CanonicalBlockRendererTestSupport.CreateClient(
            new NotionClientOptions { Token = "token", MaxRetries = 5 },
            handler);
        var client = new NotionWriteClient(transport);

        var result = await client.CreatePageAsync("{}");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain(secret, result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpFailure_DoesNotExposeResponseBody()
    {
        const string secret = "secret-response-body";
        var handler = new SequenceHandler(Json($"{{\"message\":\"{secret}\"}}", HttpStatusCode.BadRequest));
        using var transport = CanonicalBlockRendererTestSupport.CreateClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler);
        var client = new NotionWriteClient(transport);

        var result = await client.CreatePageAsync("{}");

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(secret, result.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesUnchanged()
    {
        var handler = new CancelingHandler();
        using var transport = CanonicalBlockRendererTestSupport.CreateClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler);
        var client = new NotionWriteClient(transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CreatePageAsync("{}", cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static HttpResponseMessage Json(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = new StringContent(body) };

    private sealed record RequestSnapshot(
        HttpMethod Method,
        string Url,
        string? Token,
        string Version,
        string? ContentType,
        string? Body);

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => callback(request);
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class RepeatingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(Json("{}", statusCode));
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromException<HttpResponseMessage>(exception);
        }
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }
}
