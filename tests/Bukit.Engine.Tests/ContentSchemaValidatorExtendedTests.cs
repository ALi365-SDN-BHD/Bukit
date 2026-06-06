using Bukit.Engine.Abstractions.Content;
using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentSchemaValidatorExtendedTests
{
    [Fact]
    public void Validate_ReportsEnumFormatAndRangeErrors()
    {
        var schema = new[]
        {
            new SchemaFieldDefinition { Name = "status", Type = "string", Enum = new[] { "draft", "published" } },
            new SchemaFieldDefinition { Name = "canonical", Type = "string", Format = "url" },
            new SchemaFieldDefinition { Name = "score", Type = "number", Min = 1, Max = 10 }
        };
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = "archived",
            ["canonical"] = "not a url",
            ["score"] = 42
        };

        var errors = ContentSchemaValidator.Validate(meta, schema, "post.md");

        Assert.Contains(errors, e => e.Field == "status" && e.Code == "enum_mismatch");
        Assert.Contains(errors, e => e.Field == "canonical" && e.Code == "format_mismatch");
        Assert.Contains(errors, e => e.Field == "score" && e.Code == "range_mismatch");
    }

    [Fact]
    public void ApplyDefaults_AddsMissingSchemaDefaultsToMetaAndFields()
    {
        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["posts"] = new CollectionConfig
            {
                Permalink = "/posts/{slug}/",
                Template = "pages/post.html",
                Schema = new[]
                {
                    new SchemaFieldDefinition { Name = "status", Type = "string", Default = "draft" }
                }
            }
        };
        var item = new ContentItem(
            "hello",
            "Hello",
            "hello",
            DateTimeOffset.UtcNow,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "posts" }));

        var result = ContentSchemaValidator.ApplyDefaults(collections, new[] { item });

        Assert.Equal("draft", ContentFieldReader.GetText(result[0].Fields, "status"));
        Assert.Equal("text", result[0].Fields!["status"].Type);
        Assert.Equal("draft", result[0].Fields!["status"].Value);
    }
}
