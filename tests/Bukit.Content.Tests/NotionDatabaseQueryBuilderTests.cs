using System.Text.Json;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionDatabaseQueryBuilderTests
{
    [Fact]
    public void Build_WithCheckboxFilterIncludeSlugsSortAndCursor_ProducesExpectedJson()
    {
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            PageSize = 25,
            FilterType = "checkbox_true",
            FilterProperty = "Published",
            IncludeSlugs = new[] { "about", "About", "contact" },
            IncludeSlugProperty = "Slug",
            SortProperty = "Updated",
            SortDirection = "descending"
        };

        var json = NotionDatabaseQueryBuilder.Build(
            options,
            startCursor: "cursor-1",
            resolvedFilterProperty: "Published?",
            resolvedSortProperty: "Updated?",
            resolvedIncludeSlugProperty: "Slug?");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(25, root.GetProperty("page_size").GetInt32());
        Assert.Equal("cursor-1", root.GetProperty("start_cursor").GetString());
        Assert.Equal("Published?", root.GetProperty("filter").GetProperty("and")[0].GetProperty("property").GetString());
        var or = root.GetProperty("filter").GetProperty("and")[1].GetProperty("or");
        Assert.Equal(2, or.GetArrayLength());
        Assert.Equal("Slug?", or[0].GetProperty("property").GetString());
        Assert.Equal("about", or[0].GetProperty("rich_text").GetProperty("equals").GetString());
        Assert.Equal("contact", or[1].GetProperty("rich_text").GetProperty("equals").GetString());
        Assert.Equal("Updated?", root.GetProperty("sorts")[0].GetProperty("property").GetString());
        Assert.Equal("descending", root.GetProperty("sorts")[0].GetProperty("direction").GetString());
    }

    [Fact]
    public void Build_WithFilterNoneAndInvalidSortDirection_UsesAscendingSortOnly()
    {
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            FilterType = "none",
            SortProperty = "Created \"At\"",
            SortDirection = "sideways"
        };

        var json = NotionDatabaseQueryBuilder.Build(options, null, null, null, null);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("filter", out _));
        Assert.Equal("Created \"At\"", root.GetProperty("sorts")[0].GetProperty("property").GetString());
        Assert.Equal("ascending", root.GetProperty("sorts")[0].GetProperty("direction").GetString());
    }

    [Theory]
    [InlineData("checkbox_false", "checkbox", false)]
    [InlineData("select_equals", "select", "Published")]
    [InlineData("status_equals", "status", "Published")]
    [InlineData("rich_text_equals", "rich_text", "Published")]
    public void Build_WithSupportedFilterTypes_ProducesExpectedFilter(string filterType, string notionFilterKey, object expectedValue)
    {
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            FilterProperty = "Status",
            FilterType = filterType,
            FilterValue = "Published"
        };

        var json = NotionDatabaseQueryBuilder.Build(options, null, null, null, null);

        using var doc = JsonDocument.Parse(json);
        var filter = doc.RootElement.GetProperty("filter");
        Assert.Equal("Status", filter.GetProperty("property").GetString());
        if (expectedValue is bool expectedBool)
        {
            Assert.Equal(expectedBool, filter.GetProperty(notionFilterKey).GetProperty("equals").GetBoolean());
        }
        else
        {
            Assert.Equal((string)expectedValue, filter.GetProperty(notionFilterKey).GetProperty("equals").GetString());
        }
    }

    [Fact]
    public void Build_WithNoFiltersSortOrCursor_TrimsTrailingComma()
    {
        var options = new NotionProviderOptions
        {
            DatabaseId = "db",
            Token = "token",
            FilterType = "none"
        };

        var json = NotionDatabaseQueryBuilder.Build(options, null, null, null, null);

        Assert.Equal("{\"page_size\":50}", json);
    }
}
