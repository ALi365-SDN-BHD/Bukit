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
    public async Task LoadRawAsync_PreservesSplitIdentifiersAndExplicitWhitespace()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db-1", Token = "token", FilterType = "none",
            FieldPolicyMode = "all", RenderContent = false
        };
        var handler = new SingleResponseHandler("""
            {"has_more":false,"results":[{"id":"page-1","created_time":"2026-09-11T00:00:00Z","properties":{
              "Title":{"type":"title","title":[{"plain_text":"Page"}]},
              "Language":{"type":"rich_text","rich_text":[{"plain_text":"zh-"},{"plain_text":"CN"}]},
              "Url":{"type":"rich_text","rich_text":[{"plain_text":"/文"},{"plain_text":"档/"}]},
              "Template":{"type":"rich_text","rich_text":[{"plain_text":"pages/page"},{"plain_text":".html"}]},
              "Summary":{"type":"rich_text","rich_text":[{"plain_text":"First"},{"plain_text":"  "},{"plain_text":"second"}]}
            }}]}
            """);
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);

        var result = await new NotionContentSource(options, logger: null, CreateClient).LoadRawAsync();
        var fields = Assert.Single(result.Documents).CustomFields;

        Assert.Equal("zh-CN", ContentFieldReader.GetText(fields, "language"));
        Assert.Equal("/文档/", ContentFieldReader.GetText(fields, "url"));
        Assert.Equal("pages/page.html", ContentFieldReader.GetText(fields, "template"));
        Assert.Equal("First  second", ContentFieldReader.GetText(fields, "summary"));
    }

    [Fact]
    public async Task PageCache_ReadonlyReusesEmptyBodyWithoutFetchingBlocks()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-empty-cache-" + Guid.NewGuid().ToString("N"));
        var cache = NotionCacheManager.CreatePageHtmlCache(new NotionContentSourceOptions { DatabaseId = "db-1", Token = "token", CacheMode = "readwrite", CacheDir = root });
        var handler = new SingleResponseHandler("""{"results":[],"has_more":false}""");
        using var client = new Bukit.Notion.Transport.NotionClient(
            new Bukit.Notion.Transport.NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler, static (_, _) => Task.CompletedTask, static () => DateTimeOffset.UtcNow);
        var renderer = new Bukit.Notion.Rendering.NotionBlocksRenderer(client);
        try
        {
            Assert.NotNull(cache);
            Assert.Equal(string.Empty, await NotionCacheManager.GetOrRenderPageHtmlAsync(renderer, cache, "page-1", "v1", CancellationToken.None));
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(string.Empty, await NotionCacheManager.GetOrRenderPageHtmlAsync(renderer, cache with { Mode = "readonly" }, "page-1", "v1", CancellationToken.None));
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"html\":null")]
    [InlineData(",\"html\":123")]
    public async Task PageCache_ReadonlyRejectsMissingOrInvalidHtml(string htmlProperty)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-invalid-cache-" + Guid.NewGuid().ToString("N"));
        var cache = NotionCacheManager.CreatePageHtmlCache(new NotionContentSourceOptions { DatabaseId = "db-1", Token = "token", CacheMode = "readonly", CacheDir = root });
        var handler = new SingleResponseHandler("""{"results":[],"has_more":false}""");
        using var client = new Bukit.Notion.Transport.NotionClient(
            new Bukit.Notion.Transport.NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler, static (_, _) => Task.CompletedTask, static () => DateTimeOffset.UtcNow);
        try
        {
            Assert.NotNull(cache);
            Directory.CreateDirectory(cache.PagesDir);
            await File.WriteAllTextAsync(Path.Combine(cache.PagesDir, "page-1.json"), "{\"version\":1,\"lastEditedTime\":\"v1\"" + htmlProperty + "}");
            await Assert.ThrowsAsync<Bukit.Shared.ContentException>(() => NotionCacheManager.GetOrRenderPageHtmlAsync(
                new Bukit.Notion.Rendering.NotionBlocksRenderer(client), cache, "page-1", "v1", CancellationToken.None));
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    [Fact]
    public async Task LoadRawAsync_UsesOneClientForQueryAndBodyConcurrencyGate()
    {
        var handler = new GatedBodyHandler(pageCount: 4, releaseImmediately: false);
        var created = 0;
        NotionContentClient CreateClient()
        {
            Interlocked.Increment(ref created);
            return new(options: Options(renderConcurrency: 2), handler, static (_, _) => Task.CompletedTask);
        }

        var source = new NotionContentSource(Options(renderConcurrency: 2), logger: null, CreateClient);
        var result = await source.LoadRawAsync();

        Assert.Equal(1, created);
        Assert.Equal(4, result.Documents.Count);
        var reads = result.Documents.Select(document => result.BodyStore.GetAsync(document)).ToArray();
        await handler.AtCapacity.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(handler.MaxActive <= 2, $"peak {handler.MaxActive}");
        handler.Release();
        await Task.WhenAll(reads).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, handler.MaxActive);
        Assert.Equal(4, handler.BlockRequests);
        Assert.Equal(1, created);
    }

    [Fact]
    public async Task LoadRawAsync_AutoSummaryPrefetchSharesClientAndSeedsBodies()
    {
        var handler = new GatedBodyHandler(pageCount: 2, releaseImmediately: true);
        var created = 0;
        var options = Options(renderConcurrency: 2) with { AutoSummary = true, AutoSummaryMaxLength = 100 };
        NotionContentClient CreateClient()
        {
            Interlocked.Increment(ref created);
            return new(options, handler, static (_, _) => Task.CompletedTask);
        }

        var source = new NotionContentSource(options, logger: null, CreateClient);
        var result = await source.LoadRawAsync();

        Assert.Equal(1, created);
        Assert.Equal(2, handler.BlockRequests);
        foreach (var document in result.Documents)
        {
            var body = await result.BodyStore.GetAsync(document);
            Assert.False(string.IsNullOrWhiteSpace(body.Html));
            Assert.False(string.IsNullOrWhiteSpace(ContentFieldReader.GetText(document.CustomFields, "summary")));
        }

        Assert.Equal(2, handler.BlockRequests);
        Assert.Equal(1, created);
    }

    [Fact]
    public async Task BodyGate_CanceledQueuedWaiter_DoesNotLeakPermit()
    {
        var handler = new GatedBodyHandler(pageCount: 3, releaseImmediately: false);
        var source = new NotionContentSource(Options(renderConcurrency: 1), logger: null,
            () => new NotionContentClient(Options(renderConcurrency: 1), handler, static (_, _) => Task.CompletedTask));
        var result = await source.LoadRawAsync();

        var first = result.BodyStore.GetAsync(result.Documents[0]);
        await handler.FirstBlockStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var queued = result.BodyStore.GetAsync(result.Documents[1], cancellation.Token);
        await Task.Delay(100);
        Assert.False(queued.IsCompleted, "second read should be waiting on the body gate");
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        handler.Release();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        var third = result.BodyStore.GetAsync(result.Documents[2]);
        var fourth = await third.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(fourth.Html));
        Assert.Equal(1, handler.MaxActive);
    }

    [Fact]
    public async Task BodyGate_RenderFailureAfterPermit_NextReadUsesTheReleasedPermit()
    {
        var handler = new FailOnceBodyHandler();
        var options = Options(renderConcurrency: 1) with { MaxRetries = 0 };
        var source = new NotionContentSource(options, logger: null,
            () => new NotionContentClient(options, handler, static (_, _) => Task.CompletedTask));
        var result = await source.LoadRawAsync();
        var document = result.Documents[0];

        await Assert.ThrowsAnyAsync<Exception>(() => result.BodyStore.GetAsync(document));
        var recovered = await result.BodyStore.GetAsync(document).WaitAsync(TimeSpan.FromSeconds(5));
        var other = await result.BodyStore.GetAsync(result.Documents[1]).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(string.IsNullOrWhiteSpace(recovered.Html));
        Assert.False(string.IsNullOrWhiteSpace(other.Html));
        Assert.Equal(1, handler.MaxActive);
    }

    private sealed class FailOnceBodyHandler : HttpMessageHandler
    {
        private int _blockRequests;
        private int _active;
        private int _maxActive;

        public int MaxActive => Volatile.Read(ref _maxActive);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Get && url == "https://api.notion.com/v1/databases/db-1")
            {
                return Task.FromResult(Json("""{"properties":{"Title":{},"Slug":{},"Type":{},"PublishAt":{}}}"""));
            }

            if (request.Method == HttpMethod.Post && url == "https://api.notion.com/v1/databases/db-1/query")
            {
                return Task.FromResult(Json("""
                {"has_more":false,"results":[
                  {"id":"page-1","created_time":"2026-01-02T03:04:05.000Z","last_edited_time":"2026-05-15T12:00:00.000Z","properties":{"Title":{"type":"title","title":[{"plain_text":"One"}]},"Slug":{"type":"rich_text","rich_text":[{"plain_text":"one"}]},"Type":{"type":"select","select":{"name":"page"}},"PublishAt":{"type":"date","date":{"start":"2026-05-15T10:30:00+00:00"}}}},
                  {"id":"page-2","created_time":"2026-01-02T03:04:05.000Z","last_edited_time":"2026-05-15T12:00:00.000Z","properties":{"Title":{"type":"title","title":[{"plain_text":"Two"}]},"Slug":{"type":"rich_text","rich_text":[{"plain_text":"two"}]},"Type":{"type":"select","select":{"name":"page"}},"PublishAt":{"type":"date","date":{"start":"2026-05-15T10:30:00+00:00"}}}}
                ]}
                """));
            }

            if (request.Method == HttpMethod.Get && url.Contains("/v1/blocks/", StringComparison.Ordinal))
            {
                var active = Interlocked.Increment(ref _active);
                UpdateMax(active);
                try
                {
                    if (Interlocked.Increment(ref _blockRequests) == 1)
                    {
                        throw new HttpRequestException("first render fails");
                    }

                    return Task.FromResult(Json("""
                    {"results":[{"type":"paragraph","paragraph":{"rich_text":[{"plain_text":"Recovered body"}]}}],"has_more":false}
                    """));
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }
            }

            return Task.FromResult(Json("{}"));
        }

        private void UpdateMax(int active)
        {
            while (true)
            {
                var observed = Volatile.Read(ref _maxActive);
                if (active <= observed || Interlocked.CompareExchange(ref _maxActive, active, observed) == observed)
                {
                    return;
                }
            }
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task LoadRawAsync_SeparateLoadsOwnIndependentResources()
    {
        var handler = new GatedBodyHandler(pageCount: 1, releaseImmediately: true);
        var created = 0;
        var options = Options(renderConcurrency: 2);
        NotionContentClient CreateClient()
        {
            Interlocked.Increment(ref created);
            return new(options, handler, static (_, _) => Task.CompletedTask);
        }

        var source = new NotionContentSource(options, logger: null, CreateClient);
        var first = await source.LoadRawAsync();
        var second = await source.LoadRawAsync();
        Assert.Equal(2, created);

        await ((IAsyncDisposable)first.BodyStore).DisposeAsync();

        var body = await second.BodyStore.GetAsync(second.Documents[0]).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(body.Html));
    }

    private static NotionContentSourceOptions Options(int renderConcurrency)
        => new()
        {
            DatabaseId = "db-1",
            Token = "token",
            FilterType = "none",
            FieldPolicyMode = "all",
            RenderContent = true,
            AutoSummary = false,
            RenderConcurrency = renderConcurrency,
            MaxRps = 1000,
            MaxRetries = 0,
            RequestDelayMs = 0
        };

    private sealed class GatedBodyHandler(int pageCount, bool releaseImmediately) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _atCapacity = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _maxActive;
        private int _blockRequests;

        private int _disposeCount;

        public int MaxActive => Volatile.Read(ref _maxActive);
        public int BlockRequests => Volatile.Read(ref _blockRequests);
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public override string ToString() => "GatedBodyHandler";
        public Task AtCapacity => _atCapacity.Task;
        public TaskCompletionSource FirstBlockStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _released.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Get && url == "https://api.notion.com/v1/databases/db-1")
            {
                return Json("""
                {"properties":{"Title":{},"Slug":{},"Type":{},"PublishAt":{}}}
                """);
            }

            if (request.Method == HttpMethod.Post && url == "https://api.notion.com/v1/databases/db-1/query")
            {
                var pages = string.Join(",", Enumerable.Range(1, pageCount).Select(index => $$"""
                    {
                      "id": "page-{{index}}",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "last_edited_time": "2026-05-15T12:00:00.000Z",
                      "properties": {
                        "Title": { "type": "title", "title": [{ "plain_text": "Page {{index}}" }] },
                        "Slug": { "type": "rich_text", "rich_text": [{ "plain_text": "page-{{index}}" }] },
                        "Type": { "type": "select", "select": { "name": "page" } },
                        "PublishAt": { "type": "date", "date": { "start": "2026-05-15T10:30:00+00:00" } }
                      }
                    }
                    """));
                return Json($$"""{"has_more":false,"results":[{{pages}}]}""");
            }

            if (request.Method == HttpMethod.Get && url.Contains("/v1/blocks/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _blockRequests);
                FirstBlockStarted.TrySetResult();
                var active = Interlocked.Increment(ref _active);
                UpdateMax(active);
                if (active >= 2)
                {
                    _atCapacity.TrySetResult();
                }

                if (!releaseImmediately)
                {
                    await _released.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }

                Interlocked.Decrement(ref _active);
                return Json("""
                {"results":[{"type":"paragraph","paragraph":{"rich_text":[{"plain_text":"Body text"}]}}],"has_more":false}
                """);
            }

            return Json("{}");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposeCount);
            }

            base.Dispose(disposing);
        }

        private void UpdateMax(int active)
        {
            while (true)
            {
                var observed = Volatile.Read(ref _maxActive);
                if (active <= observed || Interlocked.CompareExchange(ref _maxActive, active, observed) == observed)
                {
                    return;
                }
            }
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task LoadRawAsync_BodyFingerprintTracksEffectiveRenderContentMode()
    {
        static NotionContentSourceOptions CreateOptions(bool renderContent) => new()
        {
            DatabaseId = "db-1",
            Token = "token",
            FilterType = "none",
            FieldPolicyMode = "all",
            RenderContent = renderContent
        };

        async Task<string> LoadFingerprintAsync(bool renderContent)
        {
            var options = CreateOptions(renderContent);
            var handler = new SourceHandler();
            NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
            var source = new NotionContentSource(options, logger: null, CreateClient);
            var result = await source.LoadRawAsync();
            return ContentFieldReader.GetText(result.Documents.Single().CustomFields, "bodyFingerprint")!;
        }

        var enabled = await LoadFingerprintAsync(true);
        var enabledAgain = await LoadFingerprintAsync(true);
        var disabled = await LoadFingerprintAsync(false);
        var disabledAgain = await LoadFingerprintAsync(false);

        Assert.Equal(enabled, enabledAgain);
        Assert.Equal(disabled, disabledAgain);
        Assert.EndsWith(":content=on", enabled, StringComparison.Ordinal);
        Assert.EndsWith(":content=off", disabled, StringComparison.Ordinal);
        Assert.NotEqual(enabled, disabled);
    }

    [Fact]
    public async Task LoadRawAsync_RateLimitPermitsContinueAcrossPages()
    {
        var clock = new FakeClock();
        var permits = new List<(DateTimeOffset At, int DelayMs)>();
        var gate = new object();
        Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                permits.Add((clock.Now, milliseconds));
                clock.Advance(milliseconds);
            }

            return Task.CompletedTask;
        }

        var handler = new GatedBodyHandler(pageCount: 4, releaseImmediately: true);
        var options = Options(renderConcurrency: 2) with { MaxRps = 2 };
        var created = 0;
        NotionContentClient CreateClient()
        {
            Interlocked.Increment(ref created);
            return new NotionContentClient(options, handler, DelayAsync, () => clock.Now);
        }

        var source = new NotionContentSource(options, logger: null, CreateClient);
        var result = await source.LoadRawAsync();
        await Task.WhenAll(result.Documents.Select(document => result.BodyStore.GetAsync(document)));

        Assert.Equal(1, created);
        // query + four block reads share one client; the first request is immediate and
        // every later permit waits and lands on a strictly later timeline point.
        Assert.Equal(4, permits.Count);
        Assert.All(permits, permit => Assert.True(permit.DelayMs > 0));
        for (var i = 1; i < permits.Count; i++)
        {
            Assert.True(permits[i].At > permits[i - 1].At,
                $"permit {i} at {permits[i].At:O} did not advance past {permits[i - 1].At:O}");
        }

        Assert.True(permits[^1].At - permits[0].At >= TimeSpan.FromMilliseconds(1500));
    }

    [Fact]
    public async Task LoadRawAsync_SuccessDisposesClientExactlyOnceAfterStoreRelease()
    {
        var handler = new GatedBodyHandler(pageCount: 1, releaseImmediately: true);
        var options = Options(renderConcurrency: 2);
        var source = new NotionContentSource(options, logger: null,
            () => new NotionContentClient(options, handler, static (_, _) => Task.CompletedTask));

        var result = await source.LoadRawAsync();

        Assert.Equal(0, handler.DisposeCount);
        await ((IAsyncDisposable)result.BodyStore).DisposeAsync();
        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public async Task LoadRawAsync_FailureDisposesClientExactlyOnce()
    {
        var handler = new CountingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"message\":\"bad\"}", Encoding.UTF8, "application/json")
        }));
        var options = Options(renderConcurrency: 2) with { MaxRetries = 0 };
        var source = new NotionContentSource(options, logger: null,
            () => new NotionContentClient(options, handler, static (_, _) => Task.CompletedTask));

        await Assert.ThrowsAnyAsync<Exception>(() => source.LoadRawAsync());

        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public async Task CompositeLoadFailure_DisposesNotionClientExactlyOnce()
    {
        var handler = new GatedBodyHandler(pageCount: 1, releaseImmediately: true);
        var options = Options(renderConcurrency: 2);
        var notion = new NotionContentSource(options, logger: null,
            () => new NotionContentClient(options, handler, static (_, _) => Task.CompletedTask));
        var failing = new FailingProvider();
        var composite = new CompositeContentProvider(new (string, string, IContentProvider)[]
        {
            ("notion", "content", notion),
            ("broken", "content", failing)
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => composite.LoadRawAsync());

        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public async Task CompositeChain_QueryTaxonomyPrefetchFallbackAndLazyBodyShareOneClientBudget()
    {
        var clock = new FakeClock();
        var permits = new List<DateTimeOffset>();
        var gate = new object();
        Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                permits.Add(clock.Now);
                clock.Advance(milliseconds);
            }

            return Task.CompletedTask;
        }

        var handler = new StageChainHandler();
        var options = Options(renderConcurrency: 2) with
        {
            AutoSummary = true,
            AutoSummaryMaxLength = 100,
            MaxRps = 20
        };
        var created = 0;
        NotionContentClient CreateClient()
        {
            Interlocked.Increment(ref created);
            return new NotionContentClient(options, handler, DelayAsync, () => clock.Now);
        }

        var source = new NotionContentSource(options, logger: null, CreateClient);
        var schema = new ContentModelSchema(
            RelationMappings: new Dictionary<string, RelationMapping>
            {
                ["related"] = new RelationMapping("Related", "relation", Reference: new ContentReferenceRule(IdField: "id"))
            });
        var composite = new CompositeContentProvider(
            new (string, string, IContentProvider)[] { ("notion", "content", source) },
            schema);

        var result = await composite.LoadRawAsync();
        var documents = result.Documents;
        var lazy = documents.Single(document => document.Id.EndsWith("lazy-page", StringComparison.Ordinal));
        var lazyBody = await result.BodyStore.GetAsync(lazy);
        await ((IAsyncDisposable)result.BodyStore).DisposeAsync();

        Assert.Equal(1, created);
        Assert.Equal(1, handler.DisposeCount);
        Assert.Contains("POST https://api.notion.com/v1/databases/db-1/query", handler.Requests);
        Assert.Contains("GET https://api.notion.com/v1/pages/taxonomy-target", handler.Requests);
        Assert.Contains("GET https://api.notion.com/v1/blocks/stage-page/children?page_size=100", handler.Requests);
        Assert.Contains("GET https://api.notion.com/v1/pages/schema-target", handler.Requests);
        Assert.Contains("GET https://api.notion.com/v1/blocks/lazy-page/children?page_size=100", handler.Requests);
        Assert.Contains("Stage body", lazyBody.Html, StringComparison.Ordinal);
        Assert.True(permits.Count >= 3, $"expected multi-stage permits, got {permits.Count}");
        for (var i = 1; i < permits.Count; i++)
        {
            Assert.True(permits[i] >= permits[i - 1]);
        }

        Assert.True(permits[^1] > permits[0]);
    }

    private sealed class StageChainHandler : HttpMessageHandler
    {
        private int _disposeCount;

        public List<string> Requests { get; } = [];

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add($"{request.Method.Method} {url}");
            if (request.Method == HttpMethod.Get && url == "https://api.notion.com/v1/databases/db-1")
            {
                return Task.FromResult(Json("""
                {"properties":{"Title":{},"Slug":{},"Type":{},"PublishAt":{},"Tags":{},"Related":{}}}
                """));
            }

            if (request.Method == HttpMethod.Post && url == "https://api.notion.com/v1/databases/db-1/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "stage-page",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "last_edited_time": "2026-05-15T12:00:00.000Z",
                      "properties": {
                        "Title": { "type": "title", "title": [{ "plain_text": "Stage Page" }] },
                        "Slug": { "type": "rich_text", "rich_text": [{ "plain_text": "stage-page" }] },
                        "Type": { "type": "select", "select": { "name": "page" } },
                        "PublishAt": { "type": "date", "date": { "start": "2026-05-15T10:30:00+00:00" } },
                        "Tags": { "type": "relation", "relation": [{ "id": "taxonomy-target" }] },
                        "Related": { "type": "relation", "relation": [{ "id": "schema-target" }] }
                      }
                    },
                    {
                      "id": "lazy-page",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "last_edited_time": "2026-05-16T12:00:00.000Z",
                      "properties": {
                        "Title": { "type": "title", "title": [{ "plain_text": "Lazy Page" }] },
                        "Slug": { "type": "rich_text", "rich_text": [{ "plain_text": "lazy-page" }] },
                        "Type": { "type": "select", "select": { "name": "page" } },
                        "PublishAt": { "type": "date", "date": { "start": "2026-05-16T10:30:00+00:00" } },
                        "Summary": { "type": "rich_text", "rich_text": [{ "plain_text": "Preset summary" }] }
                      }
                    }
                  ]
                }
                """));
            }

            if (request.Method == HttpMethod.Get && url.Contains("/v1/pages/", StringComparison.Ordinal))
            {
                return Task.FromResult(Json("""
                {"properties":{"Title":{"type":"title","title":[{"plain_text":"Target"}]},"Slug":{"type":"rich_text","rich_text":[{"plain_text":"target"}]},"Type":{"type":"select","select":{"name":"page"}}}}
                """));
            }

            if (request.Method == HttpMethod.Get && url.Contains("/v1/blocks/", StringComparison.Ordinal))
            {
                return Task.FromResult(Json("""
                {"results":[{"type":"paragraph","paragraph":{"rich_text":[{"plain_text":"Stage body"}]}}],"has_more":false}
                """));
            }

            return Task.FromResult(Json("{}"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposeCount);
            }

            base.Dispose(disposing);
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task CompositeProjectionFailure_DisposesNotionClientExactlyOnce()
    {
        var handler = new GatedBodyHandler(pageCount: 1, releaseImmediately: true);
        var options = Options(renderConcurrency: 2);
        var notion = new NotionContentSource(options, logger: null,
            () => new NotionContentClient(options, handler, static (_, _) => Task.CompletedTask));
        var schema = new ContentModelSchema(
            RelationMappings: new Dictionary<string, RelationMapping>
            {
                ["related"] = new("Related", "relation")
            });
        var composite = new CompositeContentProvider(
            new (string, string, IContentProvider)[] { ("notion", "content", new TokenIgnoringProvider(notion)) },
            schema);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => composite.LoadRawAsync(cancellation.Token));

        Assert.Equal(1, handler.DisposeCount);
    }

    private sealed class TokenIgnoringProvider(IContentProvider inner) : IContentProvider
    {
        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            => inner.LoadRawAsync(CancellationToken.None);
    }

    private sealed class CountingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref _disposeCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => send(request, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposeCount);
            }

            base.Dispose(disposing);
        }
    }

    private sealed class FailingProvider : IContentProvider
    {
        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("provider failed");
    }

    private sealed class FakeClock
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public DateTimeOffset Now => _now;
        public void Advance(int milliseconds) => _now = _now.AddMilliseconds(milliseconds);
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
        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
