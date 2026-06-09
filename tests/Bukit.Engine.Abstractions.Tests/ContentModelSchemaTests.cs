using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentModelSchemaTests
{
    [Fact]
    public void DefaultSchema_RequiresAltAndRelationTargets()
    {
        var schema = new ContentModelSchema();
        Assert.True(schema.RequireMediaAlt);
        Assert.True(schema.RequireRelationTargets);
        Assert.False(schema.RequireSummary);
        Assert.False(schema.RequireAuthor);
        Assert.False(schema.RejectUnknownRawKeys);
    }

    [Fact]
    public void ContentTypes_RestrictValidValues()
    {
        var schema = new ContentModelSchema(ContentTypes: new[] { "post", "page" });
        Assert.Equal(2, schema.ContentTypes!.Count);
        Assert.Contains("post", schema.ContentTypes);
    }

    [Fact]
    public void Statuses_DefineAllowedTransitions()
    {
        var schema = new ContentModelSchema(
            Statuses: new[] { "draft", "review", "published" },
            ReviewStatuses: new[] { "pending", "approved", "rejected" });
        Assert.Equal(3, schema.Statuses!.Count);
        Assert.Equal(3, schema.ReviewStatuses!.Count);
    }

    [Fact]
    public void CustomFields_DefineFieldContracts()
    {
        var fields = new Dictionary<string, CustomFieldDefinition>
        {
            ["seoTitle"] = new("seoTitle", "text", Required: true, Label: "SEO Title"),
            ["priority"] = new("priority", "number", Min: 1, Max: 10, Default: 5.0)
        };
        var schema = new ContentModelSchema(CustomFields: fields);
        Assert.Equal(2, schema.CustomFields!.Count);
        Assert.True(schema.CustomFields["seoTitle"].Required);
        Assert.Equal(5.0, schema.CustomFields["priority"].Default);
    }

    [Fact]
    public void EntityMappings_MapRawKeysToEntities()
    {
        var entities = new Dictionary<string, EntityMapping>
        {
            ["authorField"] = new("authorField", "Person", Required: true)
        };
        var schema = new ContentModelSchema(EntityMappings: entities);
        Assert.Single(schema.EntityMappings!);
        Assert.Equal("Person", schema.EntityMappings!["authorField"].EntityType);
    }

    [Fact]
    public void CustomFieldDefinition_EnumField_DefinesAllowedValues()
    {
        var field = new CustomFieldDefinition("color", "text",
            Enum: new[] { "red", "green", "blue" });
        Assert.Equal(3, field.Enum!.Count);
        Assert.Contains("blue", field.Enum);
    }

    [Fact]
    public void MediaPolicy_ConfiguredPerSchema()
    {
        var schema = new ContentModelSchema(
            Media: new MediaPolicy(RequireDescription: true, RequireLicense: true));
        Assert.True(schema.Media!.RequireDescription);
        Assert.True(schema.Media!.RequireLicense);
    }
}
