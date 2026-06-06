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
    public void ContentModelSchemaValidator_ReportsCanonicalStatusErrors()
    {
        var document = ContentDocument.Create(
            "hello",
            "Hello",
            "hello",
            DateTimeOffset.UtcNow,
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = "unknown",
                ["review_status"] = "unreviewable"
            }));
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(new[] { document });

        var errors = ContentModelSchemaValidator.Validate(graph);

        Assert.Contains(errors, e => e.Code == "canonical_status_invalid" && e.SourcePath == "hello");
        Assert.Contains(errors, e => e.Code == "canonical_review_status_invalid" && e.SourcePath == "hello");
    }
}
