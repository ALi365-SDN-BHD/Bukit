using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class LlmsCurationPolicyTests
{
    [Fact]
    public void Parse_OmittedMetadata_ReturnsValidDefault()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(null));

        Assert.True(result.Valid);
        Assert.Equal(LlmsCurationPolicy.Default, result.Policy);
        Assert.Empty(result.ErrorCodes);
    }

    [Fact]
    public void Parse_EmptyLlmsMap_ReturnsValidDefault()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>()));

        Assert.True(result.Valid);
        Assert.Equal(LlmsCurationPolicy.Default, result.Policy);
    }

    [Fact]
    public void Parse_DeclaredGeoWithWrongShape_IsInvalid()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocumentWithGeo("invalid"));

        Assert.False(result.Valid);
        Assert.Equal(LlmsCurationPolicy.Default, result.Policy);
        Assert.Equal(["geo.llms_field_unknown"], result.ErrorCodes);
    }

    [Fact]
    public void Parse_DeclaredLlmsWithWrongShape_IsInvalid()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocumentWithGeo(new Dictionary<string, object>
        {
            ["llms"] = "invalid"
        }));

        Assert.False(result.Valid);
        Assert.Equal(LlmsCurationPolicy.Default, result.Policy);
        Assert.Equal(["geo.llms_field_unknown"], result.ErrorCodes);
    }

    [Theory]
    [InlineData("visibility", null, "geo.llms_visibility_invalid")]
    [InlineData("visibility", "", "geo.llms_visibility_invalid")]
    [InlineData("visibility", 1, "geo.llms_visibility_invalid")]
    [InlineData("tier", null, "geo.llms_tier_invalid")]
    [InlineData("tier", " ", "geo.llms_tier_invalid")]
    [InlineData("tier", true, "geo.llms_tier_invalid")]
    [InlineData("priority", null, "geo.llms_priority_invalid")]
    public void Parse_DeclaredInvalidFieldValue_IsInvalid(string field, object? value, string errorCode)
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            [field] = value!
        }));

        Assert.False(result.Valid);
        Assert.Equal(LlmsCurationPolicy.Default, result.Policy);
        Assert.Contains(errorCode, result.ErrorCodes);
    }

    [Theory]
    [InlineData("auto", "Auto")]
    [InlineData("include", "Include")]
    [InlineData("exclude", "Exclude")]
    public void Parse_VisibilityValues_MapToEnum(string value, string expected)
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["visibility"] = value
        }));

        Assert.True(result.Valid);
        Assert.Equal(expected, result.Policy.Visibility.ToString());
        Assert.Empty(result.ErrorCodes);
    }

    [Theory]
    [InlineData("primary", "Primary")]
    [InlineData("optional", "Optional")]
    public void Parse_TierValues_MapToEnum(string value, string expected)
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["tier"] = value
        }));

        Assert.True(result.Valid);
        Assert.Equal(expected, result.Policy.Tier.ToString());
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(100)]
    public void Parse_PriorityBoundaries_AreAccepted(int priority)
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["priority"] = priority
        }));

        Assert.True(result.Valid);
        Assert.Equal(priority, result.Policy.Priority);
    }

    [Theory]
    [InlineData(-101)]
    [InlineData(101)]
    public void Parse_PriorityOutOfRange_IsInvalid(int priority)
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["priority"] = priority
        }));

        Assert.False(result.Valid);
        Assert.Equal(LlmsCurationPolicy.Default, result.Policy);
        Assert.Contains("geo.llms_priority_invalid", result.ErrorCodes);
    }

    [Fact]
    public void Parse_PriorityNonInteger_IsInvalid()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["priority"] = "high"
        }));

        Assert.False(result.Valid);
        Assert.Contains("geo.llms_priority_invalid", result.ErrorCodes);
    }

    [Fact]
    public void Parse_UnknownVisibility_IsInvalid()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["visibility"] = "always"
        }));

        Assert.False(result.Valid);
        Assert.Contains("geo.llms_visibility_invalid", result.ErrorCodes);
    }

    [Fact]
    public void Parse_UnknownTier_IsInvalid()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["tier"] = "featured"
        }));

        Assert.False(result.Valid);
        Assert.Contains("geo.llms_tier_invalid", result.ErrorCodes);
    }

    [Fact]
    public void Parse_UnknownField_IsInvalid()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["weight"] = 5
        }));

        Assert.False(result.Valid);
        Assert.Contains("geo.llms_field_unknown", result.ErrorCodes);
    }

    [Fact]
    public void Parse_MultipleErrors_ReturnsDefaultPolicyWithAllCodes()
    {
        var result = LlmsCurationPolicyParser.Parse(CreateDocument(new Dictionary<string, object>
        {
            ["visibility"] = "always",
            ["tier"] = "featured",
            ["priority"] = 999,
            ["weight"] = 1
        }));

        Assert.False(result.Valid);
        Assert.Equal(LlmsCurationPolicy.Default, result.Policy);
        Assert.Contains("geo.llms_visibility_invalid", result.ErrorCodes);
        Assert.Contains("geo.llms_tier_invalid", result.ErrorCodes);
        Assert.Contains("geo.llms_priority_invalid", result.ErrorCodes);
        Assert.Contains("geo.llms_field_unknown", result.ErrorCodes);
    }

    private static ContentDocument CreateDocument(Dictionary<string, object>? llms)
    {
        var fields = new Dictionary<string, object>
        {
            ["type"] = "page"
        };
        if (llms is not null)
        {
            fields["geo"] = new Dictionary<string, object>
            {
                ["llms"] = llms
            };
        }

        return ContentDocument.Create(
            id: "llms-page",
            title: "LLMS Curation",
            slug: "llms-curation",
            publishAt: new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>llms</p>",
            fields: ContentFieldReader.ToFieldMap(fields));
    }

    private static ContentDocument CreateDocumentWithGeo(object geo)
        => ContentDocument.Create(
            id: "llms-page",
            title: "LLMS Curation",
            slug: "llms-curation",
            publishAt: new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>llms</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["geo"] = geo
            }));
}
