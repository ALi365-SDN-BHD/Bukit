using System.Text.Json;
#pragma warning disable CS0618 // Test-only use of obsolete injected-HttpClient constructor
using System.Net;
using System.Text;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class NotionContentSourceTests
{
    [Fact]
    public async Task LoadRawAsync_ProjectsStableDocumentAndBodyContract()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db-1",
            Token = "token",
            FilterType = "none",
            FieldPolicyMode = "all",
            RenderContent = false
        };
        var handler = new SourceHandler();
        NotionContentClient CreateClient() => new(
            options,
            handler,
            static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var result = await source.LoadRawAsync();

        var document = Assert.Single(result.Documents);
        Assert.Equal("page-1", document.Id);
        Assert.Equal("Adapter page", document.Title);
        Assert.Equal("adapter-page", document.Slug);
        Assert.Equal(DateTimeOffset.Parse("2026-07-22T00:00:00Z"), document.PublishAt);
        Assert.Equal("notion", document.Source.Provider);
        Assert.Equal("page-1", document.Source.ExternalId);
        Assert.Equal("article", ContentFieldReader.GetText(document.CustomFields, "type"));
        Assert.Equal("en", ContentFieldReader.GetText(document.CustomFields, "language"));
        Assert.Equal("page-1", document.Body.BodyKey);

        var body = await result.BodyStore.GetAsync(document);
        Assert.Equal(string.Empty, body.Html);
        Assert.Equal(
            [
                "POST https://api.notion.com/v1/databases/db-1/query"
            ],
            handler.Requests);
    }

    [Fact]
    public async Task LoadRawAsync_PropagatesCallerCancellationUnchanged()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db-1",
            Token = "token"
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        NotionContentClient CreateClient() => new(
            options,
            new CancelingHandler(),
            static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.LoadRawAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task PageQuery_PreservesEngineTitleFragmentSpacing()
    {
        var singleHandler = new SingleResponseHandler("""
            {
              "id": "page-1",
              "url": "https://www.notion.so/page-1",
              "properties": {
                "Title": {
                  "type": "title",
                  "title": [
                    { "plain_text": "First" },
                    { "plain_text": "Second" }
                  ]
                }
              }
            }
            """);
        using var client = new Bukit.Notion.Transport.NotionClient(
            new Bukit.Notion.Transport.NotionClientOptions { Token = "token", MaxRetries = 0 },
            singleHandler,
            static (_, _) => Task.CompletedTask,
            static () => DateTimeOffset.UtcNow);

        var page = await NotionPageQuery.FetchAsync(client, "page-1", CancellationToken.None);

        Assert.Equal("First Second", page.Title);
        Assert.Equal("first-second", page.Slug);
    }

    [Fact]
    public async Task ContentSource_RepeatedCursor_ThrowsStablePaginationException()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db-loop",
            Token = "token",
            FilterType = "none",
            FieldPolicyMode = "all",
            RenderContent = false
        };
        var handler = new RepeatingCursorQueryHandler();
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var exception = await Assert.ThrowsAsync<Bukit.Notion.Rendering.NotionPaginationException>(
            () => source.LoadRawAsync()).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(Bukit.Notion.Rendering.NotionPaginationGuard.ReasonRepeatedCursor, exception.Reason);
        Assert.Equal(2, handler.QueryCount);
    }

    [Fact]
    public async Task LoadRawAsync_AutoSummary_IsPresentBeforeCanonicalConversion()
    {
        var options = CreateAutoSummaryOptions();
        var handler = new AutoSummaryBlocksHandler();
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var result = await source.LoadRawAsync();

        var document = Assert.Single(result.Documents);
        Assert.Equal(
            "Alpha beta gamma text",
            ContentFieldReader.GetText(document.CustomFields, "summary"));
        Assert.True(handler.ChildrenRequests >= 1);
    }

    [Fact]
    public async Task LoadRawAsync_AutoSummary_PrefetchedBodyIsFetchedOnce()
    {
        var options = CreateAutoSummaryOptions();
        var handler = new AutoSummaryBlocksHandler();
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);
        var requestsAfterLoad = handler.TotalRequests;

        var body = await result.BodyStore.GetAsync(document);

        Assert.Contains("Alpha beta gamma text", body.Html);
        Assert.Equal(requestsAfterLoad, handler.TotalRequests);
    }

    [Fact]
    public async Task LoadRawAsync_AutoSummary_CollectionCopiesShareImmutableValue()
    {
        var options = CreateAutoSummaryOptions();
        var handler = new AutoSummaryBlocksHandler();
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);
        Assert.NotNull(document.CustomFields);
        var summaryBefore = document.CustomFields!["summary"];

        await result.BodyStore.GetAsync(document);

        // Reading the body must not replace or mutate the published field snapshot.
        Assert.Same(summaryBefore, document.CustomFields["summary"]);
        Assert.Equal("Alpha beta gamma text", (string)summaryBefore.Value!);
    }

    [Fact]
    public async Task LoadRawAsync_RenderContentFalse_DoesNotPrefetchOrSummarize()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db-123",
            Token = "token",
            FilterType = "none",
            FieldPolicyMode = "all",
            RenderContent = false,
            AutoSummary = true,
            AutoSummaryMaxLength = 42
        };
        var handler = new AutoSummaryBlocksHandler();
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var result = await source.LoadRawAsync();

        var document = Assert.Single(result.Documents);
        Assert.False(ContentFieldReader.TryGetField(document.CustomFields, "summary", out _));
        Assert.Equal(0, handler.ChildrenRequests);
    }

    [Fact]
    public async Task PageCache_CancelDuringWrite_PreservesPreviousValidJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-notion-cache-" + Guid.NewGuid().ToString("N"));
        var pagesDir = Path.Combine(root, "pages");
        Directory.CreateDirectory(pagesDir);
        var cachePath = Path.Combine(pagesDir, "page-x.json");
        var existing = """{"version":1,"lastEditedTime":"t0","html":"<p>old</p>"}""";
        await File.WriteAllTextAsync(cachePath, existing);
        AtomicNotionCacheWriter.BeforeReplaceHook = _ => throw new OperationCanceledException();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                AtomicNotionCacheWriter.WriteJsonAsync(
                    cachePath,
                    Encoding.UTF8.GetBytes("""{"version":1,"lastEditedTime":"t1","html":"<p>new</p>"}"""),
                    CancellationToken.None));

            var content = await File.ReadAllTextAsync(cachePath);
            Assert.Equal(existing, content);
            using var doc = JsonDocument.Parse(content);
            Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
        }
        finally
        {
            AtomicNotionCacheWriter.BeforeReplaceHook = null;
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CacheWriteFailure_RemovesTemporaryFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-notion-cache-" + Guid.NewGuid().ToString("N"));
        var pagesDir = Path.Combine(root, "pages");
        Directory.CreateDirectory(pagesDir);
        var cachePath = Path.Combine(pagesDir, "page-y.json");
        AtomicNotionCacheWriter.BeforeReplaceHook = _ => throw new InvalidOperationException("simulated crash");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AtomicNotionCacheWriter.WriteJsonAsync(
                    cachePath,
                    Encoding.UTF8.GetBytes("""{"version":1}"""),
                    CancellationToken.None));

            Assert.DoesNotContain(
                Directory.EnumerateFiles(pagesDir),
                file => Path.GetFileName(file).Contains(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            AtomicNotionCacheWriter.BeforeReplaceHook = null;
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RelationCache_ConcurrentWriters_LeaveOneCompleteDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-notion-cache-" + Guid.NewGuid().ToString("N"));
        var cacheA = NotionRelationTargetCache.Create("readwrite", root);
        var cacheB = NotionRelationTargetCache.Create("readwrite", root);
        Assert.NotNull(cacheA);
        Assert.NotNull(cacheB);
        try
        {
            var writes = new List<Task>();
            for (var i = 0; i < 8; i++)
            {
                var cache = i % 2 == 0 ? cacheA! : cacheB!;
                var index = i;
                writes.Add(cache.WriteAsync(
                    new RelationTargetInfo("p-conc", $"Title {index}", "slug", "page", null),
                    CancellationToken.None));
            }

            await Task.WhenAll(writes);

            var path = Path.Combine(root, "relations", "p-conc.json");
            var content = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(content);
            Assert.Equal(2, doc.RootElement.GetProperty("version").GetInt32());
            Assert.Equal("p-conc", doc.RootElement.GetProperty("pageId").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static NotionContentSourceOptions CreateAutoSummaryOptions() => new()
    {
        DatabaseId = "db-123",
        Token = "token",
        FilterType = "none",
        FieldPolicyMode = "all",
        RenderContent = true,
        AutoSummary = true,
        AutoSummaryMaxLength = 42
    };

    private sealed class RepeatingCursorQueryHandler : HttpMessageHandler
    {
        public int QueryCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            QueryCount++;
            var body = """{"has_more":true,"next_cursor":"cursor-loop","results":[]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class AutoSummaryBlocksHandler : HttpMessageHandler
    {
        public int TotalRequests { get; private set; }
        public int ChildrenRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            TotalRequests++;
            string body;
            if (request.Method == HttpMethod.Post)
            {
                body = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Alpha" }]
                        }
                      }
                    }
                  ]
                }
                """;
            }
            else
            {
                ChildrenRequests++;
                body = """
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "paragraph": {
                        "rich_text": [{ "plain_text": "Alpha beta gamma text" }]
                      }
                    }
                  ]
                }
                """;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class SourceHandler : HttpMessageHandler
    {
        internal List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method.Method} {request.RequestUri}");
            var body = request.Method == HttpMethod.Get
                ? """{ "properties": { "Title": {}, "Slug": {}, "Type": {} } }"""
                : """
                  {
                    "has_more": false,
                    "results": [{
                      "id": "page-1",
                      "created_time": "2026-07-22T00:00:00Z",
                      "last_edited_time": "2026-07-22T01:00:00Z",
                      "properties": {
                        "Title": { "type": "title", "title": [{ "plain_text": "Adapter page" }] },
                        "Slug": { "type": "rich_text", "rich_text": [{ "plain_text": "adapter-page" }] },
                        "Type": { "type": "select", "select": { "name": "article" } },
                        "Language": { "type": "select", "select": { "name": "en" } }
                      }
                    }]
                  }
                  """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }

    private sealed class SingleResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
