using Bukit.Content.Markdown;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class MarkdownFieldBuilderTests
{
    [Fact]
    public void BuildFields_NullValue_Skipped()
    {
        var projectedValues = new Dictionary<string, object> { ["key"] = null! };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildFields_EmptyKey_Skipped()
    {
        var projectedValues = new Dictionary<string, object> { ["  "] = "value" };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildFields_ReservedKey_Skipped()
    {
        var projectedValues = new Dictionary<string, object> { ["title"] = "My Title", ["slug"] = "my-slug" };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildFields_ListValue_ReturnsListField()
    {
        var projectedValues = new Dictionary<string, object> { ["items"] = new[] { "a", "b", "c" } };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("list", result["items"].Type);
    }

    [Fact]
    public void BuildFields_Tags_ReturnsTagsField()
    {
        var projectedValues = new Dictionary<string, object> { ["tags"] = new[] { "tag1", "tag2" } };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("list", result["tags"].Type);
    }

    [Fact]
    public void BuildFields_Categories_ReturnsCategoriesField()
    {
        var projectedValues = new Dictionary<string, object> { ["categories"] = new[] { "cat1" } };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("list", result["categories"].Type);
    }

    [Fact]
    public void BuildFields_Summary_ReturnsTextField()
    {
        var projectedValues = new Dictionary<string, object> { ["summary"] = "A brief summary" };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("text", result["summary"].Type);
    }

    [Fact]
    public void BuildFields_BoolValue_ReturnsBoolField()
    {
        var projectedValues = new Dictionary<string, object> { ["featured"] = true };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("bool", result["featured"].Type);
    }

    [Fact]
    public void BuildFields_IntValue_ReturnsNumberField()
    {
        var projectedValues = new Dictionary<string, object> { ["priority"] = 42 };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("number", result["priority"].Type);
    }

    [Fact]
    public void BuildFields_DoubleValue_ReturnsNumberField()
    {
        var projectedValues = new Dictionary<string, object> { ["score"] = 3.14 };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("number", result["score"].Type);
    }

    [Fact]
    public void BuildFields_DateTime_ReturnsDateField()
    {
        var projectedValues = new Dictionary<string, object> { ["eventDate"] = new DateTime(2026, 6, 1) };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("date", result["eventDate"].Type);
    }

    [Fact]
    public void BuildFields_StringBool_ConvertsToBoolField()
    {
        var projectedValues = new Dictionary<string, object> { ["published"] = "true" };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("bool", result["published"].Type);
    }

    [Fact]
    public void BuildFields_StringNumber_ConvertsToNumberField()
    {
        var projectedValues = new Dictionary<string, object> { ["count"] = "123" };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("number", result["count"].Type);
    }

    [Fact]
    public void BuildFields_PlainString_ReturnsTextField()
    {
        var projectedValues = new Dictionary<string, object> { ["author"] = "Alice" };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("text", result["author"].Type);
    }

    [Fact]
    public void BuildFields_StringDate_ReturnsDateField()
    {
        var projectedValues = new Dictionary<string, object> { ["deadline"] = "2026-12-31" };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("date", result["deadline"].Type);
    }

    [Fact]
    public void BuildFields_MultipleFields_ReturnsAll()
    {
        var projectedValues = new Dictionary<string, object>
        {
            ["author"] = "Bob",
            ["count"] = 7,
            ["active"] = true,
            ["tags"] = new[] { "x" }
        };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal(4, result.Count);
        Assert.Equal("text", result["author"].Type);
        Assert.Equal("number", result["count"].Type);
        Assert.Equal("bool", result["active"].Type);
        Assert.Equal("list", result["tags"].Type);
    }

    [Fact]
    public void BuildFields_ShouldPreserveCanonicalProvenanceAndTrustFields_WhenPresentInFrontMatter()
    {
        var projectedValues = new Dictionary<string, object>
        {
            ["source"] = "markdown",
            ["original_url"] = "https://example.com/source",
            ["review_status"] = "approved",
            ["credibility_score"] = 0.95,
            ["quality_flags"] = new[] { "reviewed" }
        };

        var result = MarkdownFieldBuilder.BuildFields(projectedValues);

        Assert.Equal("text", result["source"].Type);
        Assert.Equal("markdown", result["source"].Value);
        Assert.Equal("text", result["original_url"].Type);
        Assert.Equal("https://example.com/source", result["original_url"].Value);
        Assert.Equal("text", result["review_status"].Type);
        Assert.Equal("approved", result["review_status"].Value);
        Assert.Equal("number", result["credibility_score"].Type);
        Assert.Equal(0.95, result["credibility_score"].Value);
        Assert.Equal("list", result["quality_flags"].Type);
    }

    [Fact]
    public void TryParseDateTimeOffset_ValidDate_ReturnsTrue()
    {
        Assert.True(MarkdownFieldBuilder.TryParseDateTimeOffset("2026-01-15", out var dto));
        Assert.Equal(2026, dto.Year);
        Assert.Equal(1, dto.Month);
        Assert.Equal(15, dto.Day);
    }

    [Fact]
    public void TryParseDateTimeOffset_InvalidDate_ReturnsFalse()
    {
        Assert.False(MarkdownFieldBuilder.TryParseDateTimeOffset("not-a-date", out _));
    }
}
