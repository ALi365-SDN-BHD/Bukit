using Bukit.Config;
using Bukit.Content.Notion;
using Bukit.Shared;
using System.Text.Json;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionPropertyMapTests
{
    private static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ExtractTitle_WithPropertyMap_UsesMappedName()
    {
        var properties = ParseJson("""
            {
                "SEO Title": { "type": "title", "title": [{ "plain_text": "Hello World" }] }
            }
            """);
        var map = new NotionPropertyMapConfig { Title = "SEO Title" };

        var result = NotionPropertyParser.ExtractTitle(properties, map);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ExtractTitle_WithoutPropertyMap_UsesDefault()
    {
        var properties = ParseJson("""
            {
                "Title": { "type": "title", "title": [{ "plain_text": "Default Title" }] }
            }
            """);

        var result = NotionPropertyParser.ExtractTitle(properties, null);

        Assert.Equal("Default Title", result);
    }

    [Fact]
    public void ExtractTitle_PropertyMapMissing_UsesDefault()
    {
        var properties = ParseJson("""
            {
                "Title": { "type": "title", "title": [{ "plain_text": "Default Title" }] }
            }
            """);
        var map = new NotionPropertyMapConfig(); // no Title set

        var result = NotionPropertyParser.ExtractTitle(properties, map);

        Assert.Equal("Default Title", result);
    }

    [Fact]
    public void ExtractSlug_WithPropertyMap_UsesMappedName()
    {
        var properties = ParseJson("""
            {
                "URL Slug": { "type": "rich_text", "rich_text": [{ "plain_text": "my-slug" }] }
            }
            """);
        var map = new NotionPropertyMapConfig { Slug = "URL Slug" };

        var result = NotionPropertyParser.ExtractSlug(properties, map);

        Assert.Equal("my-slug", result);
    }

    [Fact]
    public void ExtractSlug_WithoutPropertyMap_UsesDefault()
    {
        var properties = ParseJson("""
            {
                "Slug": { "type": "rich_text", "rich_text": [{ "plain_text": "default-slug" }] }
            }
            """);

        var result = NotionPropertyParser.ExtractSlug(properties, null);

        Assert.Equal("default-slug", result);
    }

    [Fact]
    public void ExtractType_WithPropertyMap_UsesMappedName()
    {
        var properties = ParseJson("""
            {
                "Content Type": { "type": "select", "select": { "name": "news" } }
            }
            """);
        var map = new NotionPropertyMapConfig { Type = "Content Type" };

        var result = NotionPropertyParser.ExtractType(properties, map);

        Assert.Equal("news", result);
    }

    [Fact]
    public void ExtractType_WithoutPropertyMap_UsesDefault()
    {
        var properties = ParseJson("""
            {
                "Type": { "type": "select", "select": { "name": "post" } }
            }
            """);

        var result = NotionPropertyParser.ExtractType(properties, null);

        Assert.Equal("post", result);
    }

    [Fact]
    public void ExtractTypeAndCollection_WithPropertyMap_ProjectDistinctCanonicalValues()
    {
        var properties = ParseJson("""
            {
                "Content Type": { "type": "select", "select": { "name": "article" } },
                "Content Collection": { "type": "status", "status": { "name": "news" } }
            }
            """);
        var map = new NotionPropertyMapConfig
        {
            Type = "Content Type",
            Collection = "Content Collection"
        };

        var type = NotionPropertyParser.ExtractType(properties, map);
        var collection = NotionPropertyParser.ExtractCollection(properties, map);

        Assert.Equal("article", type);
        Assert.Equal("news", collection);
    }

    [Theory]
    [InlineData("""{ "type": "rich_text", "rich_text": [{ "plain_text": "news" }] }""")]
    [InlineData("""{ "type": "select", "select": { "name": "news" } }""")]
    [InlineData("""{ "type": "status", "status": { "name": "news" } }""")]
    public void ExtractCollection_WithAllowedScalarType_ParsesValue(string propertyJson)
    {
        var properties = ParseJson($$"""
            {
                "Content Collection": {{propertyJson}}
            }
            """);
        var map = new NotionPropertyMapConfig { Collection = "Content Collection" };

        var collection = NotionPropertyParser.ExtractCollection(properties, map);

        Assert.Equal("news", collection);
    }

    [Theory]
    [InlineData("title", """{ "type": "title", "title": [{ "plain_text": "news" }] }""")]
    [InlineData("url", """{ "type": "url", "url": "https://example.test/news" }""")]
    [InlineData("email", """{ "type": "email", "email": "news@example.test" }""")]
    [InlineData("phone_number", """{ "type": "phone_number", "phone_number": "+12025550123" }""")]
    [InlineData("formula", """{ "type": "formula", "formula": { "type": "string", "string": "news" } }""")]
    [InlineData("multi_select", """{ "type": "multi_select", "multi_select": [{ "name": "news" }] }""")]
    [InlineData("people", """{ "type": "people", "people": [] }""")]
    [InlineData("relation", """{ "type": "relation", "relation": [] }""")]
    [InlineData("rollup", """{ "type": "rollup", "rollup": { "type": "array", "array": [] } }""")]
    [InlineData("files", """{ "type": "files", "files": [] }""")]
    public void ExtractCollection_WithDisallowedPropertyType_ThrowsAllowedTypesError(
        string notionType,
        string propertyJson)
    {
        var properties = ParseJson($$"""
            {
                "Content Collection": {{propertyJson}}
            }
            """);
        var map = new NotionPropertyMapConfig { Collection = "Content Collection" };

        var ex = Assert.Throws<ContentException>(() =>
            NotionPropertyParser.ExtractCollection(properties, map));

        Assert.Contains("Content Collection", ex.Message);
        Assert.Contains(notionType, ex.Message);
        Assert.Contains("rich_text", ex.Message);
        Assert.Contains("select", ex.Message);
        Assert.Contains("status", ex.Message);
    }

    [Theory]
    [InlineData("""{ "type": "rich_text", "rich_text": [] }""")]
    [InlineData("""{ "type": "select", "select": { "name": "   " } }""")]
    [InlineData("""{ "type": "status", "status": { "name": "" } }""")]
    public void ExtractCollection_WithEmptyAllowedScalar_ReturnsNull(string propertyJson)
    {
        var properties = ParseJson($$"""
            {
                "Content Collection": {{propertyJson}}
            }
            """);
        var map = new NotionPropertyMapConfig { Collection = "Content Collection" };

        var collection = NotionPropertyParser.ExtractCollection(properties, map);

        Assert.Null(collection);
    }

    [Fact]
    public void ExtractCollection_WithMissingMappedProperty_ReturnsNull()
    {
        var properties = ParseJson("""{ "Other": { "type": "rich_text", "rich_text": [] } }""");
        var map = new NotionPropertyMapConfig { Collection = "Content Collection" };

        var collection = NotionPropertyParser.ExtractCollection(properties, map);

        Assert.Null(collection);
    }

    [Fact]
    public void ExtractCollection_WithMultiSelect_ThrowsClearContentException()
    {
        var properties = ParseJson("""
            {
                "Content Collection": {
                    "type": "multi_select",
                    "multi_select": [
                        { "name": "news" },
                        { "name": "featured" }
                    ]
                }
            }
            """);
        var map = new NotionPropertyMapConfig { Collection = "Content Collection" };

        var ex = Assert.Throws<ContentException>(() =>
            NotionPropertyParser.ExtractCollection(properties, map));

        Assert.Contains("Collection", ex.Message);
        Assert.Contains("single scalar", ex.Message);
        Assert.Contains("Content Collection", ex.Message);
    }

    [Fact]
    public void ExtractCollection_WithEmptyMultiSelect_ThrowsClearContentException()
    {
        var properties = ParseJson("""
            {
                "Content Collection": {
                    "type": "multi_select",
                    "multi_select": []
                }
            }
            """);
        var map = new NotionPropertyMapConfig { Collection = "Content Collection" };

        var ex = Assert.Throws<ContentException>(() =>
            NotionPropertyParser.ExtractCollection(properties, map));

        Assert.Contains("single scalar", ex.Message);
    }

    [Fact]
    public void ExtractCollection_WithSingleFile_ThrowsClearContentException()
    {
        var properties = ParseJson("""
            {
                "Content Collection": {
                    "type": "files",
                    "files": [
                        {
                            "type": "external",
                            "external": { "url": "https://example.test/news" }
                        }
                    ]
                }
            }
            """);
        var map = new NotionPropertyMapConfig { Collection = "Content Collection" };

        var ex = Assert.Throws<ContentException>(() =>
            NotionPropertyParser.ExtractCollection(properties, map));

        Assert.Contains("single scalar", ex.Message);
    }

    [Fact]
    public void ExtractPublishAt_WithPropertyMap_UsesMappedName()
    {
        var properties = ParseJson("""
            {
                "Release Date": { "type": "date", "date": { "start": "2025-01-15" } }
            }
            """);
        var map = new NotionPropertyMapConfig { PublishAt = "Release Date" };

        var result = NotionPropertyParser.ExtractPublishAt(properties, map);

        Assert.NotNull(result);
        Assert.Equal(2025, result!.Value.Year);
        Assert.Equal(1, result.Value.Month);
    }

    [Fact]
    public void ExtractPublishAt_WithoutPropertyMap_UsesDefault()
    {
        var properties = ParseJson("""
            {
                "PublishAt": { "type": "date", "date": { "start": "2024-06-01" } }
            }
            """);

        var result = NotionPropertyParser.ExtractPublishAt(properties, null);

        Assert.NotNull(result);
        Assert.Equal(2024, result!.Value.Year);
    }

    [Fact]
    public void ExtractPublishAt_DoesNotUseUnmappedDateField()
    {
        var properties = ParseJson("""
            {
                "Date": { "type": "date", "date": { "start": "2024-03-15" } }
            }
            """);
        var map = new NotionPropertyMapConfig { PublishAt = "NonExistent" };

        var result = NotionPropertyParser.ExtractPublishAt(properties, map);

        Assert.Null(result);
    }
}
