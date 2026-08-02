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
        using var client = new NotionClient(options, handler);

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
        using var client = new NotionClient(options, handler);

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
        using var client = new NotionClient(options, handler);

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
        using var client = new NotionClient(options, handler);

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
        using var client = new NotionClient(options, handler);

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
        using var client = new NotionClient(options, handler);

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
        using var client = new NotionClient(options, handler);

        var renderer = new NotionBlocksRenderer(client);
        var sb = new StringBuilder();
        await renderer.RenderChildrenToBuilderAsync("parent-id", sb, CancellationToken.None);

        var html = sb.ToString();
        Assert.DoesNotContain("notion-default", html);
        Assert.Contains("Normal", html);
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
