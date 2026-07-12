using System.Net;
using System.Text;
using Bukit.Config;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionCanonicalProjectionTests
{
    [Fact]
    public async Task LoadRawAsync_WithMappedCanonicalProperties_ProjectsCanonicalKeys()
    {
        var item = await LoadSingleAsync("""
            "Original URL": { "type": "url", "url": "https://source.example/item" },
            "References": { "type": "rich_text", "rich_text": [{ "plain_text": "https://ref.example/a" }] },
            "Hero Image": { "type": "files", "files": [{ "type": "external", "name": "hero", "external": { "url": "https://img.example/hero.jpg" } }] },
            "Hero Alt": { "type": "rich_text", "rich_text": [{ "plain_text": "Detailed hero" }] },
            "Hero Caption": { "type": "rich_text", "rich_text": [{ "plain_text": "Hero caption" }] },
            "Structured Entities": { "type": "rich_text", "rich_text": [{ "plain_text": "[{\"type\":\"organization\",\"name\":\"Bukit\",\"description\":\"Static site generator\"}]" }] }
            """);

        Assert.Equal("https://source.example/item", ContentFieldReader.GetText(item.CustomFields, "original_url"));
        Assert.True(ContentFieldReader.TryGetField(item.CustomFields, "references", out var referencesField));
        Assert.Equal(new[] { "https://ref.example/a" }, Assert.IsAssignableFrom<IEnumerable<string>>(referencesField.Value));
        Assert.Equal("https://img.example/hero.jpg", ContentFieldReader.GetText(item.CustomFields, "cover"));
        Assert.Equal("Detailed hero", ContentFieldReader.GetText(item.CustomFields, "cover_alt"));
        Assert.Equal("Hero caption", ContentFieldReader.GetText(item.CustomFields, "cover_caption"));

        Assert.True(ContentFieldReader.TryGetField(item.CustomFields, "entities", out var entitiesField));
        var entities = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(entitiesField.Value);
        var entity = Assert.Single(entities);
        Assert.Equal("organization", entity["type"]);
        Assert.Equal("Bukit", entity["name"]);
        Assert.Equal("Static site generator", entity["description"]);
    }

    [Theory]
    [InlineData("{}", "array")]
    [InlineData("[\"Bukit\"]", "object")]
    [InlineData("[{\"type\":\"organization\",\"name\":\"Bukit\",\"description\":\"\"}]", "description")]
    public async Task LoadRawAsync_WithInvalidEntitiesJson_ThrowsContextualError(string entitiesJson, string expected)
    {
        var ex = await Assert.ThrowsAsync<ContentException>(() => LoadSingleAsync($$"""
            "Structured Entities": { "type": "rich_text", "rich_text": [{ "plain_text": {{System.Text.Json.JsonSerializer.Serialize(entitiesJson)}} }] }
            """));

        Assert.Contains("page-canonical", ex.Message);
        Assert.Contains("Structured Entities", ex.Message);
        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadRawAsync_StructuredEntityWinsOverDescriptionlessNormalizedMultiSelectEntity()
    {
        var item = await LoadSingleAsync("""
            "Entities": { "type": "multi_select", "multi_select": [{ "name": "Bukit" }] },
            "Structured Entities": { "type": "rich_text", "rich_text": [{ "plain_text": "[{\"type\":\"organization\",\"name\":\"Bukit\",\"description\":\"Canonical description\"}]" }] }
            """);

        Assert.True(ContentFieldReader.TryGetField(item.CustomFields, "entities", out var entitiesField));
        var entities = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(entitiesField.Value);
        var entity = Assert.Single(entities);
        Assert.Equal("Canonical description", entity["description"]);
    }

    [Theory]
    [InlineData("Original URL", "rich_text", "url")]
    [InlineData("References", "url", "multi_select, rich_text")]
    [InlineData("Structured Entities", "multi_select", "rich_text")]
    [InlineData("Hero Image", "select", "rich_text, url, files")]
    [InlineData("Hero Alt", "url", "rich_text")]
    [InlineData("Hero Caption", "select", "rich_text")]
    public async Task LoadRawAsync_WithIncompatibleMappedPropertyType_ThrowsContractError(
        string propertyName,
        string actualType,
        string allowedTypes)
    {
        var ex = await Assert.ThrowsAsync<ContentException>(() => LoadSingleAsync(
            $"\"{propertyName}\": {BuildProperty(actualType)}"));

        Assert.Contains("page-canonical", ex.Message);
        Assert.Contains(propertyName, ex.Message);
        Assert.Contains(actualType, ex.Message);
        Assert.Contains(allowedTypes, ex.Message);
    }

    [Theory]
    [InlineData("rich_text", "/assets/images/hero.jpg")]
    [InlineData("url", "https://img.example/hero.jpg")]
    public async Task LoadRawAsync_WithSupportedCoverType_ProjectsCover(string notionType, string expected)
    {
        var property = notionType == "rich_text"
            ? "{ \"type\": \"rich_text\", \"rich_text\": [{ \"plain_text\": \"/assets/images/hero.jpg\" }] }"
            : "{ \"type\": \"url\", \"url\": \"https://img.example/hero.jpg\" }";

        var item = await LoadSingleAsync($"\"Hero Image\": {property}");

        Assert.Equal(expected, ContentFieldReader.GetText(item.CustomFields, "cover"));
    }

    private static async Task<RawContentDocument> LoadSingleAsync(string extraProperties)
    {
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-canonical",
            Token = "token",
            FilterType = "none",
            RequestDelayMs = 0,
            RenderContent = false,
            FieldPolicyMode = "all",
            PropertyMap = new NotionPropertyMapConfig
            {
                OriginalUrl = "Original URL",
                References = "References",
                EntitiesJson = "Structured Entities",
                Cover = "Hero Image",
                CoverAlt = "Hero Alt",
                CoverCaption = "Hero Caption"
            }
        };
        var handler = new CanonicalPageHandler(extraProperties);
        NotionApiClient CreateClient() => new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadRawAsync();

        return Assert.Single(result.Documents);
    }

    private static string BuildProperty(string notionType) => notionType switch
    {
        "rich_text" => "{ \"type\": \"rich_text\", \"rich_text\": [{ \"plain_text\": \"value\" }] }",
        "multi_select" => "{ \"type\": \"multi_select\", \"multi_select\": [{ \"name\": \"value\" }] }",
        "url" => "{ \"type\": \"url\", \"url\": \"https://example.test/value\" }",
        "select" => "{ \"type\": \"select\", \"select\": { \"name\": \"value\" } }",
        _ => throw new ArgumentOutOfRangeException(nameof(notionType), notionType, null)
    };

    private sealed class CanonicalPageHandler(string extraProperties) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Method == HttpMethod.Get
                ? """{ "properties": { "Title": {}, "Slug": {}, "Type": {} } }"""
                : $$"""
                  {
                    "has_more": false,
                    "results": [{
                      "id": "page-canonical",
                      "last_edited_time": "2026-07-12T00:00:00Z",
                      "properties": {
                        "Title": { "type": "title", "title": [{ "plain_text": "Canonical page" }] },
                        "Slug": { "type": "rich_text", "rich_text": [{ "plain_text": "canonical-page" }] },
                        "Type": { "type": "select", "select": { "name": "article" } },
                        {{extraProperties}}
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
}
