using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentDocumentFactoryTests
{
    [Fact]
    public void Type_IsInternalImplementationDetail()
    {
        var type = typeof(IContentBodyStore).Assembly.GetType(
            "Bukit.Engine.Abstractions.Content.ContentDocumentFactory",
            throwOnError: true)!;

        Assert.False(type.IsPublic);
        Assert.True(type.IsNotPublic);
    }

    [Fact]
    public void MergeFields_CaseSensitiveMutableInput_CustomFieldWinsCaseInsensitively()
    {
        // A case-sensitive mutable dictionary must not defeat the documented
        // case-insensitive precedence of explicit custom fields.
        var properties = new Dictionary<string, RawContentValue>(StringComparer.Ordinal)
        {
            ["Title"] = new RawContentValue("text", "from-properties")
        };
        var customFields = new Dictionary<string, ContentField>(StringComparer.Ordinal)
        {
            ["title"] = new ContentField("text", "from-custom")
        };

        var merged = ContentDocumentFactory.MergeFields(properties, customFields);

        Assert.NotNull(merged);
        var field = Assert.Single(merged!);
        Assert.Equal("from-custom", field.Value.Value);
    }

    [Fact]
    public void MergeFields_DoesNotMutateCallerDictionary()
    {
        var properties = new Dictionary<string, RawContentValue>(StringComparer.Ordinal)
        {
            ["summary"] = new RawContentValue("text", "merged-summary")
        };
        var customFields = new Dictionary<string, ContentField>(StringComparer.Ordinal)
        {
            ["title"] = new ContentField("text", "kept")
        };

        var merged = ContentDocumentFactory.MergeFields(properties, customFields);

        Assert.NotSame(customFields, merged);
        var callerEntry = Assert.Single(customFields);
        Assert.Equal("title", callerEntry.Key, StringComparer.Ordinal);
        Assert.False(customFields.ContainsKey("summary"));
    }

    [Fact]
    public void MergeFields_CustomFieldsOnly_ReturnsIndependentCaseInsensitiveCopy()
    {
        var customFields = new Dictionary<string, ContentField>(StringComparer.Ordinal)
        {
            ["Title"] = new ContentField("text", "original")
        };

        var merged = ContentDocumentFactory.MergeFields(null, customFields);

        Assert.NotNull(merged);
        Assert.NotSame(customFields, merged);
        Assert.Equal("original", merged!["title"].Value);

        customFields["Title"] = new ContentField("text", "mutated");
        Assert.Equal("original", merged["TITLE"].Value);
    }
}
