using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionContentProviderExtendedEdgeCasesTests
{
    [Fact]
    public async Task LoadAsync_WithRenderContentFalse_ReturnsEmptyBody()
    {
        var handler = new SimplePageHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            RenderContent = false
        };
        NotionApiClient CreateClient() =>
            new(options, handler, (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadRawAsync();
        var item = Assert.Single(result.Documents);
        var body = await result.BodyStore.GetAsync(item);

        Assert.Equal(string.Empty, body.Html);
    }

    [Fact]
    public async Task LoadAsync_WithCacheCorruption_LogsWarningAndRerenders()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "bukit-corrupt-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pagesDir = Path.Combine(cacheDir, "pages");
            Directory.CreateDirectory(pagesDir);
            await File.WriteAllTextAsync(Path.Combine(pagesDir, "page-1.json"), "NOT VALID JSON {{{");

            var logMessages = new List<string>();
            var logger = new ListLogger(logMessages);
            var handler = new SimplePageHandler();
            var options = new NotionProviderOptions
            {
                DatabaseId = "db-123",
                Token = "secret-token",
                RequestDelayMs = 0,
                FilterType = "none",
                CacheMode = "readwrite",
                CacheDir = cacheDir
            };
            NotionApiClient CreateClient() =>
                new(options, handler, (_, _) => Task.CompletedTask);
            var provider = new NotionContentProvider(options, logger, CreateClient);

            var result = await provider.LoadRawAsync();
            var item = Assert.Single(result.Documents);
            var body = await result.BodyStore.GetAsync(item);

            Assert.Equal("<p>Rendered body</p>", body.Html.Trim());
            Assert.Contains(logMessages, m => m.Contains("event=notion.cache.read_failed") && m.Contains("pageId=page-1"));
            Assert.Equal(1, handler.Count(HttpMethod.Get, "https://api.notion.com/v1/blocks/page-1/children?page_size=100"));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WithoutLastEditedTime_WritesNullToCache()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "bukit-null-edited-" + Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new NoLastEditedTimeHandler();
            var options = new NotionProviderOptions
            {
                DatabaseId = "db-123",
                Token = "secret-token",
                RequestDelayMs = 0,
                FilterType = "none",
                CacheMode = "readwrite",
                CacheDir = cacheDir
            };
            NotionApiClient CreateClient() =>
                new(options, handler, (_, _) => Task.CompletedTask);
            var provider = new NotionContentProvider(options, logger: null, CreateClient);

            var result = await provider.LoadRawAsync();
            var item = Assert.Single(result.Documents);
            var body = await result.BodyStore.GetAsync(item);

            Assert.Equal("<p>No edit time</p>", body.Html.Trim());
            var cacheContent = await File.ReadAllTextAsync(Path.Combine(cacheDir, "pages", "page-1.json"));
            Assert.Contains("\"lastEditedTime\":null", cacheContent);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_RelationResolveFails_LogsWarningAndContinues()
    {
        var logMessages = new List<string>();
        var logger = new ListLogger(logMessages);
        var handler = new RelationResolveFailureHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            RenderContent = false,
            FieldPolicyMode = "all"
        };
        NotionApiClient CreateClient() =>
            new(options, handler, (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger, CreateClient);

        var result = await provider.LoadRawAsync();
        var item = Assert.Single(result.Documents);

        Assert.Contains(logMessages, m => m.Contains("event=notion.relation.resolve_failed") && m.Contains("pageId=tag-missing"));
        Assert.NotNull(item.CustomFields);
        Assert.Contains("tags_links", item.CustomFields);
    }

    [Fact]
    public async Task LoadAsync_RelationCacheHit_SkipsApiCall()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "bukit-rel-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var target = new RelationTargetInfo("tag-1", "Cached Tag", "cached-tag", "page", "https://example.test/cached-tag");
            var cache = NotionRelationTargetCache.Create("readwrite", cacheDir);
            Assert.NotNull(cache);
            await cache.WriteAsync(target, CancellationToken.None);

            var handler = new RelationCacheHitHandler();
            var options = new NotionProviderOptions
            {
                DatabaseId = "db-123",
                Token = "secret-token",
                RequestDelayMs = 0,
                FilterType = "none",
                RenderContent = false,
                FieldPolicyMode = "all",
                CacheMode = "readwrite",
                CacheDir = cacheDir
            };
            NotionApiClient CreateClient() =>
                new(options, handler, (_, _) => Task.CompletedTask);
            var provider = new NotionContentProvider(options, logger: null, CreateClient);

            var result = await provider.LoadRawAsync();
            var item = Assert.Single(result.Documents);

            Assert.NotNull(item.CustomFields);
            Assert.Contains("tags_links", item.CustomFields);
            Assert.True(ContentFieldReader.TryGetField(item.CustomFields, "tags", out var tagsField));
            var tags = Assert.IsAssignableFrom<IEnumerable<string>>(tagsField.Value);
            Assert.Equal(new[] { "Cached Tag" }, tags.ToArray());
            Assert.Equal(0, handler.Count(HttpMethod.Get, "https://api.notion.com/v1/pages/tag-1"));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────

    private sealed class SimplePageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Invocations { get; } = new();

        public int Count(HttpMethod method, string url) =>
            Invocations.Count(req => req.Method == method && req.RequestUri!.AbsoluteUri == url);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            var url = request.RequestUri!.AbsoluteUri;

            if (request.Method == HttpMethod.Get && url.Contains("/databases/db-123"))
            {
                return Ok("""
                {
                  "properties": { "Title": {}, "Slug": {}, "Type": {} }
                }
                """);
            }

            if (request.Method == HttpMethod.Post && url.EndsWith("/query"))
            {
                return Ok("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "last_edited_time": "2026-05-15T12:00:00.000Z",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Test Page" }]
                        },
                        "Slug": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "test-page" }]
                        },
                        "Type": {
                          "type": "select",
                          "select": { "name": "post" }
                        }
                      }
                    }
                  ]
                }
                """);
            }

            if (request.Method == HttpMethod.Get && url.Contains("/children"))
            {
                return Ok("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "block-1",
                      "paragraph": { "rich_text": [{ "plain_text": "Rendered body" }] }
                    }
                  ]
                }
                """);
            }

            return NotFound();
        }
    }

    private sealed class NoLastEditedTimeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;

            if (request.Method == HttpMethod.Get && url.Contains("/databases/db-123"))
            {
                return Ok("""{"properties":{"Title":{},"Slug":{},"Type":{}}}""");
            }

            if (request.Method == HttpMethod.Post && url.EndsWith("/query"))
            {
                return Ok("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "No Edit Page" }]
                        },
                        "Slug": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "no-edit-page" }]
                        },
                        "Type": {
                          "type": "select",
                          "select": { "name": "post" }
                        }
                      }
                    }
                  ]
                }
                """);
            }

            if (request.Method == HttpMethod.Get && url.Contains("/children"))
            {
                return Ok("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "block-1",
                      "paragraph": { "rich_text": [{ "plain_text": "No edit time" }] }
                    }
                  ]
                }
                """);
            }

            return NotFound();
        }
    }

    private sealed class RelationResolveFailureHandler : HttpMessageHandler
    {
        private int _queryCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;

            if (request.Method == HttpMethod.Get && url.Contains("/databases/db-123"))
            {
                return Ok("""{"properties":{"Title":{},"Slug":{},"Type":{},"Tags":{}}}""");
            }

            if (request.Method == HttpMethod.Post && url.EndsWith("/query"))
            {
                if (++_queryCount == 1)
                {
                    return Ok("""
                    {
                      "has_more": false,
                      "results": [
                        {
                          "id": "page-1",
                          "created_time": "2026-01-02T03:04:05.000Z",
                          "properties": {
                            "Title": {
                              "type": "title",
                              "title": [{ "plain_text": "Relation Page" }]
                            },
                            "Slug": {
                              "type": "rich_text",
                              "rich_text": [{ "plain_text": "relation-page" }]
                            },
                            "Type": {
                              "type": "select",
                              "select": { "name": "post" }
                            },
                            "Tags": {
                              "type": "relation",
                              "relation": [{ "id": "tag-missing" }]
                            }
                          }
                        }
                      ]
                    }
                    """);
                }

                return NotFound();
            }

            if (request.Method == HttpMethod.Get && url == "https://api.notion.com/v1/pages/tag-missing")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server Error", Encoding.UTF8, "text/plain")
                });
            }

            return NotFound();
        }
    }

    private sealed class RelationCacheHitHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Invocations { get; } = new();

        public int Count(HttpMethod method, string url) =>
            Invocations.Count(req => req.Method == method && req.RequestUri!.AbsoluteUri == url);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            var url = request.RequestUri!.AbsoluteUri;

            if (request.Method == HttpMethod.Get && url.Contains("/databases/db-123"))
            {
                return Ok("""{"properties":{"Title":{},"Slug":{},"Type":{},"Tags":{}}}""");
            }

            if (request.Method == HttpMethod.Post && url.EndsWith("/query"))
            {
                return Ok("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Relation Page" }]
                        },
                        "Slug": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "relation-page" }]
                        },
                        "Type": {
                          "type": "select",
                          "select": { "name": "post" }
                        },
                        "Tags": {
                          "type": "relation",
                          "relation": [{ "id": "tag-1" }]
                        }
                      }
                    }
                  ]
                }
                """);
            }

            return NotFound();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Task<HttpResponseMessage> Ok(string json)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    private static Task<HttpResponseMessage> NotFound()
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
    }

    private sealed class ListLogger : Bukit.Shared.ILogger
    {
        private readonly List<string> _messages;

        public ListLogger(List<string> messages)
        {
            _messages = messages;
        }

        public void Debug(string message) => _messages.Add(message);
        public void Info(string message) => _messages.Add(message);
        public void Warn(string message) => _messages.Add(message);
        public void Error(string message) => _messages.Add(message);
    }

}
