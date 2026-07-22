using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionDatabaseSchemaResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenNoSchemaDependentOptions_ReturnsNullsWithoutRequest()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db",
            Token = "token",
            FilterType = "none"
        };
        var handler = new JsonHandler("{}");
        using var client = CreateClient(options, handler);

        var resolved = await NotionDatabaseSchemaResolver.ResolveAsync(client, options, CancellationToken.None);

        Assert.Null(resolved.FilterProperty);
        Assert.Null(resolved.SortProperty);
        Assert.Null(resolved.IncludeSlugProperty);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_MatchesPropertyNamesCaseInsensitively()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db",
            Token = "token",
            FilterType = "checkbox_true",
            FilterProperty = "published",
            SortProperty = "updated",
            IncludeSlugs = new[] { "about" },
            IncludeSlugProperty = "slug"
        };
        var handler = new JsonHandler("""
        {
          "properties": {
            "Published": {},
            "Updated": {},
            "Slug": {}
          }
        }
        """);
        using var client = CreateClient(options, handler);

        var resolved = await NotionDatabaseSchemaResolver.ResolveAsync(client, options, CancellationToken.None);

        Assert.Equal("Published", resolved.FilterProperty);
        Assert.Equal("Updated", resolved.SortProperty);
        Assert.Equal("Slug", resolved.IncludeSlugProperty);
    }

    [Fact]
    public async Task ResolveAsync_WhenSchemaMissingProperties_Throws()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db",
            Token = "token",
            FilterType = "checkbox_true"
        };
        using var client = CreateClient(options, new JsonHandler("{}"));

        var ex = await Assert.ThrowsAsync<ContentException>(() =>
            NotionDatabaseSchemaResolver.ResolveAsync(client, options, CancellationToken.None));

        Assert.Contains("schema missing properties", ex.Message);
    }

    [Fact]
    public async Task ResolveAsync_WhenPropertyMissing_ListsAvailableProperties()
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "db",
            Token = "token",
            FilterType = "checkbox_true",
            FilterProperty = "Missing"
        };
        using var client = CreateClient(options, new JsonHandler("""
        {
          "properties": {
            "Published": {},
            "Status": {}
          }
        }
        """));

        var ex = await Assert.ThrowsAsync<ContentException>(() =>
            NotionDatabaseSchemaResolver.ResolveAsync(client, options, CancellationToken.None));

        Assert.Contains("property 'Missing' not found", ex.Message);
        Assert.Contains("Published", ex.Message);
        Assert.Contains("Status", ex.Message);
    }

    private static NotionContentClient CreateClient(NotionContentSourceOptions options, HttpMessageHandler handler)
    {
        return new NotionContentClient(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
    }

    private sealed class JsonHandler : HttpMessageHandler
    {
        private readonly string _json;

        public JsonHandler(string json)
        {
            _json = json;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }
}
