using System.Text.Json;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionPropertyParserExtendedTests
{
    [Fact]
    public void ParseFormula_StringType_ReturnsTextField()
    {
        var json = """{"type":"formula","formula":{"type":"string","string":"hello world"}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("formula", type);
        Assert.Equal("text", field.Type);
        Assert.Equal("hello world", field.Value);
    }

    [Fact]
    public void ParseFormula_NumberType_ReturnsNumberField()
    {
        var json = """{"type":"formula","formula":{"type":"number","number":42.5}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("formula", type);
        Assert.Equal("number", field.Type);
        Assert.Equal(42.5, (double)field.Value!);
    }

    [Fact]
    public void ParseFormula_BooleanType_ReturnsBoolField()
    {
        var json = """{"type":"formula","formula":{"type":"boolean","boolean":true}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("formula", type);
        Assert.Equal("bool", field.Type);
        Assert.True((bool)field.Value!);
    }

    [Fact]
    public void ParseFormula_DateType_ReturnsDateField()
    {
        var json = """{"type":"formula","formula":{"type":"date","date":{"start":"2025-06-15T10:30:00Z"}}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("formula", type);
        Assert.Equal("date", field.Type);
        Assert.NotNull(field.Value);
    }

    [Fact]
    public void ParseFormula_NullResult_ReturnsFalse()
    {
        var json = """{"type":"formula","formula":null}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var _, out var _);

        Assert.False(ok);
    }

    [Fact]
    public void ParseFormula_UnknownType_ReturnsFalse()
    {
        var json = """{"type":"formula","formula":{"type":"unknown","value":123}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var _, out var _);

        Assert.False(ok);
    }

    [Fact]
    public void ParseRollup_NumberType_ReturnsNumberField()
    {
        var json = """{"type":"rollup","rollup":{"type":"number","number":99.9}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("rollup", type);
        Assert.Equal("number", field.Type);
        Assert.Equal(99.9, (double)field.Value!);
    }

    [Fact]
    public void ParseRollup_DateType_ReturnsDateField()
    {
        var json = """{"type":"rollup","rollup":{"type":"date","date":{"start":"2025-01-01T00:00:00Z"}}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("rollup", type);
        Assert.Equal("date", field.Type);
        Assert.NotNull(field.Value);
    }

    [Fact]
    public void ParseRollup_ArrayType_ReturnsListField()
    {
        var json = """{"type":"rollup","rollup":{"type":"array","array":[{"type":"rich_text","rich_text":[{"plain_text":"a"}]},{"type":"rich_text","rich_text":[{"plain_text":"b"}]},{"type":"rich_text","rich_text":[{"plain_text":"c"}]}]}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("rollup", type);
        Assert.Equal("list", field.Type);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(field.Value!);
        Assert.Equal(3, list.Count());
    }

    [Fact]
    public void ParseSelect_SingleSelect_ReturnsTextField()
    {
        var json = """{"type":"select","select":{"name":"Draft"}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("select", type);
        Assert.Equal("text", field.Type);
        Assert.Equal("Draft", field.Value);
    }

    [Fact]
    public void ParseStatus_StatusName_ReturnsTextField()
    {
        var json = """{"type":"status","status":{"name":"Done","color":"green"}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("status", type);
        Assert.Equal("text", field.Type);
        Assert.Equal("Done", field.Value);
    }

    [Fact]
    public void ParseUniqueId_PrefixAndNumber_ReturnsTextField()
    {
        var json = """{"type":"unique_id","unique_id":{"prefix":"TASK","number":42}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("unique_id", type);
        Assert.Equal("text", field.Type);
        Assert.Equal("TASK-42", field.Value);
    }

    [Fact]
    public void ParseVerification_VerifiedState_ReturnsTextField()
    {
        var json = """{"type":"verification","verification":{"state":"verified","verified_by":null}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("verification", type);
        Assert.Equal("text", field.Type);
        Assert.Equal("verified", field.Value);
    }

    [Fact]
    public void ParseVerification_UnverifiedState_ReturnsTextField()
    {
        var json = """{"type":"verification","verification":{"state":"unverified","verified_by":null}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("unverified", field.Value);
    }

    [Fact]
    public void ParsePeople_NameFallbackToId_ReturnsListField()
    {
        var json = """{"type":"people","people":[{"object":"user","id":"user-1234","name":"Alice"},{"object":"user","id":"user-5678"}]}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("people", type);
        Assert.Equal("list", field.Type);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(field.Value!);
        Assert.Contains("Alice", list.Select(x => x.ToString()));
        Assert.Contains("user-5678", list.Select(x => x.ToString()));
    }

    [Fact]
    public void ParseFiles_ExternalUrl_ReturnsFileField()
    {
        var json = """{"type":"files","files":[{"name":"doc.pdf","type":"external","external":{"url":"https://cdn.example.com/doc.pdf"}}]}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("files", type);
        Assert.Equal("file", field.Type);
        Assert.Equal("https://cdn.example.com/doc.pdf", field.Value);
    }

    [Fact]
    public void ParseFiles_InternalFile_ReturnsFileField()
    {
        var json = """{"type":"files","files":[{"name":"image.png","type":"file","file":{"url":"https://s3.notion/abc.png","expiry_time":"2026-01-01"}}]}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("file", field.Type);
        Assert.Equal("https://s3.notion/abc.png", field.Value);
    }

    [Fact]
    public void NormalizeFieldKey_Uppercase_ConvertsToLower()
    {
        Assert.Equal("hello_world", NotionPropertyParser.NormalizeFieldKey("Hello World"));
    }

    [Fact]
    public void NormalizeFieldKey_SpacesAndSpecialChars_ReplacedWithUnderscore()
    {
        Assert.Equal("hello_123", NotionPropertyParser.NormalizeFieldKey("Hello @#$ 123"));
    }

    [Fact]
    public void NormalizeFieldKey_ConsecutiveUnderscores_Collapsed()
    {
        Assert.Equal("a_b", NotionPropertyParser.NormalizeFieldKey("A   B"));
    }

    [Fact]
    public void NormalizeFieldKey_LeadingTrailingSpecialChars_Trimmed()
    {
        Assert.Equal("test", NotionPropertyParser.NormalizeFieldKey("!!!Test!!!"));
    }

    [Fact]
    public void NormalizeFieldKey_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NotionPropertyParser.NormalizeFieldKey(""));
    }

    [Fact]
    public void TryParseRichTextArray_MultiSegment_Concatenated()
    {
        var json = """{"type":"title","title":[{"plain_text":"Hello"},{"plain_text":"World"}]}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var _);

        Assert.True(ok);
        Assert.Equal("Hello World", field.Value);
    }

    [Fact]
    public void TryParseRichTextArray_WithAnnotations_IgnoresAnnotations()
    {
        var json = """{"type":"rich_text","rich_text":[{"plain_text":"bold text","annotations":{"bold":true,"italic":false,"strikethrough":false,"underline":false,"code":false,"color":"default"}}]}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var _);

        Assert.True(ok);
        Assert.Equal("bold text", field.Value);
    }

    [Fact]
    public void TryParseList_MultiSelectNames_ReturnsListField()
    {
        var json = """{"type":"multi_select","multi_select":[{"name":"Tag A"},{"name":"Tag B"}]}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("multi_select", type);
        Assert.Equal("list", field.Type);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(field.Value!);
        Assert.Contains("Tag A", list.Select(x => x.ToString()));
        Assert.Contains("Tag B", list.Select(x => x.ToString()));
    }

    [Fact]
    public void TryParseDate_WithEndTime_ReturnsDateField()
    {
        var json = """{"type":"date","date":{"start":"2026-01-15","end":"2026-01-16","time_zone":"Asia/Shanghai"}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("date", type);
        Assert.Equal("date", field.Type);
        Assert.NotNull(field.Value);
    }

    [Fact]
    public void TryParseDate_InvalidDate_ReturnsFalse()
    {
        var json = """{"type":"date","date":{"start":"not-a-date"}}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var _, out var _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseCheckbox_FalseValue_ReturnsBoolField()
    {
        var json = """{"type":"checkbox","checkbox":false}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var type);

        Assert.True(ok);
        Assert.Equal("checkbox", type);
        Assert.Equal("bool", field.Type);
        Assert.False((bool)field.Value!);
    }

    [Fact]
    public void TryParseCheckbox_TrueValue_ReturnsBoolField()
    {
        var json = """{"type":"checkbox","checkbox":true}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var _);

        Assert.True(ok);
        Assert.True((bool)field.Value!);
    }

    [Fact]
    public void TryParseNumber_Integer_ReturnsNumberField()
    {
        var json = """{"type":"number","number":100}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var _);

        Assert.True(ok);
        Assert.Equal("number", field.Type);
        Assert.Equal(100.0, (double)field.Value!);
    }

    [Fact]
    public void TryParseNumber_Double_ReturnsNumberField()
    {
        var json = """{"type":"number","number":3.14}""";
        using var doc = JsonDocument.Parse(json);
        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var _);

        Assert.True(ok);
        Assert.Equal("number", field.Type);
    }

    [Fact]
    public void IsReservedNotionField_ReservedKeysAreFiltered()
    {
        var reservedKeys = new[]
        {
            "published", "title", "slug", "type", "publishat", "publish_at"
        };

        var jsonParts = new System.Text.StringBuilder();
        foreach (var key in reservedKeys)
        {
            jsonParts.Append($"\"{key}\":{{\"type\":\"rich_text\",\"rich_text\":[{{\"plain_text\":\"val\"}}]}},");
        }
        jsonParts.Append("\"extra\":{\"type\":\"rich_text\",\"rich_text\":[{\"plain_text\":\"visible\"}]}");

        using var doc = JsonDocument.Parse($"{{{jsonParts}}}");
        var fields = NotionPropertyParser.ExtractFields(doc.RootElement);

        Assert.DoesNotContain(fields.Keys, k => reservedKeys.Contains(k, StringComparer.OrdinalIgnoreCase));
        Assert.True(fields.ContainsKey("extra"));
    }

    [Fact]
    public void ExtractAllFields_IncludesReservedKeys()
    {
        var json = """{"title":{"type":"title","title":[{"plain_text":"My Title"}]},"tags":{"type":"rich_text","rich_text":[{"plain_text":"tag1"}]}}""";
        using var doc = JsonDocument.Parse(json);
        var fields = NotionPropertyParser.ExtractAllFields(doc.RootElement);

        Assert.Contains("title", fields.Keys);
        Assert.Contains("tags", fields.Keys);
    }

    [Fact]
    public void ExtractFields_WithNonObjectOrEmptyKeys_ReturnsOnlyParseableFields()
    {
        using var nonObject = JsonDocument.Parse("[]");
        using var withEmptyKeys = JsonDocument.Parse("""
        {
          "!!!": { "type": "rich_text", "rich_text": [{ "plain_text": "hidden" }] },
          "Unsupported": { "type": "unsupported" },
          "Visible": { "type": "rich_text", "rich_text": [{ "plain_text": "shown" }] }
        }
        """);

        Assert.Empty(NotionPropertyParser.ExtractFields(nonObject.RootElement));
        var fields = NotionPropertyParser.ExtractAllFields(withEmptyKeys.RootElement);

        Assert.Single(fields);
        Assert.Equal("shown", fields["visible"].Value);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"url":"https://example.test"}""")]
    [InlineData("""{"type":123}""")]
    [InlineData("""{"type":"unknown"}""")]
    public void TryParseNotionPropertyToField_WithInvalidEnvelope_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out var notionType);

        Assert.False(ok);
        if (json.Contains("\"type\":\"unknown\"", StringComparison.Ordinal))
        {
            Assert.Equal("unknown", notionType);
        }
    }

    [Fact]
    public void TryParseRichTextArray_WhenMissingBlankOrMalformed_ReturnsFalse()
    {
        foreach (var json in new[]
        {
            """{"type":"title"}""",
            """{"type":"title","title":{}}""",
            """{"type":"title","title":[123,{"plain_text":"   "},{"text":{"content":"ignored"}}]}"""
        })
        {
            using var doc = JsonDocument.Parse(json);
            var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out _);
            Assert.False(ok);
        }
    }

    [Theory]
    [InlineData("""{"type":"url","url":"https://example.test"}""", "url", "https://example.test")]
    [InlineData("""{"type":"email","email":"ali@example.test"}""", "email", "ali@example.test")]
    [InlineData("""{"type":"phone_number","phone_number":"+601234"}""", "phone_number", "+601234")]
    [InlineData("""{"type":"unique_id","unique_id":{"number":99}}""", "unique_id", "99")]
    public void TryParseTextLike_WithSupportedTextValues_ReturnsTextField(string json, string expectedType, string expectedValue)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var notionType);

        Assert.True(ok);
        Assert.Equal(expectedType, notionType);
        Assert.Equal("text", field.Type);
        Assert.Equal(expectedValue, field.Value);
    }

    [Theory]
    [InlineData("""{"type":"url","url":123}""")]
    [InlineData("""{"type":"select","select":null}""")]
    [InlineData("""{"type":"status","status":{"name":"   "}}""")]
    [InlineData("""{"type":"unique_id","unique_id":null}""")]
    [InlineData("""{"type":"unique_id","unique_id":{}}""")]
    [InlineData("""{"type":"verification","verification":null}""")]
    [InlineData("""{"type":"verification","verification":{"state":"   "}}""")]
    public void TryParseTextLike_WithMissingOrBlankValues_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("""{"type":"number"}""")]
    [InlineData("""{"type":"checkbox","checkbox":"true"}""")]
    [InlineData("""{"type":"date","date":null}""")]
    [InlineData("""{"type":"date","date":{"start":"   "}}""")]
    public void TryParseScalarValues_WithInvalidPayloads_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("""{"type":"created_time","created_time":"not-a-date"}""", "created_time")]
    [InlineData("""{"type":"last_edited_time","last_edited_time":"not-a-date"}""", "last_edited_time")]
    public void TryParseTimeValues_WithInvalidDate_ReturnsTextField(string json, string expectedType)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var notionType);

        Assert.True(ok);
        Assert.Equal(expectedType, notionType);
        Assert.Equal("text", field.Type);
        Assert.Equal("not-a-date", field.Value);
    }

    [Theory]
    [InlineData("""{"type":"created_time","created_time":"2026-05-15T12:00:00Z"}""", "created_time")]
    [InlineData("""{"type":"last_edited_time","last_edited_time":"2026-05-16T12:00:00Z"}""", "last_edited_time")]
    public void TryParseDate_WithTimestampProperties_ReturnsDateField(string json, string expectedType)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var notionType);

        Assert.True(ok);
        Assert.Equal(expectedType, notionType);
        Assert.Equal("date", field.Type);
    }

    [Theory]
    [InlineData("""{"type":"relation","relation":[123,{"id":" rel-1 "},{"id":"   "}]}""", "relation", "rel-1")]
    [InlineData("""{"type":"people","people":[123,{"name":"   "},{"id":" user-1 "}]}""", "people", "user-1")]
    public void TryParseList_WithMalformedEntries_KeepsValidEntries(string json, string expectedType, string expectedValue)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out var field, out var notionType);

        Assert.True(ok);
        Assert.Equal(expectedType, notionType);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(field.Value!);
        Assert.Equal(new[] { expectedValue }, list.Select(x => x.ToString()).ToArray());
    }

    [Theory]
    [InlineData("""{"type":"multi_select","multi_select":{}}""")]
    [InlineData("""{"type":"multi_select","multi_select":[123,{"name":"   "}]}""")]
    [InlineData("""{"type":"relation","relation":[{"id":"   "}]}""")]
    [InlineData("""{"type":"people","people":[123,{}]}""")]
    public void TryParseList_WithNoUsableEntries_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("""{"type":"files"}""")]
    [InlineData("""{"type":"files","files":{}}""")]
    [InlineData("""{"type":"files","files":[123,{"type":"external","external":{}},{"type":"file","file":{"url":"   "}}]}""")]
    public void TryParseFiles_WithNoUsableFile_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("""{"type":"formula"}""")]
    [InlineData("""{"type":"formula","formula":{}}""")]
    [InlineData("""{"type":"formula","formula":{"type":"string","string":"   "}}""")]
    [InlineData("""{"type":"formula","formula":{"type":"number","number":null}}""")]
    [InlineData("""{"type":"formula","formula":{"type":"boolean","boolean":null}}""")]
    [InlineData("""{"type":"formula","formula":{"type":"date","date":{"start":"not-a-date"}}}""")]
    public void TryParseFormula_WithInvalidPayloads_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("""{"type":"rollup"}""")]
    [InlineData("""{"type":"rollup","rollup":{}}""")]
    [InlineData("""{"type":"rollup","rollup":{"type":"number","number":null}}""")]
    [InlineData("""{"type":"rollup","rollup":{"type":"date","date":{"start":"not-a-date"}}}""")]
    [InlineData("""{"type":"rollup","rollup":{"type":"array","array":[]}}""")]
    [InlineData("""{"type":"rollup","rollup":{"type":"unknown"}}""")]
    public void TryParseRollup_WithInvalidPayloads_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = NotionPropertyTypeParser.TryParseNotionPropertyToField(doc.RootElement, out _, out _);

        Assert.False(ok);
    }
}
