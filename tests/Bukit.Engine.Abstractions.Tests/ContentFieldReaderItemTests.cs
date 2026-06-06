using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentFieldReaderItemTests
{
    private static ContentItem CreateItem(IReadOnlyDictionary<string, object>? values = null)
    {
        return new ContentItem(
            Id: "test-id",
            Title: "Test",
            Slug: "test-slug",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Fields: ContentFieldReader.ToFieldMap(values ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GetCollection_CollectionPresent_ReturnsCollection()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "news",
            ["type"] = "post"
        });

        var result = ContentFieldReader.GetCollection(item);

        Assert.Equal("news", result);
    }

    [Fact]
    public void GetCollection_NoCollection_IgnoresType()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });

        var result = ContentFieldReader.GetCollection(item);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetCollection_NoCollectionNoType_ReturnsDefault()
    {
        var item = CreateItem();

        var result = ContentFieldReader.GetCollection(item);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetCollection_CustomDefault_ReturnsCustomDefault()
    {
        var item = CreateItem();

        var result = ContentFieldReader.GetCollection(item, "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void GetCollection_CollectionEmpty_ReturnsDefault()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "",
            ["type"] = "post"
        });

        var result = ContentFieldReader.GetCollection(item);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetCollection_CollectionWhitespace_ReturnsDefault()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "   ",
            ["type"] = "page"
        });

        var result = ContentFieldReader.GetCollection(item);

        Assert.Equal("", result);
    }

    [Fact]
    public void GetTextValues_FieldStringList_ReturnsStructuredValues()
    {
        var item = new ContentItem(
            Id: "test-id",
            Title: "Test",
            Slug: "test-slug",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["collections"] = new("list", new[] { "news", "featured" })
            });

        var result = ContentFieldReader.GetTextValues(item, "collections");

        Assert.Equal(["news", "featured"], result);
    }
}
