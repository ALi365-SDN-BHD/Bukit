using System.Net;
using System.Text;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class NotionPageCacheExpiryTests
{
    [Fact]
    public async Task PageCache_ExpiredSignedLink_ReadwriteRefreshesAndReadonlyReuses()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-notion-expiry-" + Guid.NewGuid().ToString("N"));
        var cache = NotionCacheManager.CreatePageHtmlCache(new NotionContentSourceOptions
        {
            DatabaseId = "db-1",
            Token = "token",
            CacheMode = "readwrite",
            CacheDir = root
        });
        var handler = new CountingHandler();
        using var client = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler,
            static (_, _) => Task.CompletedTask,
            static () => DateTimeOffset.UtcNow);
        var renderer = new NotionBlocksRenderer(client);

        try
        {
            Assert.NotNull(cache);
            var first = await NotionCacheManager.GetOrRenderPageHtmlAsync(
                renderer, cache, "page-1", "v1", CancellationToken.None);
            Assert.Equal(1, handler.RequestCount);
            Assert.Contains("X-Amz-Expires", first, StringComparison.Ordinal);

            var second = await NotionCacheManager.GetOrRenderPageHtmlAsync(
                renderer, cache, "page-1", "v1", CancellationToken.None);
            Assert.Equal(2, handler.RequestCount);
            Assert.Equal(first, second);

            var readonlyResult = await NotionCacheManager.GetOrRenderPageHtmlAsync(
                renderer, cache with { Mode = "readonly" }, "page-1", "v1", CancellationToken.None);
            Assert.Equal(2, handler.RequestCount);
            Assert.Equal(first, readonlyResult);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PageCache_NotionExpiryTimeWithoutSignedQuery_ReadwriteRefreshes()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-notion-expiry-time-" + Guid.NewGuid().ToString("N"));
        var cache = NotionCacheManager.CreatePageHtmlCache(new NotionContentSourceOptions
        {
            DatabaseId = "db-1",
            Token = "token",
            CacheMode = "readwrite",
            CacheDir = root
        });
        var handler = new ExpiryTimeHandler();
        using var client = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler,
            static (_, _) => Task.CompletedTask,
            static () => DateTimeOffset.UtcNow);
        var renderer = new NotionBlocksRenderer(client);

        try
        {
            Assert.NotNull(cache);
            await NotionCacheManager.GetOrRenderPageHtmlAsync(
                renderer, cache, "page-1", "v1", CancellationToken.None);
            await NotionCacheManager.GetOrRenderPageHtmlAsync(
                renderer, cache, "page-1", "v1", CancellationToken.None);

            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class ExpiryTimeHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            const string json = """
                {
                  "results": [
                    {
                      "id": "block-1",
                      "type": "file",
                      "file": {
                        "type": "file",
                        "name": "manual.pdf",
                        "file": {
                          "url": "https://files.example/a.pdf",
                          "expiry_time": "2020-01-01T00:00:00.000Z"
                        },
                        "caption": []
                      }
                    }
                  ],
                  "has_more": false
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            const string json = """
                {
                  "results": [
                    {
                      "id": "block-1",
                      "type": "paragraph",
                      "paragraph": {
                        "rich_text": [
                          {
                            "type": "text",
                            "plain_text": "download",
                            "text": {
                              "content": "download",
                              "link": {
                                "url": "https://files.example/a.png?X-Amz-Date=20200101T000000Z&X-Amz-Expires=60"
                              }
                            }
                          }
                        ]
                      }
                    }
                  ],
                  "has_more": false
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
