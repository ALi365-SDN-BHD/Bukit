using System.Net;
using System.Text;
using Bukit.Notion.Client;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionHttpClientTests
{
    [Fact]
    public async Task CreatePageAsync_SetsAuthorizationAndNotionVersionHeaders()
    {
        using var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.OK, """{"id":"page-1"}"""));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.notion.com") };
        var client = new NotionHttpClient(httpClient, new NotionRequestOptions("secret-token", "2026-03-11"));

        await client.CreatePageAsync(new NotionCreatePageRequest("""{"parent":{"data_source_id":"ds"}}"""), CancellationToken.None);

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/pages", request.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization.Parameter);
        Assert.Equal("2026-03-11", Assert.Single(request.Headers.GetValues("Notion-Version")));
    }

    [Fact]
    public async Task CreatePageAsync_MapsUnauthorizedWithoutLeakingToken()
    {
        using var handler = new RecordingHandler(_ => JsonResponse(
            HttpStatusCode.Unauthorized,
            """{"object":"error","code":"unauthorized","message":"secret-token is bad"}"""));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.notion.com") };
        var client = new NotionHttpClient(httpClient, new NotionRequestOptions("secret-token", "2026-03-11"));

        NotionApiException ex = await Assert.ThrowsAsync<NotionApiException>(() =>
            client.CreatePageAsync(new NotionCreatePageRequest("""{"parent":{"data_source_id":"ds"}}"""), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("unauthorized", ex.Code);
        Assert.DoesNotContain("secret-token", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePageAsync_RetriesRateLimitOnce()
    {
        int calls = 0;
        using var handler = new RecordingHandler(_ =>
        {
            calls++;
            return calls == 1
                ? JsonResponse((HttpStatusCode)429, """{"object":"error","code":"rate_limited","message":"slow down"}""", retryAfterSeconds: 0)
                : JsonResponse(HttpStatusCode.OK, """{"id":"page-1"}""");
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.notion.com") };
        var client = new NotionHttpClient(httpClient, new NotionRequestOptions("secret-token", "2026-03-11", MaxRetries: 1));

        NotionPageResult result = await client.CreatePageAsync(new NotionCreatePageRequest("""{"parent":{"data_source_id":"ds"}}"""), CancellationToken.None);

        Assert.Equal("page-1", result.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task QueryDataSourceAsync_FollowsPagination()
    {
        int calls = 0;
        using var handler = new RecordingHandler(request =>
        {
            calls++;
            return calls == 1
                ? JsonResponse(HttpStatusCode.OK, """{"results":[{"id":"a"}],"has_more":true,"next_cursor":"cursor-1"}""")
                : JsonResponse(HttpStatusCode.OK, """{"results":[{"id":"b"}],"has_more":false,"next_cursor":null}""");
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.notion.com") };
        var client = new NotionHttpClient(httpClient, new NotionRequestOptions("secret-token", "2026-03-11"));

        NotionQueryResult result = await client.QueryDataSourceAsync("ds", new NotionQueryRequest("""{"page_size":1}"""), CancellationToken.None);

        Assert.Equal(["a", "b"], result.ResultIds);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task CreatePageAsync_MapsApiErrors()
    {
        using var handler = new RecordingHandler(_ => JsonResponse(
            HttpStatusCode.Conflict,
            """{"object":"error","code":"conflict_error","message":"conflict"}"""));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.notion.com") };
        var client = new NotionHttpClient(httpClient, new NotionRequestOptions("secret-token", "2026-03-11"));

        NotionApiException ex = await Assert.ThrowsAsync<NotionApiException>(() =>
            client.CreatePageAsync(new NotionCreatePageRequest("""{"parent":{"data_source_id":"ds"}}"""), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("conflict_error", ex.Code);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (retryAfterSeconds is not null)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds.Value));
        }

        return response;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_handler(request));
        }
    }
}
