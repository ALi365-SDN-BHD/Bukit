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
            new HttpClient(handler),
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
            new HttpClient(new CancelingHandler()),
            static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.LoadRawAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task PageQuery_PreservesEngineTitleFragmentSpacing()
    {
        using var http = new HttpClient(new SingleResponseHandler("""
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
            """));
        using var client = new Bukit.Notion.Transport.NotionClient(
            new Bukit.Notion.Transport.NotionClientOptions { Token = "token", MaxRetries = 0 },
            http);

        var page = await NotionPageQuery.FetchAsync(client, "page-1", CancellationToken.None);

        Assert.Equal("First Second", page.Title);
        Assert.Equal("first-second", page.Slug);
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
