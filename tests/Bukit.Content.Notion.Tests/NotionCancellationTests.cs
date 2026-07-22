using System.Net;
using System.Text;
using Bukit.Engine.Abstractions.Content;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class NotionCancellationTests
{
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
        using var http = new HttpClient(new BlockingHandler(requestStarted));
        using var client = new NotionContentClient(options, http, static (_, _) => Task.CompletedTask);
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
            using var http = new HttpClient(new StaticJsonHandler());
            using var transport = new NotionClient(
                new NotionClientOptions { Token = "token", MaxRetries = 0 },
                http);
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
}
