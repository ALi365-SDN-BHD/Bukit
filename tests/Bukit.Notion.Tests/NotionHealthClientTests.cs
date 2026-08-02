#pragma warning disable CS0618 // Test-only use of obsolete injected-HttpClient constructor
using System.Net;
using System.Text;
using Bukit.Notion.Diagnostics;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionHealthClientTests
{
    [Fact]
    public async Task CheckConnectivityAsync_UsesCanonicalUsersMeRequest()
    {
        HttpRequestMessage? captured = null;
        var handler = new CallbackHandler(request =>
        {
            captured = request;
            return Response(HttpStatusCode.OK, "{}");
        });
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler);
        var client = new NotionHealthClient(transport);

        var result = await client.CheckConnectivityAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal(NotionApiUrls.UsersMe(), captured.RequestUri?.ToString());
        Assert.Equal("token", captured.Headers.Authorization?.Parameter);
        Assert.Equal(
            NotionApiUrls.NotionVersion,
            captured.Headers.GetValues("Notion-Version").Single());
    }

    [Fact]
    public async Task CheckDatabaseAsync_ReturnsStructuredHttpFailure()
    {
        var handler = new CallbackHandler(_ => Response(HttpStatusCode.Unauthorized, "{\"secret\":\"body\"}"));
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler);
        var client = new NotionHealthClient(transport);

        var result = await client.CheckDatabaseAsync("db");

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(NotionApiErrorKind.HttpStatus, result.ErrorKind);
        Assert.DoesNotContain("body", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckConnectivityAsync_TreatsSuccessfulNonJsonResponseAsReachable()
    {
        var handler = new CallbackHandler(_ => Response(HttpStatusCode.NoContent, string.Empty));
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler);
        var client = new NotionHealthClient(transport);

        var result = await client.CheckConnectivityAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
    }

    [Fact]
    public async Task InspectDatabaseSchemaAsync_ParsesPropertyNamesAndTypes()
    {
        const string response = """
            {
              "properties": {
                "Title": { "type": "title" },
                "Slug": { "type": "rich_text" }
              }
            }
            """;
        var handler = new CallbackHandler(_ => Response(HttpStatusCode.OK, response));
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler);
        var client = new NotionHealthClient(transport);

        var result = await client.InspectDatabaseSchemaAsync("db");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [new NotionDatabaseProperty("Title", "title"), new NotionDatabaseProperty("Slug", "rich_text")],
            result.Properties);
    }

    [Fact]
    public async Task CheckConnectivityAsync_DoesNotSwallowCallerCancellation()
    {
        var handler = new CancelingHandler();
        using var transport = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler);
        var client = new NotionHealthClient(transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CheckConnectivityAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body)
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class CallbackHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(callback(request));
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }
}
