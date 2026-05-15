using Bukit.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MetaHelpersTests
{
    private static ContentItem CreateItem(Dictionary<string, object> meta)
    {
        return new ContentItem(
            Id: "test",
            Title: "Test",
            Slug: "test",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: meta);
    }

    [Fact]
    public void IsDataItem_TypeIsData_ReturnsTrue()
    {
        var meta = new Dictionary<string, object> { ["sourceMode"] = "data" };

        Assert.True(MetaHelpers.IsDataItem(CreateItem(meta)));
    }

    [Fact]
    public void IsDataItem_TypeIsNotData_ReturnsFalse()
    {
        var meta = new Dictionary<string, object> { ["type"] = "post" };

        Assert.False(MetaHelpers.IsDataItem(CreateItem(meta)));
    }

    [Fact]
    public void IsDataItem_NoTypeKey_ReturnsFalse()
    {
        var meta = new Dictionary<string, object> { ["title"] = "hello" };

        Assert.False(MetaHelpers.IsDataItem(CreateItem(meta)));
    }

    [Fact]
    public void GetString_KeyExists_ReturnsValue()
    {
        var meta = new Dictionary<string, object> { ["author"] = "Alice" };

        var result = MetaHelpers.GetString(meta, "author");

        Assert.Equal("Alice", result);
    }

    [Fact]
    public void GetString_KeyMissing_ReturnsFallback()
    {
        var meta = new Dictionary<string, object>();

        var result = MetaHelpers.GetString(meta, "author") ?? "Unknown";

        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void GetString_KeyMissing_ReturnsNullWhenNoFallback()
    {
        var meta = new Dictionary<string, object>();

        var result = MetaHelpers.GetString(meta, "author");

        Assert.Null(result);
    }

    [Fact]
    public void GetStringList_CommaSeparated_ReturnsTrimmedList()
    {
        var meta = new Dictionary<string, object> { ["tags"] = "dotnet, aspire , cloud " };

        var result = MetaHelpers.GetStringList(meta, "tags");

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.Equal("dotnet", result[0]);
        Assert.Equal("aspire", result[1]);
        Assert.Equal("cloud", result[2]);
    }

    [Fact]
    public void GetStringList_SingleValue_ReturnsSingleItemList()
    {
        var meta = new Dictionary<string, object> { ["tag"] = "tech" };

        var result = MetaHelpers.GetStringList(meta, "tag");

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("tech", result[0]);
    }

    [Fact]
    public void GetStringList_KeyMissing_ReturnsEmptyList()
    {
        var meta = new Dictionary<string, object>();

        var result = MetaHelpers.GetStringList(meta, "tags");

        Assert.Null(result);
    }

    [Fact]
    public void GetStringList_EmptyString_ReturnsEmptyList()
    {
        var meta = new Dictionary<string, object> { ["tags"] = "" };

        var result = MetaHelpers.GetStringList(meta, "tags");

        Assert.Null(result);
    }

    [Fact]
    public void TryGetI18nKey_KeyExists_ReturnsTrue()
    {
        var meta = new Dictionary<string, object> { ["i18nKey"] = "page.about" };

        var result = MetaHelpers.TryGetI18nKey(meta, out var key);

        Assert.True(result);
        Assert.Equal("page.about", key);
    }

    [Fact]
    public void TryGetI18nKey_KeyMissing_ReturnsFalse()
    {
        var meta = new Dictionary<string, object>();

        var result = MetaHelpers.TryGetI18nKey(meta, out var key);

        Assert.False(result);
        Assert.Equal("", key);
    }

    [Fact]
    public void TryGetBoolField_FieldExists_ReturnsTrue()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["enabled"] = new("bool", true)
        };

        var result = MetaHelpers.TryGetBoolField(fields, "enabled");

        Assert.True(result);
    }

    [Fact]
    public void TryGetBoolField_FieldMissing_ReturnsNull()
    {
        var fields = new Dictionary<string, ContentField>();

        var result = MetaHelpers.TryGetBoolField(fields, "enabled");

        Assert.Null(result);
    }

    [Fact]
    public void TryGetBoolField_FieldFalse_ReturnsFalse()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["disabled"] = new("bool", false)
        };

        var result = MetaHelpers.TryGetBoolField(fields, "disabled");

        Assert.False(result);
    }

    [Fact]
    public void TryGetTextField_FieldExists_ReturnsValue()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["color"] = new("text", "blue")
        };

        var result = MetaHelpers.TryGetTextField(fields, "color");

        Assert.Equal("blue", result);
    }

    [Fact]
    public void TryGetTextField_FieldMissing_ReturnsNull()
    {
        var fields = new Dictionary<string, ContentField>();

        var result = MetaHelpers.TryGetTextField(fields, "color");

        Assert.Null(result);
    }

    [Fact]
    public void TryGetTextField_WrongType_ReturnsStringified()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["value"] = new("number", 42d)
        };

        var result = MetaHelpers.TryGetTextField(fields, "value");

        Assert.Equal("42", result);
    }

    [Fact]
    public void TryGetNumberField_FieldExists_ReturnsValue()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["order"] = new("number", 5d)
        };

        var result = MetaHelpers.TryGetNumberField(fields, "order");

        Assert.Equal(5d, result);
    }

    [Fact]
    public void TryGetNumberField_FieldMissing_ReturnsNull()
    {
        var fields = new Dictionary<string, ContentField>();

        var result = MetaHelpers.TryGetNumberField(fields, "order");

        Assert.Null(result);
    }

    [Fact]
    public void TryGetNumberField_WrongType_ReturnsNull()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["value"] = new("text", "not a number")
        };

        var result = MetaHelpers.TryGetNumberField(fields, "value");

        Assert.Null(result);
    }

    [Fact]
    public void GetString_WhitespaceValue_ReturnsWhitespace()
    {
        var meta = new Dictionary<string, object> { ["summary"] = "   " };

        var result = MetaHelpers.GetString(meta, "summary");

        Assert.Equal("   ", result);
    }

    [Fact]
    public void GetString_WhitespaceValue_ReturnsFallbackWhenProvided()
    {
        var meta = new Dictionary<string, object> { ["summary"] = "   " };

        var result = MetaHelpers.GetString(meta, "summary") ?? "Default description";

        Assert.Equal("   ", result);
    }
}
