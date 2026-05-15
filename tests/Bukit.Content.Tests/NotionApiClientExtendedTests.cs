using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionApiClientExtendedTests
{
    [Fact]
    public async Task PostAsync_SendsCorrectJsonBody()
    {
        string? capturedBody = null;
        var handler = new CaptureHandler((req, _) =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler);
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };

        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);
        using var doc = await client.PostAsync("https://api.notion.com/v1/databases/db/query", "{\"page_size\":10}", CancellationToken.None);

        Assert.NotNull(capturedBody);
        Assert.Equal("{\"page_size\":10}", capturedBody);
        Assert.True(doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
    }

    [Fact]
    public async Task GetRetryDelayMs_WithRetryAfterDateHeader_UsesDateOffset()
    {
        var futureDate = DateTimeOffset.UtcNow.AddSeconds(5);
        var responses = new Queue<HttpResponseMessage>();
        var r1 = new HttpResponseMessage((HttpStatusCode)429);
        r1.Headers.RetryAfter = new RetryConditionHeaderValue(futureDate);
        r1.Content = new StringContent("{\"code\":\"rate_limited\"}", Encoding.UTF8, "application/json");
        responses.Enqueue(r1);

        var r2 = new HttpResponseMessage(HttpStatusCode.OK);
        r2.Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json");
        responses.Enqueue(r2);

        var handler = new SequenceHandler(responses);
        using var http = new HttpClient(handler);

        var delays = new List<int>();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0
        };

        using var client = new NotionApiClient(options, http, (ms, _) =>
        {
            delays.Add(ms);
            now = now.AddMilliseconds(ms);
            return Task.CompletedTask;
        }, () => now);

        using var doc = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);

        Assert.True(doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
        Assert.Equal(2, handler.RequestCount);
        Assert.Single(delays);
        Assert.True(delays[0] > 0);
    }

    [Fact]
    public async Task GetRetryDelayMs_WithRetryAfterSecondsString_UsesSeconds()
    {
        var responses = new Queue<HttpResponseMessage>();
        var r1 = new HttpResponseMessage((HttpStatusCode)429);
        r1.Headers.TryAddWithoutValidation("Retry-After", "2");
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
        Assert.Single(delays);
        Assert.Equal(2000, delays[0]);
    }

    [Fact]
    public async Task GetRetryDelayMs_ExponentialBackoffFallback()
    {
        var responses = new Queue<HttpResponseMessage>();
        var r1 = new HttpResponseMessage((HttpStatusCode)429);
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
        Assert.Single(delays);
        Assert.Equal(1000, delays[0]);
    }

    [Fact]
    public async Task MaybeThrottleAsync_WithMaxRpsNull_NoThrottling()
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

        var delays = new List<int>();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRps = null
        };

        using var client = new NotionApiClient(options, http, (ms, _) =>
        {
            delays.Add(ms);
            return Task.CompletedTask;
        });

        using var doc1 = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);
        using var doc2 = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);

        Assert.True(doc1.RootElement.TryGetProperty("ok", out var ok1) && ok1.ValueKind == JsonValueKind.True);
        Assert.True(doc2.RootElement.TryGetProperty("ok", out var ok2) && ok2.ValueKind == JsonValueKind.True);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task MaybeDelayAsync_WithDelayGreaterThanZero_Delays()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });

        var handler = new SequenceHandler(responses);
        using var http = new HttpClient(handler);

        var delays = new List<int>();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 150
        };

        using var client = new NotionApiClient(options, http, (ms, _) =>
        {
            delays.Add(ms);
            return Task.CompletedTask;
        });

        using var doc = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);

        Assert.True(doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
        Assert.Single(delays);
        Assert.Equal(150, delays[0]);
    }

    [Fact]
    public async Task ReadJsonAsync_Non2xxStatus_ThrowsContentException()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\":\"server_error\"}", Encoding.UTF8, "application/json")
        });

        var handler = new SequenceHandler(responses);
        using var http = new HttpClient(handler);

        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };

        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<ContentException>(() =>
            client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None));
        Assert.Contains("500", ex.Message);
        Assert.Contains("Internal Server Error", ex.Message);
    }

    [Fact]
    public async Task ReadJsonAsync_InvalidJson_ThrowsContentException()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json {{{", Encoding.UTF8, "application/json")
        });

        var handler = new SequenceHandler(responses);
        using var http = new HttpClient(handler);

        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };

        using var client = new NotionApiClient(options, http, (_, _) => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<ContentException>(() =>
            client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None));
        Assert.Contains("invalid json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MaybeThrottleAsync_WithMaxRpsZero_NoThrottling()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });

        var handler = new SequenceHandler(responses);
        using var http = new HttpClient(handler);

        var delays = new List<int>();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            RequestDelayMs = 0,
            MaxRps = 0
        };

        using var client = new NotionApiClient(options, http, (ms, _) =>
        {
            delays.Add(ms);
            return Task.CompletedTask;
        });

        using var doc = await client.GetAsync("https://api.notion.com/v1/databases/db", CancellationToken.None);

        Assert.True(doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task MaybeDelayAsync_WithZeroDelay_NoDelay()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });

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
        Assert.Empty(delays);
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

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public CaptureHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
