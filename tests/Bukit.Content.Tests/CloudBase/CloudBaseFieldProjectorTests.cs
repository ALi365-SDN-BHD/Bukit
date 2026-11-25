using System.Text.Json;
using Bukit.Content.CloudBase;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests.CloudBase;

public sealed class CloudBaseFieldProjectorTests
{
    private static readonly JsonElement Row = JsonDocument.Parse("""
        {
          "id": "a1",
          "title": "Post",
          "status": "published",
          "cover": "cover.jpg",
          "tags": ["x", "y"],
          "views": 7,
          "featured": true,
          "user_id": "u-secret",
          "owner": "o-secret",
          "created_by": "c-secret",
          "updated_by": "m-secret",
          "_openid": "openid-secret",
          "nested": { "secret": true }
        }
        """).RootElement;

    [Fact]
    public void Project_Whitelist_KeepsOnlyAllowedFields()
    {
        var fields = Project(Row, "whitelist", ["title", "tags"], "id");

        Assert.Equal(["tags", "title"], fields.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("text", fields["title"].Type);
        Assert.Equal("list", fields["tags"].Type);
    }

    [Fact]
    public void Project_Whitelist_StillDropsHardExcludedFields()
    {
        var fields = Project(
            Row,
            "whitelist",
            ["user_id", "owner", "created_by", "updated_by", "_openid", "title"],
            "id");

        Assert.Equal(["title"], fields.Keys);
    }

    [Fact]
    public void Project_All_DropsHardExcludedAndIdField()
    {
        var fields = Project(Row, "all", null, "id");

        Assert.DoesNotContain("id", fields.Keys);
        Assert.DoesNotContain("user_id", fields.Keys);
        Assert.DoesNotContain("owner", fields.Keys);
        Assert.DoesNotContain("created_by", fields.Keys);
        Assert.DoesNotContain("updated_by", fields.Keys);
        Assert.DoesNotContain("_openid", fields.Keys);
        Assert.Contains("nested", fields.Keys);
        Assert.Contains("title", fields.Keys);
        Assert.Contains("views", fields.Keys);
        Assert.Contains("featured", fields.Keys);
    }

    [Fact]
    public void Project_All_KeepsIdFieldOnlyWhenExplicitlyAllowed()
    {
        var fields = Project(Row, "all", ["id"], "id");

        Assert.Contains("id", fields.Keys);
        Assert.DoesNotContain("_openid", fields.Keys);
    }

    [Fact]
    public void Project_ConvertsJsonTypes()
    {
        var fields = Project(Row, "all", null, "id");

        Assert.Equal("number", fields["views"].Type);
        Assert.Equal("check", fields["featured"].Type);
        Assert.Equal(["x", "y"], Assert.IsAssignableFrom<IEnumerable<string>>(fields["tags"].Value));
    }

    [Theory]
    [InlineData("9007199254740993", 9007199254740993L)]
    [InlineData("1790000000000000001", 1790000000000000001L)]
    public void Project_BigIntegers_KeepEveryDigit(string json, long expected)
    {
        var row = JsonDocument.Parse($$"""{"author_id": {{json}}}""").RootElement;

        var fields = Project(row, "whitelist", ["author_id"], "id");

        Assert.Equal(expected, Assert.IsType<long>(fields["author_id"].Value));
    }

    [Fact]
    public void Project_Fractions_StayDouble()
    {
        var row = JsonDocument.Parse("""{"price": 19.99}""").RootElement;

        var fields = Project(row, "whitelist", ["price"], "id");

        Assert.Equal(19.99, Assert.IsType<double>(fields["price"].Value));
    }

    [Fact]
    public void Project_NonObjectRow_ReturnsEmpty()
    {
        var fields = Project(JsonDocument.Parse("[1,2]").RootElement, "all", null, "id");

        Assert.Empty(fields);
    }

    [Fact]
    public void Project_ObjectArray_KeepsStructureAndJsonTypes()
    {
        var row = JsonDocument.Parse("""
            {"days": [{"title": "Day 1", "text": "Arrive", "attractions": ["a1", "a2"], "nights": 1, "price": 12.50, "free": false, "note": null}]}
            """).RootElement;

        var fields = Project(row, "whitelist", ["days"], "id");

        Assert.Equal("list", fields["days"].Type);
        var day = Assert.IsType<SortedDictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(fields["days"].Value)));
        Assert.Equal(["attractions", "free", "nights", "note", "price", "text", "title"], day.Keys);
        Assert.Equal(["a1", "a2"], Assert.IsType<List<object?>>(day["attractions"]));
        Assert.Equal(1L, day["nights"]);
        Assert.Equal("12.50", Assert.IsType<decimal>(day["price"]).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(false, day["free"]);
        Assert.Null(day["note"]);
    }

    [Fact]
    public void Project_NumberArray_KeepsNumbers()
    {
        var row = JsonDocument.Parse("""{"months": [3, 4, 5]}""").RootElement;

        var fields = Project(row, "whitelist", ["months"], "id");

        Assert.Equal([3L, 4L, 5L], Assert.IsType<List<object?>>(fields["months"].Value));
    }

    [Fact]
    public void Project_Object_HasOrdinalSortedKeys()
    {
        var row = JsonDocument.Parse("""{"contact": {"wechat": "w", "Phone": "+60", "email": "e"}}""").RootElement;

        var fields = Project(row, "whitelist", ["contact"], "id");

        var contact = Assert.IsType<SortedDictionary<string, object?>>(fields["contact"].Value);
        Assert.Equal(["Phone", "email", "wechat"], contact.Keys);
    }

    [Fact]
    public void Project_StringArrayWithNulls_StaysTextList()
    {
        var row = JsonDocument.Parse("""{"tags": ["x", null, " ", "y"]}""").RootElement;

        var fields = Project(row, "whitelist", ["tags"], "id");

        Assert.Equal(["x", "y"], Assert.IsType<string[]>(fields["tags"].Value));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("""{"USER_ID": 1, "owner": "o"}""")]
    public void Project_EmptyContainer_IsAbsent(string json)
    {
        var row = JsonDocument.Parse($$"""{"value": {{json}}}""").RootElement;

        Assert.Empty(Project(row, "whitelist", ["value"], "id"));
    }

    [Fact]
    public void Project_Nested_DropsHardExcludedKeysAtEveryLevel()
    {
        var row = JsonDocument.Parse("""
            {"info": {"name": "n", "USER_ID": "u", "items": [{"owner": "o", "_openid": "x", "label": "l"}], "meta": {"created_by": "c"}}}
            """).RootElement;

        var fields = Project(row, "whitelist", ["info"], "id");

        var info = Assert.IsType<SortedDictionary<string, object?>>(fields["info"].Value);
        Assert.Equal(["items", "meta", "name"], info.Keys);
        var item = Assert.IsType<SortedDictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(info["items"])));
        Assert.Equal(["label"], item.Keys);
        Assert.Empty(Assert.IsType<SortedDictionary<string, object?>>(info["meta"]));
    }

    [Fact]
    public void Project_TooDeep_IsRejectedWithPath()
    {
        // Nine levels: one object holding eight nested arrays.
        var row = JsonDocument.Parse("""{"deep": {"a": [[[[[[[[1]]]]]]]]}, "ok": "kept"}""").RootElement;
        var rejected = new List<(string Field, string Problem)>();

        var fields = CloudBaseFieldProjector.ProjectWithSet(
            row,
            "whitelist",
            CloudBaseFieldProjector.BuildAllowedSet("whitelist", ["deep", "ok"]),
            "id",
            rejected);

        Assert.Equal(["ok"], fields.Keys);
        var (field, problem) = Assert.Single(rejected);
        Assert.Equal("deep", field);
        Assert.Contains("maximum nesting depth of 8", problem, StringComparison.Ordinal);
        Assert.Contains("'a[0][0][0][0][0][0][0]'", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_EightLevels_IsAccepted()
    {
        var row = JsonDocument.Parse("""{"deep": {"a": [[[[[[[1]]]]]]]}}""").RootElement;

        Assert.Contains("deep", Project(row, "whitelist", ["deep"], "id").Keys);
    }

    [Fact]
    public void Project_TooLarge_IsRejectedWithoutValue()
    {
        var big = new string('x', CloudBaseFieldProjector.MaxStructuredFieldBytes);
        var row = JsonDocument.Parse($$$"""{"blob": {"text": "{{{big}}}"}}""").RootElement;
        var rejected = new List<(string Field, string Problem)>();

        var fields = CloudBaseFieldProjector.ProjectWithSet(
            row,
            "whitelist",
            CloudBaseFieldProjector.BuildAllowedSet("whitelist", ["blob"]),
            "id",
            rejected);

        Assert.Empty(fields);
        var (field, problem) = Assert.Single(rejected);
        Assert.Equal("blob", field);
        Assert.Contains("more than the maximum of 262144", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("xxxx", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_Structured_IsStableAcrossKeyOrder()
    {
        var first = Project(JsonDocument.Parse("""{"v": {"b": [1, {"y": 2, "x": 1}], "a": 1.10}}""").RootElement, "all", null, "id");
        var second = Project(JsonDocument.Parse("""{"v": {"a": 1.10, "b": [1, {"x": 1, "y": 2}]}}""").RootElement, "all", null, "id");

        Assert.Equal(Serialize(first["v"].Value), Serialize(second["v"].Value));
    }

    private static string Serialize(object? value) => value switch
    {
        SortedDictionary<string, object?> map => "{" + string.Join(",", map.Select(pair => pair.Key + ":" + Serialize(pair.Value))) + "}",
        List<object?> list => "[" + string.Join(",", list.Select(Serialize)) + "]",
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "null"
    };

    private static Dictionary<string, ContentField> Project(
        JsonElement row,
        string policyMode,
        IReadOnlyList<string>? allowedFields,
        string idField)
        => CloudBaseFieldProjector.ProjectWithSet(
            row,
            policyMode,
            CloudBaseFieldProjector.BuildAllowedSet(policyMode, allowedFields),
            idField);
}
