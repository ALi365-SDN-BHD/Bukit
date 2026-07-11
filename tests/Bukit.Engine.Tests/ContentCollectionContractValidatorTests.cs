using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentCollectionContractValidatorTests
{
    [Fact]
    public void Validate_ContentModeWithCollection_AcceptsDocument()
    {
        var document = Document("article-1", new Dictionary<string, object>
        {
            ["sourceMode"] = "content",
            ["collection"] = "articles"
        });

        ContentCollectionContractValidator.Validate([document]);
    }

    [Fact]
    public void Validate_MissingSourceModeWithCollection_AcceptsDocument()
    {
        var document = Document("article-1", new Dictionary<string, object>
        {
            ["collection"] = "articles"
        });

        ContentCollectionContractValidator.Validate([document]);
    }

    [Fact]
    public void Validate_DataModeWithoutCollection_AcceptsDocument()
    {
        var document = Document("settings-1", new Dictionary<string, object>
        {
            ["sourceMode"] = " DATA "
        });

        ContentCollectionContractValidator.Validate([document]);
    }

    [Fact]
    public void Validate_PropertiesOnlyContentModeAndCollection_AcceptsDocument()
    {
        var properties = new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceMode"] = new("text", "content"),
            ["collection"] = new("text", "articles")
        };
        var document = new RawContentDocument(
            Id: "article-1",
            Title: "article-1",
            Slug: "article-1",
            PublishAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(InlineHtml: string.Empty),
            Properties: properties);

        ContentCollectionContractValidator.Validate([document]);
    }

    [Fact]
    public void Validate_ContentModeWithWhitespaceCollection_ThrowsStableDiagnostic()
    {
        var document = Document("article-1", new Dictionary<string, object>
        {
            ["sourceMode"] = "content",
            ["collection"] = "   ",
            ["sourceKey"] = "notion-news"
        });

        var exception = Assert.Throws<ConfigException>(() =>
            ContentCollectionContractValidator.Validate([document]));

        Assert.Equal(DiagnosticCode.ContentCollectionMissing, exception.Code);
        Assert.Equal(
            "Content \"article-1\" from source \"notion-news\" is missing required collection. Set content.sources[].collection or item collection metadata.",
            exception.Message);
    }

    [Fact]
    public void Validate_MissingCollectionUsesSourceInfoKey()
    {
        var document = Document(
            "article-1",
            new Dictionary<string, object> { ["sourceMode"] = "content" },
            new ContentSourceInfo("notion", SourceKey: "editorial"));

        var exception = Assert.Throws<ConfigException>(() =>
            ContentCollectionContractValidator.Validate([document]));

        Assert.Contains("source \"editorial\"", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MissingSourceKeyUsesStableFallback()
    {
        var document = Document("article-1", new Dictionary<string, object>
        {
            ["sourceMode"] = "content"
        });

        var exception = Assert.Throws<ConfigException>(() =>
            ContentCollectionContractValidator.Validate([document]));

        Assert.Contains("source \"unknown\"", exception.Message, StringComparison.Ordinal);
    }

    private static RawContentDocument Document(
        string id,
        IReadOnlyDictionary<string, object> values,
        ContentSourceInfo? source = null)
    {
        var fields = ContentFieldReader.ToFieldMap(values);
        return new RawContentDocument(
            Id: id,
            Title: id,
            Slug: id,
            PublishAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(InlineHtml: string.Empty),
            Properties: RawContentValue.FromFields(fields),
            Source: source,
            CustomFields: fields);
    }
}
