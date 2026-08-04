#pragma warning disable CS0618 // Test-only use of obsolete injected-HttpClient constructor
using System.Net;
using System.Text;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionBlocksRendererPaginationTests
{
    [Fact]
    public async Task RenderChildren_SinglePage_NoPagination()
    {
        var handler = new JsonSequenceHandler(new[]
        {
            new JsonSequenceEntry((req, _) =>
            {
                var json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "blk-1",
                      "paragraph": { "rich_text": [{ "plain_text": "Hello" }] }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            })
        });

        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.Contains("<p>Hello</p>", html);
        Assert.Single(handler.Invocations);
    }

    [Fact]
    public async Task RenderChildren_MultiplePages_FollowsCursor()
    {
        var handler = new JsonSequenceHandler(new[]
        {
            new JsonSequenceEntry((req, _) =>
            {
                Assert.False(req.RequestUri!.Query.Contains("start_cursor"), "First page should not have cursor");
                var json = """
                {
                  "has_more": true,
                  "next_cursor": "cursor-abc",
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "blk-1",
                      "paragraph": { "rich_text": [{ "plain_text": "Page 1" }] }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }),
            new JsonSequenceEntry((req, _) =>
            {
                Assert.Contains("start_cursor=cursor-abc", req.RequestUri!.Query);
                var json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "blk-2",
                      "paragraph": { "rich_text": [{ "plain_text": "Page 2" }] }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            })
        });

        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.Contains("Page 1", html);
        Assert.Contains("Page 2", html);
        Assert.Equal(2, handler.Invocations.Count);
    }

    [Fact]
    public async Task RenderChildren_EmptyResults_ReturnsEmpty()
    {
        var handler = new JsonSequenceHandler(new[]
        {
            new JsonSequenceEntry((req, _) =>
            {
                var json = """
                {
                  "has_more": false,
                  "results": []
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            })
        });

        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.True(string.IsNullOrEmpty(html) || html == "\r\n" || html == "\n" || html.All(c => c is '\r' or '\n'));
    }

    [Fact]
    public async Task RenderChildren_ListTypeSwitching_ClosesPreviousList()
    {
        var handler = new JsonSequenceHandler(new[]
        {
            new JsonSequenceEntry((req, _) =>
            {
                var json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "bulleted_list_item",
                      "id": "blk-1",
                      "has_children": false,
                      "bulleted_list_item": { "rich_text": [{ "plain_text": "Bullet" }] }
                    },
                    {
                      "type": "numbered_list_item",
                      "id": "blk-2",
                      "has_children": false,
                      "numbered_list_item": { "rich_text": [{ "plain_text": "Number" }] }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            })
        });

        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.Contains("<ul>", html);
        Assert.Contains("</ul>", html);
        Assert.Contains("<ol>", html);
        Assert.Contains("</ol>", html);
        Assert.Contains("Bullet", html);
        Assert.Contains("Number", html);
    }

    [Fact]
    public async Task RenderChildren_NestedListItem_RendersChildren()
    {
        var handler = new JsonSequenceHandler(new[]
        {
            new JsonSequenceEntry((req, _) =>
            {
                var json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "bulleted_list_item",
                      "id": "blk-1",
                      "has_children": true,
                      "bulleted_list_item": { "rich_text": [{ "plain_text": "Parent" }] }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }),
            new JsonSequenceEntry((req, _) =>
            {
                Assert.Contains("blk-1", req.RequestUri!.ToString());
                var json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "bulleted_list_item",
                      "id": "blk-2",
                      "has_children": false,
                      "bulleted_list_item": { "rich_text": [{ "plain_text": "Child" }] }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            })
        });

        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.Contains("Parent", html);
        Assert.Contains("Child", html);
    }

    [Fact]
    public async Task RenderChildren_BlockLevelColorOnListItem()
    {
        var handler = new JsonSequenceHandler(new[]
        {
            new JsonSequenceEntry((req, _) =>
            {
                var json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "bulleted_list_item",
                      "id": "blk-1",
                      "has_children": false,
                      "bulleted_list_item": {
                        "rich_text": [{ "plain_text": "Colored bullet" }],
                        "color": "red"
                      }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            })
        });

        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.Contains("class=\"notion-red\"", html);
        Assert.Contains("Colored bullet", html);
    }

    [Fact]
    public async Task RenderChildren_DefaultColorListItem_NoColorClass()
    {
        var handler = new JsonSequenceHandler(new[]
        {
            new JsonSequenceEntry((req, _) =>
            {
                var json = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "bulleted_list_item",
                      "id": "blk-1",
                      "has_children": false,
                      "bulleted_list_item": {
                        "rich_text": [{ "plain_text": "Normal" }],
                        "color": "default"
                      }
                    }
                  ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            })
        });

        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.DoesNotContain("notion-default", html);
        Assert.Contains("Normal", html);
    }

    [Fact]
    public async Task BlocksRenderer_RepeatedCursor_ThrowsStablePaginationException()
    {
        // The API keeps returning the same cursor with has_more=true; the guard must
        // fail closed instead of looping forever (baseline hung until the WaitAsync cap).
        var handler = new RepeatingCursorHandler();
        var options = new NotionClientOptions { Token = "token", RequestDelayMs = 0, MaxRetries = 0 };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);
        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();

        var exception = await Assert.ThrowsAsync<NotionPaginationException>(
            () => renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None))
            .WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(NotionPaginationGuard.ReasonRepeatedCursor, exception.Reason);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task TableRenderer_RepeatedCursor_ThrowsStablePaginationException()
    {
        var handler = new TableRepeatingCursorHandler();
        var options = new NotionClientOptions { Token = "token", RequestDelayMs = 0, MaxRetries = 0 };
        using var client = CanonicalBlockRendererTestSupport.CreateClient(options, handler);
        var renderer = new NotionBlocksRenderer(client);

        var exception = await Assert.ThrowsAsync<NotionPaginationException>(
            () => renderer.RenderPageAsync("page-id", CancellationToken.None))
            .WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(NotionPaginationGuard.ReasonRepeatedCursor, exception.Reason);
    }

    [Fact]
    public void Pagination_MoreThan10000Requests_ThrowsBudgetException()
    {
        var guard = new NotionPaginationGuard();
        NotionPaginationException? caught = null;
        var iterations = 0;
        try
        {
            for (var i = 0; i < NotionPaginationGuard.MaxRequests + 1; i++)
            {
                iterations++;
                guard.CountRequest();
            }
        }
        catch (NotionPaginationException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal(NotionPaginationGuard.ReasonRequestBudgetExceeded, caught!.Reason);
        Assert.Equal(NotionPaginationGuard.MaxRequests + 1, iterations);
    }

    [Fact]
    public void Pagination_Advance_MissingOrRepeatedCursor_ThrowsStableReasons()
    {
        var guard = new NotionPaginationGuard();

        var missing = Assert.Throws<NotionPaginationException>(() => guard.Advance(null));
        Assert.Equal(NotionPaginationGuard.ReasonMissingCursor, missing.Reason);

        guard.Advance("cursor-1");
        var repeated = Assert.Throws<NotionPaginationException>(() => guard.Advance("cursor-1"));
        Assert.Equal(NotionPaginationGuard.ReasonRepeatedCursor, repeated.Reason);
    }

    private sealed class RepeatingCursorHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var json = """
            {
              "has_more": true,
              "next_cursor": "cursor-loop",
              "results": [
                {
                  "type": "paragraph",
                  "paragraph": { "rich_text": [{ "plain_text": "Loop" }] }
                }
              ]
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TableRepeatingCursorHandler : HttpMessageHandler
    {
        private int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requestCount++;
            var json = _requestCount == 1
                ? """
                  {
                    "has_more": false,
                    "results": [
                      {
                        "type": "table",
                        "id": "table-1",
                        "has_children": true,
                        "table": { "has_column_header": false, "has_row_header": false }
                      }
                    ]
                  }
                  """
                : """
                  {
                    "has_more": true,
                    "next_cursor": "row-cursor-loop",
                    "results": [
                      {
                        "type": "table_row",
                        "table_row": { "cells": [[{ "plain_text": "cell" }]] }
                      }
                    ]
                  }
                  """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class JsonSequenceHandler : HttpMessageHandler
    {
        private readonly IReadOnlyList<JsonSequenceEntry> _entries;
        private int _index;

        public JsonSequenceHandler(IReadOnlyList<JsonSequenceEntry> entries)
        {
            _entries = entries;
        }

        public List<HttpRequestMessage> Invocations { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            if (_index >= _entries.Count)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"has_more\":false,\"results\":[]}",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            var entry = _entries[_index++];
            return Task.FromResult(entry.Handler(request, cancellationToken));
        }
    }

    private sealed record JsonSequenceEntry(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> Handler);
}
