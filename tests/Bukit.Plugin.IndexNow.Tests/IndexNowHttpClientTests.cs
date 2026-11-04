using System.Net;
using Bukit.IndexNow;
using Xunit;

namespace Bukit.Plugin.IndexNow.Tests;

public sealed class IndexNowHttpClientTests
{
    [Fact]
    public void CreateDefaultHandler_DisablesAutomaticRedirects()
    {
        using var handler = IndexNowHttpClient.CreateDefaultHandler();

        Assert.False(handler.AllowAutoRedirect);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task GetPageAsync_ReturnsRedirectStatusWithoutFollowingIt(HttpStatusCode status)
    {
        var url = new Uri("https://silushangxun.com/page/");
        using var http = new HttpClient(new StubHandler(_ => Response(status, url)));
        var client = new IndexNowHttpClient(http);

        var response = await client.GetPageAsync(url, CancellationToken.None);

        Assert.Equal((int)status, response.StatusCode);
        Assert.Null(response.CanonicalUrl);
    }

    [Fact]
    public async Task GetPageAsync_RejectsResponseFromADifferentRequestUri()
    {
        var url = new Uri("https://silushangxun.com/page/");
        var redirected = new Uri("https://other.example/page/");
        using var http = new HttpClient(new StubHandler(_ => Response(HttpStatusCode.OK, redirected)));
        var client = new IndexNowHttpClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetPageAsync(url, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task SubmitAsync_ReturnsRedirectStatusWithoutTreatingItAsReceived(HttpStatusCode status)
    {
        var endpoint = new Uri("https://api.indexnow.org/indexnow");
        using var http = new HttpClient(new StubHandler(_ => Response(status, endpoint)));
        var client = new IndexNowHttpClient(http);

        var response = await client.SubmitAsync(
            new IndexNowSubmissionPayload(
                "silushangxun.com",
                "key",
                "https://silushangxun.com/key.txt",
                ["https://silushangxun.com/page/"]),
            CancellationToken.None);

        Assert.Equal((int)status, response.StatusCode);
    }

    [Fact]
    public async Task SubmitAsync_RejectsResponseFromADifferentRequestUri()
    {
        var redirected = new Uri("https://other.example/indexnow");
        using var http = new HttpClient(new StubHandler(_ => Response(HttpStatusCode.OK, redirected)));
        var client = new IndexNowHttpClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SubmitAsync(
                new IndexNowSubmissionPayload(
                    "silushangxun.com",
                    "key",
                    "https://silushangxun.com/key.txt",
                    ["https://silushangxun.com/page/"]),
                CancellationToken.None));
    }

    private static HttpResponseMessage Response(HttpStatusCode status, Uri requestUri)
        => new(status)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri)
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(request));
        }
    }
}
