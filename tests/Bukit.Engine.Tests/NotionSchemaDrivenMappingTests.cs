using System.Net;
using System.Text;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class NotionSchemaDrivenMappingTests
{
    [Fact]
    public async Task NotionRawInput_ProjectsThroughSchemaDrivenCanonicalMappings()
    {
        var handler = new SchemaMappingNotionHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            RenderContent = false,
            FieldPolicyMode = "all"
        };

        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);

        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadRawAsync();

        var raw = Assert.Single(result.Documents);
        Assert.Equal("notion", raw.Source.Provider);
        Assert.Equal("page-1", raw.Source.SourcePath);
        Assert.Equal("page-1", raw.Body.BodyKey);
        Assert.Null(raw.Body.InlineHtml);
        Assert.NotNull(raw.Properties);
        Assert.Equal("Schema mapped summary", Assert.Contains("abstract", raw.Properties!).Value);
        Assert.Equal("text", Assert.Contains("abstract", raw.Properties!).Kind);
        Assert.Equal("list", Assert.Contains("company_refs", raw.Properties!).Kind);
        Assert.Equal("list", Assert.Contains("reading_refs", raw.Properties!).Kind);

        var schema = new ContentModelSchema(
            CanonicalMappings: new Dictionary<string, CanonicalFieldMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = new("summary", RawKey: "abstract", Required: true)
            },
            EntityMappings: new Dictionary<string, EntityMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["company_refs"] = new(
                    RawKey: "company_refs",
                    EntityType: "company",
                    NameField: "name")
            },
            RelationMappings: new Dictionary<string, RelationMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["reading_refs"] = new(
                    RawKey: "reading_refs",
                    RelationType: "references",
                    TargetType: "content")
            });

        var document = ContentDocumentNormalizer.ToDocument(raw, schema);

        Assert.Equal("Schema mapped summary", document.Record.Presentation.Summary);
        Assert.Equal("Schema mapped summary", ContentFieldReader.GetText(document.CustomFields, "summary"));
        Assert.Equal("post", document.Record.Identity.ContentType);

        var entity = Assert.Single(document.Record.Entities, entity =>
            entity.Type == "company" &&
            entity.Name == "Bukit Labs");
        Assert.Null(entity.Id);

        var relation = Assert.Single(document.Record.Relations, relation =>
            relation.Type == "references" &&
            relation.Target == "Architecture Note");
        Assert.Equal("content", relation.TargetType);

        Assert.Contains(document.Record.Relations, relation =>
            relation.Type == "mentions" &&
            relation.Target == "Bukit Labs" &&
            relation.TargetType == "company");
    }

    private sealed class SchemaMappingNotionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Get && url == "https://api.notion.com/v1/databases/db-123")
            {
                return Task.FromResult(Json("""
                {
                  "properties": {
                    "Title": {},
                    "Slug": {},
                    "Type": {},
                    "Abstract": {},
                    "Company Refs": {},
                    "Reading Refs": {}
                  }
                }
                """));
            }

            if (request.Method == HttpMethod.Post && url == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "created_time": "2026-01-02T03:04:05.000Z",
                      "last_edited_time": "2026-06-01T12:00:00.000Z",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Schema Mapping Page" }]
                        },
                        "Slug": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "schema-mapping-page" }]
                        },
                        "Type": {
                          "type": "select",
                          "select": { "name": "post" }
                        },
                        "Abstract": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "Schema mapped summary" }]
                        },
                        "Company Refs": {
                          "type": "multi_select",
                          "multi_select": [{ "name": "Bukit Labs" }]
                        },
                        "Reading Refs": {
                          "type": "multi_select",
                          "multi_select": [{ "name": "Architecture Note" }]
                        }
                      }
                    }
                  ]
                }
                """));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }

        private static HttpResponseMessage Json(string json)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
