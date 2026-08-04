using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Notion.Transport;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionApiClientTests
{
    [Fact]
    public async Task Constructor_WithArbitraryHttpClient_FailsClosedBeforeSending()
    {
        var handler = new SequenceHandler(new Queue<HttpResponseMessage>());
        using var http = new HttpClient(handler);
        using var client = new NotionApiClient(
            new NotionProviderOptions
            {
                DatabaseId = "db",
                Token = "token",
                MaxRetries = 0
            },
            http,
            static (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetAsync(
            "https://api.notion.com/v1/databases/db",
            CancellationToken.None));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HandlerSeam_WhenResponseIsRedirect_DoesNotFollowRedirect()
    {
        var redirect = new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        redirect.Headers.Location = new Uri("https://example.com/steal");
        var handler = new SequenceHandler(new Queue<HttpResponseMessage>([redirect]));
        using var client = CreateHandlerClient(handler);

        await Assert.ThrowsAsync<ContentException>(() => client.GetAsync(
            "https://api.notion.com/v1/databases/db",
            CancellationToken.None));

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("http://api.notion.com/v1/databases/db")]
    [InlineData("https://example.com/v1/databases/db")]
    public async Task HandlerSeam_WhenUriIsNonCanonical_RejectsBeforeHandler(string url)
    {
        var handler = new SequenceHandler(new Queue<HttpResponseMessage>());
        using var client = CreateHandlerClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAsync(url, CancellationToken.None));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HandlerSeam_WhenHostHeaderMismatches_RejectsBeforeHandler()
    {
        var handler = new SequenceHandler(new Queue<HttpResponseMessage>());
        using var client = CreateHandlerClient(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.notion.com/v1/databases/db");
        request.Headers.Host = "example.com";

        await Assert.ThrowsAsync<ArgumentException>(() => client.Transport.SendAsync(
            request,
            NotionRequestSemantics.IdempotentRead,
            CancellationToken.None));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HandlerSeam_WhenUriIsCanonical_SendsAuthorizedRequest()
    {
        var handler = new CaptureHandler();
        using var client = CreateHandlerClient(handler);

        using var document = await client.PostAsync(
            "https://api.notion.com/v1/databases/db/query",
            "{\"page_size\":1}",
            CancellationToken.None);

        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("token", handler.AuthorizationParameter);
        Assert.Equal("{\"page_size\":1}", handler.Body);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task GetAsync_WhenMaxRpsIsOne_ThrottlesSecondRequest()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });

        var handler = new SequenceHandler(responses);

        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var delays = new List<int>();

        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRps = 1
        };

        using var client = new NotionApiClient(options, handler, (ms, _) =>
        {
            delays.Add(ms);
            now = now.AddMilliseconds(ms);
            return Task.CompletedTask;
        }, () => now);

        using var doc1 = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);
        using var doc2 = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);

        Bukit.Notion.Transport.NotionClientStats stats = client.GetStats();
        Assert.True(doc1.RootElement.TryGetProperty("ok", out var ok1) && ok1.ValueKind == JsonValueKind.True);
        Assert.True(doc2.RootElement.TryGetProperty("ok", out var ok2) && ok2.ValueKind == JsonValueKind.True);
        Assert.Equal(2, handler.RequestCount);
        Assert.Single(delays);
        Assert.True(delays[0] >= 900 && delays[0] <= 1100);
        Assert.Equal(2, stats.RequestCount);
        Assert.Equal(1, stats.ThrottleWaitCount);
        Assert.True(stats.ThrottleWaitTotalMs >= 900 && stats.ThrottleWaitTotalMs <= 1100);
    }

    [Fact]
    public async Task GetAsync_When429Then200_RetriesAndRespectsRetryAfter()
    {
        var responses = new Queue<HttpResponseMessage>();

        var r1 = new HttpResponseMessage((HttpStatusCode)429);
        r1.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
        r1.Content = new StringContent("{\"code\":\"rate_limited\"}", Encoding.UTF8, "application/json");
        responses.Enqueue(r1);

        var r2 = new HttpResponseMessage(HttpStatusCode.OK);
        r2.Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json");
        responses.Enqueue(r2);

        var handler = new SequenceHandler(responses);

        var delays = new List<int>();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };

        using var client = new NotionApiClient(options, handler, (ms, _) =>
        {
            delays.Add(ms);
            return Task.CompletedTask;
        });

        using var doc = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);
        Bukit.Notion.Transport.NotionClientStats stats = client.GetStats();

        Assert.True(doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
        Assert.Equal(2, handler.RequestCount);
        Assert.Single(delays);
        Assert.Equal(2000, delays[0]);
        Assert.Equal(2, stats.RequestCount);
        Assert.Equal(0, stats.ThrottleWaitCount);
        Assert.Equal(0, stats.ThrottleWaitTotalMs);
    }

    [Fact]
    public async Task GetAsync_WhenAlways429_ThrowsAfterMaxRetries()
    {
        var responses = new Queue<HttpResponseMessage>();
        for (var i = 0; i < 5; i++)
        {
            var r = new HttpResponseMessage((HttpStatusCode)429);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(0));
            r.Content = new StringContent("{\"code\":\"rate_limited\"}", Encoding.UTF8, "application/json");
            responses.Enqueue(r);
        }

        var handler = new SequenceHandler(responses);

        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 2
        };

        using var client = new NotionApiClient(options, handler, (_, _) => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<ContentException>(() =>
            client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None));
        Assert.Contains("rate limited", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, handler.RequestCount);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(Queue<HttpResponseMessage> responses)
        {
            _responses = responses;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private static NotionApiClient CreateHandlerClient(HttpMessageHandler handler)
        => new(
            new NotionProviderOptions
            {
                DatabaseId = "db",
                Token = "token",
                RequestDelayMs = 0,
                MaxRetries = 0
            },
            handler,
            static (_, _) => Task.CompletedTask);

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        }
    }
}
