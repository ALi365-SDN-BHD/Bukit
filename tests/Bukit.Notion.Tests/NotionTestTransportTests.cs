using System.Net;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Bukit.Notion.Write;
using Xunit;
using static Bukit.Notion.Tests.NotionTestTransport;

namespace Bukit.Notion.Tests;

[Trait("Category", "Integration")]
public sealed class NotionTestTransportTests
{
    [Fact]
    public async Task Renderer_ConsumesTwoPagesAndEncodesCursor()
    {
        using var transport = new NotionTestTransport(
            Json("""
                {"has_more":true,"next_cursor":"cursor/value +?&","results":[
                  {"type":"paragraph","paragraph":{"rich_text":[{"plain_text":"First <page>"}]}}
                ]}
                """),
            Json("""
                {"has_more":false,"results":[
                  {"type":"paragraph","paragraph":{"rich_text":[{"plain_text":"Second page"}]}}
                ]}
                """));
        using var client = transport.CreateClient(new NotionClientOptions { Token = "test-token", MaxRetries = 0 });

        var html = await new NotionBlocksRenderer(client).RenderPageAsync("page", CancellationToken.None);

        Assert.Equal("<p>First &lt;page&gt;</p>\n<p>Second page</p>\n", html);
        Assert.Equal(
            ["https://api.notion.com/v1/blocks/page/children?page_size=100",
             "https://api.notion.com/v1/blocks/page/children?page_size=100&start_cursor=cursor%2Fvalue+%2B%3F%26"],
            transport.Requests.Select(request => request.Uri.AbsoluteUri));
        Assert.All(transport.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Empty(transport.Responses);
        Assert.Empty(transport.Delays);
    }

    [Fact]
    public async Task WriteClient_ListsTwoPagesUsingReturnedCursor()
    {
        using var transport = new NotionTestTransport(
            Json("""{"has_more":true,"next_cursor":"cursor/value +?&","results":[{"id":"first"}]}"""),
            Json("""{"has_more":false,"results":[{"id":"second"}]}"""));
        using var client = transport.CreateClient(new NotionClientOptions { Token = "test-token", MaxRetries = 0 });
        var writer = new NotionWriteClient(client);

        var first = await writer.ListBlockChildrenAsync("page");
        Assert.True(first.IsSuccess);
        Assert.True(first.Payload!.Value.GetProperty("has_more").GetBoolean());
        var second = await writer.ListBlockChildrenAsync("page", first.Payload.Value.GetProperty("next_cursor").GetString());

        Assert.True(second.IsSuccess);
        Assert.False(second.Payload!.Value.GetProperty("has_more").GetBoolean());
        Assert.Equal("first", first.Payload.Value.GetProperty("results")[0].GetProperty("id").GetString());
        Assert.Equal("second", second.Payload.Value.GetProperty("results")[0].GetProperty("id").GetString());
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal("?page_size=100&start_cursor=cursor%2Fvalue%20%2B%3F%26", transport.Requests[1].Uri.Query);
        Assert.Empty(transport.Responses);
    }

    [Fact]
    public async Task QueryDatabase_RetriesDeltaThenDateUsingAdvancingClockAndPreservesDisposedRequests()
    {
        const string payload = """{"page_size":2,"start_cursor":"cursor/value"}""";
        using var transport = new NotionTestTransport(
            Json(status: HttpStatusCode.TooManyRequests, retryAfter: "2"),
            Json(status: HttpStatusCode.TooManyRequests, retryAfter: "Sun, 25 Oct 2026 00:00:05 GMT"),
            Json("""{"results":[{"id":"recovered"}]}"""));
        var startedAt = transport.UtcNow;
        using (var client = transport.CreateClient(new NotionClientOptions { Token = "test-token", MaxRetries = 2 }))
        {
            var result = await new NotionWriteClient(client).QueryDatabaseAsync("db", payload);

            Assert.True(result.IsSuccess);
            Assert.Equal("recovered", result.Payload!.Value.GetProperty("results")[0].GetProperty("id").GetString());
            Assert.Equal(3, client.GetStats().RequestCount);
        }

        Assert.Equal([2000, 3000], transport.Delays);
        Assert.Equal(startedAt.AddSeconds(5), transport.UtcNow);
        Assert.Equal(3, transport.Requests.Count);
        Assert.All(transport.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.notion.com/v1/databases/db/query", request.Uri.AbsoluteUri);
            Assert.Equal(payload, request.Body);
            Assert.Equal(["Bearer test-token"], request.Headers["Authorization"]);
            Assert.Equal([NotionApiUrls.NotionVersion], request.Headers["Notion-Version"]);
            Assert.Equal(["application/json"], request.Headers["Accept"]);
            Assert.Equal(["application/json"], request.ContentHeaders["Content-Type"]);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task RateLimitBudget_StopsWithoutFinalDelayAndAllowsNextCall(int maxRetries)
    {
        using var transport = new NotionTestTransport();
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            transport.Responses.Enqueue(Json(status: HttpStatusCode.TooManyRequests));
        }
        transport.Responses.Enqueue(Json("""{"results":[]}"""));
        using var client = transport.CreateClient(new NotionClientOptions { Token = "test-token", MaxRetries = maxRetries });
        var writer = new NotionWriteClient(client);

        var failed = await writer.QueryDatabaseAsync("db", "{}");

        Assert.False(failed.IsSuccess);
        Assert.Equal(NotionApiErrorKind.RateLimited, failed.ErrorKind);
        Assert.Equal(HttpStatusCode.TooManyRequests, failed.StatusCode);
        Assert.Equal(maxRetries + 1, failed.Attempts);
        Assert.Equal(maxRetries + 1, transport.Requests.Count);
        Assert.Equal(maxRetries == 0 ? [] : new[] { 1000, 2000 }, transport.Delays);
        Assert.Single(transport.Responses);
        Assert.True((await writer.QueryDatabaseAsync("db", "{}")).IsSuccess);
        Assert.Equal(maxRetries + 2, transport.Requests.Count);
        Assert.Empty(transport.Responses);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ServerFailure_StopsRemainingRetryBudgetAndRecoversOnlyOnNextCall(HttpStatusCode status)
    {
        using var transport = new NotionTestTransport(
            Json(status: HttpStatusCode.TooManyRequests, retryAfter: "1"),
            Json(status: status),
            Json("""{"results":[]}"""));
        using var client = transport.CreateClient(new NotionClientOptions { Token = "test-token", MaxRetries = 3 });
        var writer = new NotionWriteClient(client);

        var failed = await writer.QueryDatabaseAsync("db", "{}");

        Assert.False(failed.IsSuccess);
        Assert.Equal(NotionApiErrorKind.HttpStatus, failed.ErrorKind);
        Assert.Equal(status, failed.StatusCode);
        Assert.Equal(2, failed.Attempts);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal([1000], transport.Delays);
        Assert.Single(transport.Responses);
        Assert.True((await writer.QueryDatabaseAsync("db", "{}")).IsSuccess);
        Assert.Equal(3, transport.Requests.Count);
        Assert.Equal([1000], transport.Delays);
        Assert.Empty(transport.Responses);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, NotionApiErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, NotionApiErrorKind.HttpStatus)]
    public async Task CreatePage_NonReplayableWriteFailsWithoutConsumingRecoveryResponse(
        HttpStatusCode status,
        NotionApiErrorKind errorKind)
    {
        const string payload = """{"parent":{"page_id":"parent"},"properties":{}}""";
        using var transport = new NotionTestTransport(Json(status: status), Json("""{"id":"duplicate"}"""));
        using var client = transport.CreateClient(new NotionClientOptions { Token = "test-token", MaxRetries = 3 });

        var result = await new NotionWriteClient(client).CreatePageAsync(payload);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorKind, result.ErrorKind);
        Assert.Equal(1, result.Attempts);
        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.notion.com/v1/pages", request.Uri.AbsoluteUri);
        Assert.Equal(payload, request.Body);
        Assert.Single(transport.Responses);
        Assert.Empty(transport.Delays);
    }

    [Theory]
    [InlineData("http://api.notion.com/v1/pages")]
    [InlineData("https://example.com/v1/pages")]
    [InlineData("https://api.notion.com:444/v1/pages")]
    public async Task InvalidTarget_IsRejectedBeforeFakeTransport(string target)
    {
        using var transport = new NotionTestTransport(Json());
        using var client = transport.CreateClient(new NotionClientOptions { Token = "test-token" });
        using var request = new HttpRequestMessage(HttpMethod.Get, target);

        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync(request, NotionRequestSemantics.IdempotentRead));

        Assert.Empty(transport.Requests);
        Assert.Single(transport.Responses);
        Assert.Null(request.Headers.Authorization);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task Redirect_FailsClosedWithoutFollowingLocation(HttpStatusCode status)
    {
        var redirect = Json(status: status);
        redirect.Headers.Location = new Uri("https://example.com/redirected");
        using var transport = new NotionTestTransport(redirect, Json());
        using var client = transport.CreateClient(new NotionClientOptions { Token = "test-token", MaxRetries = 3 });

        var result = await new NotionWriteClient(client).InspectDatabaseSchemaAsync("db");

        Assert.False(result.IsSuccess);
        Assert.Equal(NotionApiErrorKind.HttpStatus, result.ErrorKind);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(1, result.Attempts);
        Assert.Single(transport.Requests);
        Assert.Single(transport.Responses);
        Assert.Empty(transport.Delays);
    }
}
