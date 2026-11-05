using Xunit;

namespace Bukit.Importing.Tests;

public sealed class NotionUrlValueTests
{
    private static ImportSeedRecord Record(string field, string value)
        => new("navigation", "Home", "home", null, null, "zh", true, null, null,
            new Dictionary<string, object?> { [field] = value });

    [Theory]
    [InlineData("https://example.com/a", true)]
    [InlineData("/company/about/", false)]
    [InlineData("javascript:alert(1)", false)]
    public void UrlProperty_AcceptsOnlyHttpAddresses(string value, bool valid)
        => Assert.Equal(valid, NotionSchemaTypeValidator.GetCompatibilityError("url", value) is null);

    [Fact]
    public void BuildAdditionalSchemaFields_SitePathLinksAreText()
    {
        var fields = NotionSchemaTypeValidator.BuildAdditionalSchemaFields("navigation",
            [Record("link", "https://example.com/"), Record("link", "/about/")]);

        Assert.Contains(("Link", "rich_text"), fields);
    }

    [Fact]
    public void BuildAdditionalSchemaFields_HttpLinksStayUrl()
    {
        var fields = NotionSchemaTypeValidator.BuildAdditionalSchemaFields("pages",
            [Record("url", "https://example.com/a/")]);

        Assert.Contains(("Url", "url"), fields);
    }
}
