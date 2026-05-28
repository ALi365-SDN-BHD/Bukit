using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentItemExtensionsTests
{
    private static ContentItem CreateItem(IReadOnlyDictionary<string, object>? meta = null)
    {
        return new ContentItem(
            Id: "test-id",
            Title: "Test",
            Slug: "test-slug",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Meta: meta ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: null);
    }

    [Fact]
    public void GetCollection_CollectionPresent_ReturnsCollection()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "news",
            ["type"] = "post"
        });

        var result = item.GetCollection();

        Assert.Equal("news", result);
    }

    [Fact]
    public void GetCollection_NoCollection_FallsBackToType()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });

        var result = item.GetCollection();

        Assert.Equal("post", result);
    }

    [Fact]
    public void GetCollection_NoCollectionNoType_ReturnsDefault()
    {
        var item = CreateItem();

        var result = item.GetCollection();

        Assert.Equal("page", result);
    }

    [Fact]
    public void GetCollection_CustomDefault_ReturnsCustomDefault()
    {
        var item = CreateItem();

        var result = item.GetCollection("fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void GetCollection_CollectionEmpty_FallsBackToType()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "",
            ["type"] = "post"
        });

        var result = item.GetCollection();

        Assert.Equal("post", result);
    }

    [Fact]
    public void GetCollection_CollectionWhitespace_FallsBackToType()
    {
        var item = CreateItem(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "   ",
            ["type"] = "page"
        });

        var result = item.GetCollection();

        Assert.Equal("page", result);
    }
}
