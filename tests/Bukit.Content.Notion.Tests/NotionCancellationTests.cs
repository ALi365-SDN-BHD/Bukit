#pragma warning disable CS0618 // Test-only use of obsolete injected-HttpClient constructor
using System.Net;
using System.Text;
using Bukit.Engine.Abstractions.Content;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class NotionCancellationTests
{
    [Fact]
    public async Task NotionContentClient_WithArbitraryHttpClient_FailsClosedBeforeSending()
    {
        var handler = new StaticJsonHandler();
        using var http = new HttpClient(handler);
        using var client = new NotionContentClient(
            new NotionContentSourceOptions
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
    }

    [Fact]
    public async Task NotionContentClient_HandlerSeam_DoesNotFollowRedirect()
    {
        var handler = new RecordingHandler(HttpStatusCode.Redirect);
        using var client = CreateHandlerClient(handler);

        await Assert.ThrowsAsync<ContentException>(() => client.GetAsync(
            "https://api.notion.com/v1/databases/db",
            CancellationToken.None));

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("http://api.notion.com/v1/databases/db")]
    [InlineData("https://example.com/v1/databases/db")]
    public async Task NotionContentClient_HandlerSeam_RejectsNonCanonicalUriBeforeHandler(string url)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = CreateHandlerClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAsync(url, CancellationToken.None));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task NotionContentClient_HandlerSeam_RejectsMismatchedHostBeforeHandler()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
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
    public async Task NotionContentClient_HandlerSeam_SendsCanonicalAuthorizedRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
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
    public async Task RelationResolver_PropagatesCancellationDuringRequest()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db",
            Token = "token",
            MaxRetries = 0
        };
        using var client = new NotionContentClient(
            options,
            new BlockingHandler(requestStarted),
            static (_, _) => Task.CompletedTask);
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = new("relation", new[] { "related-page" })
        };
        var drafts = new[]
        {
            new NotionContentSource.PageDraft(
                "source-page",
                "Source",
                "source",
                "article",
                DateTimeOffset.UtcNow,
                null,
                fields,
                ["tags"])
        };
        using var cancellation = new CancellationTokenSource();

        var resolveTask = NotionRelationResolver.ResolveMissingTaxonomyRelationTargetsAsync(
            client,
            drafts,
            new Dictionary<string, RelationTargetInfo>(),
            relationTargetCache: null,
            renderConcurrency: 1,
            logger: null,
            cancellation.Token);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolveTask);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task ReadonlyPageCache_PropagatesCallerCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bukit-notion-cache-{Guid.NewGuid():N}");
        var pages = Path.Combine(root, "pages");
        Directory.CreateDirectory(pages);
        await File.WriteAllTextAsync(
            Path.Combine(pages, "page.json"),
            "{\"version\":1,\"lastEditedTime\":\"v1\",\"html\":\"<p>cached</p>\"}");
        try
        {
            var staticHandler = new StaticJsonHandler();
            using var transport = new NotionClient(
                new NotionClientOptions { Token = "token", MaxRetries = 0 },
                staticHandler,
                static (_, _) => Task.CompletedTask,
                static () => DateTimeOffset.UtcNow);
            var renderer = new NotionBlocksRenderer(transport);
            var cache = new NotionCacheManager.PageHtmlCache("readonly", root, pages);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                NotionCacheManager.GetOrRenderPageHtmlAsync(
                    renderer,
                    cache,
                    "page",
                    "v1",
                    cancellation.Token));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RelationTargetCache_PropagatesCallerCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bukit-notion-relations-{Guid.NewGuid():N}");
        var relations = Path.Combine(root, "relations");
        Directory.CreateDirectory(relations);
        await File.WriteAllTextAsync(
            Path.Combine(relations, "page.json"),
            "{\"version\":1,\"pageId\":\"page\",\"title\":\"Page\",\"slug\":\"page\",\"type\":\"article\",\"url\":null}");
        try
        {
            var cache = Assert.IsType<NotionRelationTargetCache>(
                NotionRelationTargetCache.Create("readonly", root));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                cache.TryReadAsync("page", cancellation.Token));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class BlockingHandler(TaskCompletionSource requestStarted) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            requestStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
    }

    private static NotionContentClient CreateHandlerClient(HttpMessageHandler handler)
        => new(
            new NotionContentSourceOptions
            {
                DatabaseId = "db",
                Token = "token",
                MaxRetries = 0
            },
            handler,
            static (_, _) => Task.CompletedTask);

    private sealed class RecordingHandler(HttpStatusCode responseStatus) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(responseStatus)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
            if ((int)responseStatus is >= 300 and < 400)
            {
                response.Headers.Location = new Uri("https://example.com/steal");
            }

            return response;
        }
    }
}
