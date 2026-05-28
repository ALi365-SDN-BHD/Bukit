using Bukit.Config;
using Bukit.Content.Notion;
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
    public void ExtractPublishAt_FallsBackToDateField_WithPropertyMap()
    {
        var properties = ParseJson("""
            {
                "Date": { "type": "date", "date": { "start": "2024-03-15" } }
            }
            """);
        // propertyMap set but field doesn't exist — should fall back to "Date"
        var map = new NotionPropertyMapConfig { PublishAt = "NonExistent" };

        var result = NotionPropertyParser.ExtractPublishAt(properties, map);

        Assert.NotNull(result);
        Assert.Equal(2024, result!.Value.Year);
    }
}
