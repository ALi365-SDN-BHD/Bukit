using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionApiClientTests
{
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
        using var http = new HttpClient(handler);

        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var delays = new List<int>();

        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRps = 1
        };

        using var client = new NotionApiClient(options, http, (ms, _) =>
        {
            delays.Add(ms);
            now = now.AddMilliseconds(ms);
            return Task.CompletedTask;
        }, () => now);

        using var doc1 = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);
        using var doc2 = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);

        var stats = client.GetStats();
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
        using var http = new HttpClient(handler);

        var delays = new List<int>();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };

        using var client = new NotionApiClient(options, http, (ms, _) =>
        {
            delays.Add(ms);
            return Task.CompletedTask;
        });

        using var doc = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);
        Assert.True(doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
        Assert.Equal(2, handler.RequestCount);
        Assert.Single(delays);
        Assert.Equal(2000, delays[0]);
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
        using var http = new HttpClient(handler);

        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 2
        };

        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);

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
}
