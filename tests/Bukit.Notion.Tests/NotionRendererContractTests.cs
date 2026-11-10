using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Notion.Tests;

// fix-notion-body-cache-contract: cached Notion bodies are keyed by
// NotionBlocksRenderer.ContractVersion. When this corpus renders different HTML,
// bump ContractVersion and APPEND the new hash to s_contractHashes; never edit old rows.
public sealed class NotionRendererContractTests
{
    private static readonly (int Version, string Hash)[] s_contractHashes =
    [
        (1, "sha256:d27555167a1c491a46ea90a3e02877936c37607833bbeb67bc69e7061080128a"),
    ];

    [Fact]
    public async Task Corpus_RendersTheRecordedHashForTheCurrentContractVersion()
    {
        var hash = await RenderCorpusHashAsync();
        var recorded = s_contractHashes.Where(row => row.Version == NotionBlocksRenderer.ContractVersion).ToArray();

        Assert.True(recorded.Length == 1,
            $"No hash recorded for Notion renderer contract {NotionBlocksRenderer.ContractVersion}. " +
            $"Append ({NotionBlocksRenderer.ContractVersion}, \"{hash}\") to s_contractHashes.");
        Assert.True(recorded[0].Hash == hash,
            $"Notion renderer output changed under contract {NotionBlocksRenderer.ContractVersion}. " +
            $"Bump NotionBlocksRenderer.ContractVersion to {NotionBlocksRenderer.ContractVersion + 1} " +
            $"and append ({NotionBlocksRenderer.ContractVersion + 1}, \"{hash}\") to s_contractHashes.");
    }

    [Fact]
    public void Record_IsAppendOnlyAndEndsAtTheCurrentVersion()
    {
        var versions = s_contractHashes.Select(row => row.Version).ToArray();
        Assert.Equal(Enumerable.Range(1, versions.Length), versions);
        Assert.Equal(NotionBlocksRenderer.ContractVersion, versions[^1]);
        Assert.Equal(versions.Length, s_contractHashes.Select(row => row.Hash).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Corpus_CoversEveryBuiltInBlockType()
    {
        using var corpus = JsonDocument.Parse(Corpus);
        var covered = corpus.RootElement.EnumerateObject()
            .SelectMany(children => children.Value.EnumerateArray())
            .Select(block => block.GetProperty("type").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var missing = NotionBlockRendererRegistry.CreateDefault().BuiltInBlockTypes
            .Where(type => !covered.Contains(type))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            $"Add these block types to the Notion renderer contract corpus: {string.Join(", ", missing)}");
        Assert.Contains("bulleted_list_item", covered);
        Assert.Contains("numbered_list_item", covered);
        Assert.Contains("unsupported_future_block", covered);
    }

    [Fact]
    public async Task Corpus_ReachesNestedChildrenAndTableRows()
    {
        var html = await RenderCorpusAsync();
        foreach (var marker in new[] { "Inside heading", "nested", "Callout body", "Sub task", "Hidden", "b &amp; c", "Synced", "Left", "Right", "<ul>", "<ol>", "<strong>", "<code>" })
        {
            Assert.Contains(marker, html, StringComparison.Ordinal);
        }
    }

    private static async Task<string> RenderCorpusHashAsync()
        => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(await RenderCorpusAsync())));

    private static async Task<string> RenderCorpusAsync()
    {
        using var handler = new CorpusHandler(Corpus);
        using var client = new NotionClient(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler,
            static (_, _) => Task.CompletedTask,
            static () => new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero));
        return await new NotionBlocksRenderer(client).RenderPageAsync("page", CancellationToken.None);
    }

    private sealed class CorpusHandler(string corpus) : HttpMessageHandler
    {
        private readonly JsonDocument _corpus = JsonDocument.Parse(corpus);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // /v1/blocks/{id}/children?page_size=100
            var segments = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var blockId = segments[^2];
            var results = _corpus.RootElement.TryGetProperty(blockId, out var children) ? children.GetRawText() : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"results":{{results}},"has_more":false,"next_cursor":null}""", Encoding.UTF8, "application/json")
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _corpus.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    // Block id -> children. "page" is the page root.
    private const string Corpus = """
        {
          "page": [
            { "id": "h1", "type": "heading_1", "heading_1": { "rich_text": [ { "type": "text", "plain_text": "Title", "text": { "content": "Title" } } ], "color": "default" } },
            { "id": "h2", "type": "heading_2", "heading_2": { "rich_text": [ { "type": "text", "plain_text": "Section", "text": { "content": "Section" } } ], "color": "blue" } },
            { "id": "h3", "type": "heading_3", "has_children": true, "heading_3": { "is_toggleable": true, "rich_text": [ { "type": "text", "plain_text": "Folded", "text": { "content": "Folded" } } ] } },
            { "id": "toc", "type": "table_of_contents", "table_of_contents": { "color": "default" } },
            { "id": "p1", "type": "paragraph", "paragraph": { "color": "gray_background", "rich_text": [
              { "type": "text", "plain_text": "plain ", "text": { "content": "plain " }, "annotations": { "bold": false, "italic": false, "strikethrough": false, "underline": false, "code": false, "color": "default" } },
              { "type": "text", "plain_text": "bold", "text": { "content": "bold" }, "annotations": { "bold": true, "italic": false, "strikethrough": false, "underline": false, "code": false, "color": "default" } },
              { "type": "text", "plain_text": "italic", "text": { "content": "italic" }, "annotations": { "bold": false, "italic": true, "strikethrough": false, "underline": false, "code": false, "color": "default" } },
              { "type": "text", "plain_text": "strike", "text": { "content": "strike" }, "annotations": { "bold": false, "italic": false, "strikethrough": true, "underline": false, "code": false, "color": "default" } },
              { "type": "text", "plain_text": "under", "text": { "content": "under" }, "annotations": { "bold": false, "italic": false, "strikethrough": false, "underline": true, "code": false, "color": "default" } },
              { "type": "text", "plain_text": "code", "text": { "content": "code" }, "annotations": { "bold": false, "italic": false, "strikethrough": false, "underline": false, "code": true, "color": "default" } },
              { "type": "text", "plain_text": "red", "text": { "content": "red" }, "annotations": { "bold": true, "italic": true, "strikethrough": false, "underline": false, "code": false, "color": "red_background" } },
              { "type": "text", "plain_text": "link <&>", "href": "https://example.com/a?b=1&c=2", "text": { "content": "link <&>", "link": { "url": "https://example.com/a?b=1&c=2" } } },
              { "type": "equation", "plain_text": "x^2", "equation": { "expression": "x^2" } },
              { "type": "mention", "plain_text": "@Page", "mention": { "type": "page", "page": { "id": "p-target" } } }
            ] } },
            { "id": "b1", "type": "bulleted_list_item", "has_children": true, "bulleted_list_item": { "rich_text": [ { "type": "text", "plain_text": "bullet one", "text": { "content": "bullet one" } } ], "color": "default" } },
            { "id": "b2", "type": "bulleted_list_item", "bulleted_list_item": { "rich_text": [ { "type": "text", "plain_text": "bullet two", "text": { "content": "bullet two" } } ], "color": "orange" } },
            { "id": "n1", "type": "numbered_list_item", "numbered_list_item": { "rich_text": [ { "type": "text", "plain_text": "number one", "text": { "content": "number one" } } ] } },
            { "id": "n2", "type": "numbered_list_item", "numbered_list_item": { "rich_text": [ { "type": "text", "plain_text": "number two", "text": { "content": "number two" } } ] } },
            { "id": "q1", "type": "quote", "quote": { "rich_text": [ { "type": "text", "plain_text": "Quoted", "text": { "content": "Quoted" } } ] } },
            { "id": "c1", "type": "code", "code": { "language": "csharp", "rich_text": [ { "type": "text", "plain_text": "var x = \"<a>\";", "text": { "content": "var x = \"<a>\";" } } ], "caption": [ { "type": "text", "plain_text": "Snippet", "text": { "content": "Snippet" } } ] } },
            { "id": "d1", "type": "divider", "divider": {} },
            { "id": "img", "type": "image", "image": { "type": "file", "file": { "url": "https://prod-files-secure.s3.us-west-2.amazonaws.com/a/b/photo.png?X-Amz-Signature=abc&X-Amz-Expires=3600", "expiry_time": "2026-11-10T01:00:00.000Z" }, "caption": [ { "type": "text", "plain_text": "Photo", "text": { "content": "Photo" } } ] } },
            { "id": "img2", "type": "image", "image": { "type": "external", "external": { "url": "https://cdn.example.com/x.jpg" }, "caption": [] } },
            { "id": "co", "type": "callout", "has_children": true, "callout": { "icon": { "type": "emoji", "emoji": "💡" }, "color": "yellow_background", "rich_text": [ { "type": "text", "plain_text": "Note", "text": { "content": "Note" } } ] } },
            { "id": "co2", "type": "callout", "callout": { "icon": { "type": "external", "external": { "url": "https://cdn.example.com/icon.png" } }, "rich_text": [ { "type": "text", "plain_text": "Icon", "text": { "content": "Icon" } } ] } },
            { "id": "td", "type": "to_do", "has_children": true, "to_do": { "checked": true, "rich_text": [ { "type": "text", "plain_text": "Done", "text": { "content": "Done" } } ] } },
            { "id": "td2", "type": "to_do", "to_do": { "checked": false, "rich_text": [ { "type": "text", "plain_text": "Open", "text": { "content": "Open" } } ] } },
            { "id": "tg", "type": "toggle", "has_children": true, "toggle": { "rich_text": [ { "type": "text", "plain_text": "More", "text": { "content": "More" } } ] } },
            { "id": "bm", "type": "bookmark", "bookmark": { "url": "https://example.com/bookmark", "caption": [ { "type": "text", "plain_text": "Saved", "text": { "content": "Saved" } } ] } },
            { "id": "lp", "type": "link_preview", "link_preview": { "url": "https://github.com/org/repo" } },
            { "id": "vy", "type": "video", "video": { "type": "external", "external": { "url": "https://www.youtube.com/watch?v=abc123" }, "caption": [] } },
            { "id": "vf", "type": "video", "video": { "type": "file", "file": { "url": "https://cdn.example.com/clip.mp4" }, "caption": [ { "type": "text", "plain_text": "Clip", "text": { "content": "Clip" } } ] } },
            { "id": "em", "type": "embed", "embed": { "url": "https://maps.example.com/embed?q=1", "caption": [] } },
            { "id": "eq", "type": "equation", "equation": { "expression": "E = mc^2" } },
            { "id": "tb", "type": "table", "has_children": true, "table": { "table_width": 2, "has_column_header": true, "has_row_header": true } },
            { "id": "fl", "type": "file", "file": { "type": "file", "name": "manual.pdf", "file": { "url": "https://cdn.example.com/manual.pdf" }, "caption": [ { "type": "text", "plain_text": "Manual", "text": { "content": "Manual" } } ] } },
            { "id": "pdf", "type": "pdf", "pdf": { "type": "external", "external": { "url": "https://cdn.example.com/doc.pdf" }, "caption": [] } },
            { "id": "au", "type": "audio", "audio": { "type": "external", "external": { "url": "https://cdn.example.com/a.mp3" }, "caption": [] } },
            { "id": "cp", "type": "child_page", "child_page": { "title": "Child page" } },
            { "id": "cd", "type": "child_database", "child_database": { "title": "Child database" } },
            { "id": "sb", "type": "synced_block", "has_children": true, "synced_block": { "synced_from": null } },
            { "id": "cl", "type": "column_list", "has_children": true, "column_list": {} },
            { "id": "ltp", "type": "link_to_page", "link_to_page": { "type": "page_id", "page_id": "p-target" } },
            { "id": "ltd", "type": "link_to_page", "link_to_page": { "type": "database_id", "database_id": "db-target" } },
            { "id": "bc", "type": "breadcrumb", "breadcrumb": {} },
            { "id": "tp", "type": "template", "template": { "rich_text": [] } },
            { "id": "uk", "type": "unsupported_future_block", "unsupported_future_block": {} },
            { "id": "p2", "type": "paragraph", "paragraph": { "rich_text": [] } }
          ],
          "h3": [ { "id": "h3c", "type": "paragraph", "paragraph": { "rich_text": [ { "type": "text", "plain_text": "Inside heading", "text": { "content": "Inside heading" } } ] } } ],
          "b1": [ { "id": "b1a", "type": "numbered_list_item", "numbered_list_item": { "rich_text": [ { "type": "text", "plain_text": "nested", "text": { "content": "nested" } } ] } } ],
          "co": [ { "id": "coc", "type": "paragraph", "paragraph": { "rich_text": [ { "type": "text", "plain_text": "Callout body", "text": { "content": "Callout body" } } ] } } ],
          "td": [ { "id": "tdc", "type": "paragraph", "paragraph": { "rich_text": [ { "type": "text", "plain_text": "Sub task", "text": { "content": "Sub task" } } ] } } ],
          "tg": [ { "id": "tgc", "type": "paragraph", "paragraph": { "rich_text": [ { "type": "text", "plain_text": "Hidden", "text": { "content": "Hidden" } } ] } } ],
          "tb": [
            { "id": "r1", "type": "table_row", "table_row": { "cells": [ [ { "type": "text", "plain_text": "H1", "text": { "content": "H1" } } ], [ { "type": "text", "plain_text": "H2", "text": { "content": "H2" } } ] ] } },
            { "id": "r2", "type": "table_row", "table_row": { "cells": [ [ { "type": "text", "plain_text": "a", "text": { "content": "a" } } ], [ { "type": "text", "plain_text": "b & c", "text": { "content": "b & c" } } ] ] } }
          ],
          "sb": [ { "id": "sbc", "type": "paragraph", "paragraph": { "rich_text": [ { "type": "text", "plain_text": "Synced", "text": { "content": "Synced" } } ] } } ],
          "cl": [
            { "id": "col1", "type": "column", "has_children": true, "column": { "width_ratio": 0.25 } },
            { "id": "col2", "type": "column", "has_children": true, "column": {} }
          ],
          "col1": [ { "id": "col1c", "type": "paragraph", "paragraph": { "rich_text": [ { "type": "text", "plain_text": "Left", "text": { "content": "Left" } } ] } } ],
          "col2": [ { "id": "col2c", "type": "paragraph", "paragraph": { "rich_text": [ { "type": "text", "plain_text": "Right", "text": { "content": "Right" } } ] } } ]
        }
        """;
}
