using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.CloudBase.Models;
using Bukit.CloudBase.Transport;
using Xunit;

namespace Bukit.CloudBase.Tests;

public sealed class CloudBaseClientTests
{
    private const string Key = "pk_test_secret";
    private const string JsonArrayOne = """[{"id":1}]""";

    [Theory]
    [InlineData("bad/env")]
    [InlineData("bad@env")]
    [InlineData("bad#env")]
    [InlineData("bad?env")]
    [InlineData("bad.env")]
    [InlineData("BAD-ENV")]
    public void Ctor_InvalidEnvId_Throws(string envId)
    {
        var options = Options(envId);
        Assert.Throws<ArgumentException>(() => _ = new CloudBaseClient(options, new StubHandler()));
    }

    [Fact]
    public async Task Ctor_IntlRegion_UsesIntlGateway()
    {
        var (client, handler, _) = Create(region: "intl");
        using var _client = client;
        handler.Enqueue(JsonArrayOne);
        await client.QueryAsync(Query());

        Assert.Equal("env-abc.api.intl.tcloudbasegateway.com", handler.Requests[0].Uri.Host);
    }

    [Fact]
    public async Task Query_SendsPostgrestRequest()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue(JsonArrayOne, contentRange: "0-0/1");

        var result = await client.QueryAsync(Query() with
        {
            Select = ["id", "title"],
            FilterField = "status",
            FilterValue = "published",
            SortField = "publish_at",
            SortDirection = "desc"
        });

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/v1/rdb/rest/articles", request.Uri.AbsolutePath);
        var query = Uri.UnescapeDataString(request.Uri.Query);
        Assert.Contains("select=id,title", query, StringComparison.Ordinal);
        Assert.Contains("status=eq.published", query, StringComparison.Ordinal);
        Assert.Contains("order=publish_at.desc,id.asc", query, StringComparison.Ordinal);
        Assert.Contains("limit=100", query, StringComparison.Ordinal);
        Assert.Contains("offset=0", query, StringComparison.Ordinal);
        Assert.Equal($"Bearer {Key}", request.Authorization);
        Assert.Equal("count=exact", request.Prefer);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Rows);
    }

    [Theory]
    [InlineData("0-99/250", 250)]
    [InlineData("*/0", 0)]
    public async Task Query_ParsesContentRange(string header, int expected)
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue(JsonArrayOne, contentRange: header);

        var result = await client.QueryAsync(Query());

        Assert.Equal(expected, result.TotalCount);
    }

    [Fact]
    public async Task Query_MissingContentRange_StopsOnShortPage()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue(JsonArrayOne);

        var result = await client.QueryAsync(Query());

        Assert.Null(result.TotalCount);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Query_MalformedContentRange_ThrowsResponseInvalid()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue(JsonArrayOne, contentRange: "nonsense");

        var ex = await Assert.ThrowsAsync<CloudBaseApiException>(() => client.QueryAsync(Query()));
        Assert.Equal(CloudBaseErrorKind.ResponseInvalid, ex.Kind);
    }

    [Fact]
    public async Task Query_PagesUntilShortPage()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue("""[{"id":1},{"id":2}]""");
        handler.Enqueue("""[{"id":3},{"id":4}]""");
        handler.Enqueue("""[{"id":5}]""");

        var result = await client.QueryAsync(Query() with { PageSize = 2 });

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(5, result.Rows.Count);
        Assert.Contains("offset=0", Uri.UnescapeDataString(handler.Requests[0].Uri.Query), StringComparison.Ordinal);
        Assert.Contains("offset=2", Uri.UnescapeDataString(handler.Requests[1].Uri.Query), StringComparison.Ordinal);
        Assert.Contains("offset=4", Uri.UnescapeDataString(handler.Requests[2].Uri.Query), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Query_StopsWhenOffsetReachesTotal()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue("""[{"id":1},{"id":2}]""", contentRange: "0-1/2");

        var result = await client.QueryAsync(Query() with { PageSize = 2 });

        Assert.Single(handler.Requests);
        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public async Task Query_EncodesFilterValue()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue(JsonArrayOne);

        await client.QueryAsync(Query() with { FilterField = "status", FilterValue = "a&b=c,d中文" });

        var raw = handler.Requests[0].Uri.Query;
        Assert.Contains("status=eq.a%26b%3Dc%2Cd%E4%B8%AD%E6%96%87", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Query_InvalidIdentifier_Throws()
    {
        var (client, handler, _) = Create();
        using var _client = client;

        await Assert.ThrowsAsync<ArgumentException>(() => client.QueryAsync(Query() with { Table = "bad;drop" }));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Sign_UsesFullSignedUrl()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        const string url = "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.jpg?token=x";
        handler.Enqueue($$"""[{"path":"a.jpg","fullSignedURL":"{{url}}"}]""");

        var result = await client.SignObjectsAsync("media", ["a.jpg"], 600);

        Assert.Equal(url, Assert.Single(result).Url);
        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/storages/object/sign/media", request.Uri.AbsolutePath);
        Assert.Contains("\"expiresIn\":600", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"paths\":[\"a.jpg\"]", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_ComposesRelativeSignedUrl()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue("""[{"path":"a.jpg","signedURL":"/v1/storages/object/sign/media/a.jpg?token=x"}]""");

        var result = await client.SignObjectsAsync("media", ["a.jpg"], 600);

        Assert.Equal(
            "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.jpg?token=x",
            Assert.Single(result).Url);
    }

    [Theory]
    [InlineData("http://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.jpg?token=x")]
    [InlineData("https://evil.example.com/v1/storages/object/sign/media/a.jpg?token=x")]
    [InlineData("https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/other/a.jpg?token=x")]
    public async Task Sign_RejectsUnsafeSignedUrl(string url)
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue($$"""[{"path":"a.jpg","fullSignedURL":"{{url}}"}]""");

        var result = Assert.Single(await client.SignObjectsAsync("media", ["a.jpg"], 600));

        Assert.Null(result.Url);
        Assert.Equal("invalid-signed-url", result.Error);
    }

    [Fact]
    public async Task Sign_PerItemError_IsReported()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue("""[{"path":"a.jpg","error":"NotFound"}]""");

        var result = Assert.Single(await client.SignObjectsAsync("media", ["a.jpg"], 600));

        Assert.Null(result.Url);
        Assert.Equal("NotFound", result.Error);
    }

    [Fact]
    public async Task Sign_BatchesAtMost500()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue(BuildSignResponse(500));
        handler.Enqueue(BuildSignResponse(1));
        var names = Enumerable.Range(0, 501).Select(index => $"p/{index}.jpg").ToArray();

        var result = await client.SignObjectsAsync("media", names, 600);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(501, result.Count);
        Assert.Equal(500, CountPaths(handler.Requests[0].Body));
        Assert.Equal(1, CountPaths(handler.Requests[1].Body));
    }

    [Fact]
    public async Task Sign_InvalidObjectNames_AreNotSent()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        string[] names = ["", "/a.jpg", "a//b.jpg", "a/../b.jpg"];

        var result = await client.SignObjectsAsync("media", names, 600);

        Assert.Empty(handler.Requests);
        Assert.Equal(4, result.Count);
        Assert.All(result, item => Assert.Equal("invalid-object-name", item.Error));
    }

    [Fact]
    public async Task Sign_InvalidBucket_Throws()
    {
        var (client, _, _) = Create();
        using var _client = client;

        await Assert.ThrowsAsync<ArgumentException>(() => client.SignObjectsAsync("Bad Bucket", ["a.jpg"], 600));
    }

    [Fact]
    public async Task Retry_On429_ThenSucceeds()
    {
        var (client, handler, delays) = Create();
        using var _client = client;
        handler.Enqueue("""{"code":"RATE"}""", HttpStatusCode.TooManyRequests);
        handler.Enqueue(JsonArrayOne);

        var result = await client.QueryAsync(Query());

        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(result.Rows);
        Assert.Contains(500, delays);
    }

    [Fact]
    public async Task Retry_Exhausted_ThrowsRequestFailed()
    {
        var (client, handler, _) = Create(maxRetries: 2);
        using var _client = client;
        handler.Enqueue("""{"code":"INTERNAL"}""", HttpStatusCode.InternalServerError);
        handler.Enqueue("""{"code":"INTERNAL"}""", HttpStatusCode.InternalServerError);
        handler.Enqueue("""{"code":"INTERNAL"}""", HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<CloudBaseApiException>(() => client.QueryAsync(Query()));

        Assert.Equal(CloudBaseErrorKind.RequestFailed, ex.Kind);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(HttpStatusCode.NotFound, "NotFound")]
    public async Task Error_Status_MapsKind(HttpStatusCode status, string expected)
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue("""{"code":"DATABASE_PGRST205","message":"missing"}""", status);

        var ex = await Assert.ThrowsAsync<CloudBaseApiException>(() => client.QueryAsync(Query()));

        Assert.Equal(expected, ex.Kind.ToString());
        Assert.Equal((int)status, ex.StatusCode);
        Assert.Equal("DATABASE_PGRST205", ex.ProviderCode);
    }

    [Fact]
    public async Task Error_Message_DoesNotLeakCredentialOrUrl()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue("""{"code":"AUTH","message":"secret detail"}""", HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<CloudBaseApiException>(() => client.QueryAsync(Query()));

        Assert.DoesNotContain(Key, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("articles", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret detail", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_MalformedJson_ThrowsResponseInvalid()
    {
        var (client, handler, _) = Create();
        using var _client = client;
        handler.Enqueue("{not json");

        var ex = await Assert.ThrowsAsync<CloudBaseApiException>(() => client.QueryAsync(Query()));
        Assert.Equal(CloudBaseErrorKind.ResponseInvalid, ex.Kind);
    }

    [Fact]
    public async Task Response_TooLarge_ThrowsResponseInvalid()
    {
        var (client, handler, _) = Create(maxResponseBytes: 5);
        using var _client = client;
        handler.Enqueue("""[{"id":1}]""");

        var ex = await Assert.ThrowsAsync<CloudBaseApiException>(() => client.QueryAsync(Query()));
        Assert.Equal(CloudBaseErrorKind.ResponseInvalid, ex.Kind);
    }

    [Fact]
    public async Task RateLimit_SpacesRequests()
    {
        var (client, handler, delays) = Create(maxRps: 2);
        using var _client = client;
        handler.Enqueue(JsonArrayOne, contentRange: "0-0/1");
        handler.Enqueue(JsonArrayOne, contentRange: "0-0/1");

        await client.QueryAsync(Query());
        await client.QueryAsync(Query());

        Assert.Contains(500, delays);
    }

    [Fact]
    public void ToString_DoesNotLeakCredentialOrSignedUrl()
    {
        var options = Options();
        Assert.DoesNotContain(Key, options.ToString(), StringComparison.Ordinal);

        var signed = new CloudBaseSignedObject(
            "a.jpg",
            "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.jpg?token=SECRET",
            null);
        var text = signed.ToString();
        Assert.DoesNotContain("token=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
        Assert.Contains("HasUrl = True", text, StringComparison.Ordinal);
    }

    private static CloudBaseQueryRequest Query() => new()
    {
        Table = "articles",
        IdField = "id"
    };

    private static CloudBaseClientOptions Options(string envId = "env-abc", string region = "cn", int maxRps = 5, int maxRetries = 3, int maxResponseBytes = 8 * 1024 * 1024)
        => new()
        {
            EnvId = envId,
            Region = region,
            PublishableKey = Key,
            MaxRps = maxRps,
            MaxRetries = maxRetries,
            MaxResponseBytes = maxResponseBytes
        };

    private static (CloudBaseClient Client, StubHandler Handler, List<int> Delays) Create(
        string envId = "env-abc",
        string region = "cn",
        int maxRps = 1000,
        int maxRetries = 3,
        int maxResponseBytes = 8 * 1024 * 1024)
    {
        var handler = new StubHandler();
        var delays = new List<int>();
        var client = new CloudBaseClient(
            Options(envId, region, maxRps, maxRetries, maxResponseBytes),
            handler,
            (milliseconds, _) =>
            {
                delays.Add(milliseconds);
                return Task.CompletedTask;
            },
            () => DateTimeOffset.UnixEpoch);
        return (client, handler, delays);
    }

    private static string BuildSignResponse(int count)
    {
        var builder = new StringBuilder("[");
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append($$"""{"path":"p/{{index}}.jpg","signedURL":"/v1/storages/object/sign/media/p/{{index}}.jpg?token=x"}""");
        }

        return builder.Append(']').ToString();
    }

    private static int CountPaths(string? body)
    {
        Assert.NotNull(body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("paths").GetArrayLength();
    }
}

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri Uri,
    string? Body,
    string? Authorization,
    string? Prefer);

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body, string? ContentRange)> _responses = new();

    internal List<CapturedRequest> Requests { get; } = [];

    internal void Enqueue(string body, HttpStatusCode status = HttpStatusCode.OK, string? contentRange = null)
        => _responses.Enqueue((status, body, contentRange));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var prefer = request.Headers.TryGetValues("Prefer", out var values) ? string.Join(",", values) : null;
        Requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri!,
            body,
            request.Headers.Authorization?.ToString(),
            prefer));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No stubbed response left.");
        }

        var (status, content, contentRange) = _responses.Dequeue();
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        if (contentRange is not null)
        {
            response.Content.Headers.TryAddWithoutValidation("Content-Range", contentRange);
        }

        return response;
    }
}
