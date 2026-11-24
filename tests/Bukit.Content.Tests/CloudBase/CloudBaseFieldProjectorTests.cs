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
        Assert.DoesNotContain("nested", fields.Keys);
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
