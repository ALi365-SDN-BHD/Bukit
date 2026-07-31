using Xunit;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Tests;

/// <summary>
/// Extended tests for ContentSchemaValidator validation logic.
/// </summary>
public sealed class ContentSchemaValidatorCoverageTests
{
    private static IReadOnlyDictionary<string, ContentField> MakeFields(params (string key, object? value)[] pairs)
    {
        var dict = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
        {
            dict[key] = new ContentField("text", value);
        }
        return dict;
    }

    // ── ValidateFields: basic scenarios ─────────────────────────────

    [Fact]
    public void ValidateFields_NullFields_NoSchema_ReturnsEmpty()
    {
        var errors = ContentSchemaValidator.ValidateFields(null, null, "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateFields_EmptySchema_ReturnsEmpty()
    {
        var fields = MakeFields(("title", "Hello"));
        var errors = ContentSchemaValidator.ValidateFields(fields, new List<CustomFieldDefinitionConfig>(), "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateFields_NullFields_WithRequiredField_ReportsMissing()
    {
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "title", FieldType = "string", Required = true }
        };
        var errors = ContentSchemaValidator.ValidateFields(null, schema, "/test.md");
        Assert.Single(errors);
        Assert.Equal("required", errors[0].Code);
    }

    [Fact]
    public void ValidateFields_RequiredFieldPresent_NoError()
    {
        var fields = MakeFields(("title", "Hello"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "title", FieldType = "string", Required = true }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateFields_RequiredFieldWithDefault_NoError()
    {
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "status", FieldType = "string", Required = true, Default = "draft" }
        };
        var errors = ContentSchemaValidator.ValidateFields(null, schema, "/test.md");
        Assert.Empty(errors);
    }

    // ── Type validation ─────────────────────────────────────────────

    [Fact]
    public void Validate_TypeMismatch_StringExpectedGotNumber()
    {
        var fields = MakeFields(("count", 42));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "count", FieldType = "string" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Single(errors);
        Assert.Equal("type_mismatch", errors[0].Code);
    }

    [Fact]
    public void Validate_TypeMismatch_NumberExpectedGotString()
    {
        var fields = MakeFields(("count", "not a number"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "count", FieldType = "number" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Single(errors);
        Assert.Equal("type_mismatch", errors[0].Code);
    }

    [Fact]
    public void Validate_TypeMatch_Bool()
    {
        var fields = MakeFields(("draft", true));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "draft", FieldType = "bool" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_TypeMatch_Date()
    {
        var fields = MakeFields(("published", DateTimeOffset.UtcNow));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "published", FieldType = "date" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_TypeMatch_List()
    {
        var fields = MakeFields(("tags", new List<object> { "a", "b" }));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "tags", FieldType = "list" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    // ── Enum validation ─────────────────────────────────────────────

    [Fact]
    public void Validate_EnumMismatch()
    {
        var fields = MakeFields(("status", "unknown"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "status", FieldType = "string", Enum = new List<string> { "draft", "published" } }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Single(errors);
        Assert.Equal("enum_mismatch", errors[0].Code);
    }

    [Fact]
    public void Validate_EnumMatch()
    {
        var fields = MakeFields(("status", "draft"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "status", FieldType = "string", Enum = new List<string> { "draft", "published" } }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    // ── Format validation ───────────────────────────────────────────

    [Fact]
    public void Validate_FormatUrl_Valid()
    {
        var fields = MakeFields(("website", "https://example.com"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "website", FieldType = "string", Format = "url" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_FormatUrl_Invalid()
    {
        var fields = MakeFields(("website", "not a url"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "website", FieldType = "string", Format = "url" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Single(errors);
        Assert.Equal("format_mismatch", errors[0].Code);
    }

    [Fact]
    public void Validate_FormatEmail_Valid()
    {
        var fields = MakeFields(("email", "user@example.com"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "email", FieldType = "string", Format = "email" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_FormatEmail_Invalid()
    {
        var fields = MakeFields(("email", "not-an-email"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "email", FieldType = "string", Format = "email" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_FormatSlug_Valid()
    {
        var fields = MakeFields(("slug", "my-page-slug"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "slug", FieldType = "string", Format = "slug" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_FormatDate_Valid()
    {
        var fields = MakeFields(("date", "2024-01-15"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "date", FieldType = "string", Format = "date" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    // ── Range validation ────────────────────────────────────────────

    [Fact]
    public void Validate_RangeMismatch_TooLow()
    {
        var fields = MakeFields(("count", 5));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "count", FieldType = "number", Min = 10 }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Single(errors);
        Assert.Equal("range_mismatch", errors[0].Code);
    }

    [Fact]
    public void Validate_RangeMismatch_TooHigh()
    {
        var fields = MakeFields(("count", 100));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "count", FieldType = "number", Max = 50 }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_RangeMatch()
    {
        var fields = MakeFields(("count", 25));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "count", FieldType = "number", Min = 10, Max = 50 }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md");
        Assert.Empty(errors);
    }

    // ── Unknown field detection ─────────────────────────────────────

    [Fact]
    public void Validate_UnknownField_WarnMode()
    {
        var fields = MakeFields(("unknown_field", "value"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "title", FieldType = "string" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md", failMode: "warn");
        Assert.Single(errors);
        Assert.Equal("unknown_field", errors[0].Code);
    }

    [Fact]
    public void Validate_UnknownField_OffMode_NoError()
    {
        var fields = MakeFields(("unknown_field", "value"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "title", FieldType = "string" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md", failMode: "off");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_SystemField_NotReportedAsUnknown()
    {
        var fields = MakeFields(("title", "Hello"), ("slug", "hello"), ("type", "page"));
        var schema = new List<CustomFieldDefinitionConfig>
        {
            new() { Name = "custom", FieldType = "string" }
        };
        var errors = ContentSchemaValidator.ValidateFields(fields, schema, "/test.md", failMode: "warn");
        Assert.Empty(errors);
    }

    // ── ResolveSchemaFailMode ───────────────────────────────────────

    [Fact]
    public void ResolveSchemaFailMode_CollectionOverridesGlobal()
    {
        var collection = new CollectionConfig { Permalink = "/{slug}/", SchemaFailMode = "strict" };
        var result = ContentSchemaValidator.ResolveSchemaFailMode(collection, "warn");
        Assert.Equal("strict", result);
    }

    [Fact]
    public void ResolveSchemaFailMode_GlobalFallback()
    {
        var collection = new CollectionConfig { Permalink = "/{slug}/" };
        var result = ContentSchemaValidator.ResolveSchemaFailMode(collection, "error");
        Assert.Equal("error", result);
    }

    [Fact]
    public void ResolveSchemaFailMode_DefaultWarn()
    {
        var result = ContentSchemaValidator.ResolveSchemaFailMode(null, null!);
        Assert.Equal("warn", result);
    }
}
