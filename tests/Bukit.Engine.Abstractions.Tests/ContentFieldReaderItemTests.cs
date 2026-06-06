using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentFieldReaderItemTests
{
    private static ContentDocument CreateDocument(IReadOnlyDictionary<string, object>? values = null)
    {
        return ContentDocument.Create(
            id: "test-id",
            title: "Test",
            slug: "test-slug",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>content</p>",
            fields: ContentFieldReader.ToFieldMap(values ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GetCollection_CollectionPresent_ReturnsCollection()
    {
        var document = CreateDocument(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "news",
            ["type"] = "post"
        });

        var result = ContentFieldReader.GetCollection(document);

        Assert.Equal("news", result);
    }

    [Fact]
    public void GetCollection_NoCollection_UsesCanonicalType()
    {
        var document = CreateDocument(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });

        var result = ContentFieldReader.GetCollection(document);

        Assert.Equal("post", result);
    }

    [Fact]
    public void GetCollection_NoCollectionNoType_ReturnsDefault()
    {
        var document = CreateDocument();

        var result = ContentFieldReader.GetCollection(document);

        Assert.Equal("page", result);
    }

    [Fact]
    public void GetCollection_CustomDefault_ReturnsCustomDefault()
    {
        var document = CreateDocument();

        var result = ContentFieldReader.GetCollection(document, "fallback");

        Assert.Equal("page", result);
    }

    [Fact]
    public void GetCollection_CollectionEmpty_ReturnsDefault()
    {
        var document = CreateDocument(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "",
            ["type"] = "post"
        });

        var result = ContentFieldReader.GetCollection(document);

        Assert.Equal("post", result);
    }

    [Fact]
    public void GetCollection_CollectionWhitespace_ReturnsDefault()
    {
        var document = CreateDocument(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "   ",
            ["type"] = "page"
        });

        var result = ContentFieldReader.GetCollection(document);

        Assert.Equal("page", result);
    }

    [Fact]
    public void GetTextValues_FieldStringList_ReturnsStructuredValues()
    {
        var document = ContentDocument.Create(
            id: "test-id",
            title: "Test",
            slug: "test-slug",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>content</p>",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["collections"] = new("list", new[] { "news", "featured" })
            });

        var result = ContentFieldReader.GetTextValues(document, "collections");

        Assert.Equal(["news", "featured"], result);
    }
}
