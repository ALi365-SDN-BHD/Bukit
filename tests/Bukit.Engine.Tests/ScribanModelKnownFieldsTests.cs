using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ScribanModelKnownFieldsTests
{
    [Fact]
    public void PageModel_ContainsKnownFields()
    {
        var fields = ScribanModelKnownFields.ForPage();

        Assert.Contains("title", fields);
        Assert.Contains("url", fields);
        Assert.Contains("content", fields);
        Assert.Contains("summary", fields);
        Assert.Contains("publish_date", fields);
        Assert.Contains("table_of_contents", fields);
        Assert.Contains("fields", fields);
    }

    [Fact]
    public void SiteModel_ContainsKnownFields()
    {
        var fields = ScribanModelKnownFields.ForSite();

        Assert.Contains("name", fields);
        Assert.Contains("title", fields);
        Assert.Contains("url", fields);
        Assert.Contains("base_url", fields);
        Assert.Contains("base_path", fields);
        Assert.Contains("build_year", fields);
        Assert.Contains("language", fields);
        Assert.Contains("params", fields);
        Assert.Contains("data", fields);
        Assert.Contains("data_index", fields);
        Assert.DoesNotContain("analytics", fields);
        Assert.False(ScribanModelKnownFields.IsKnownField("site", "analytics"));
        Assert.False(ScribanModelKnownFields.IsKnownField("site", "analytics.enabled"));
    }

    [Fact]
    public void SiteModel_DataIndex_AllowsDynamicNestedFields()
    {
        Assert.True(ScribanModelKnownFields.IsKnownField(
            "site", "data_index.settings.contact.email"));
    }

    [Fact]
    public void ListPageModel_ContainsKnownFields()
    {
        var fields = ScribanModelKnownFields.ForListPage();

        Assert.Contains("site", fields);
        Assert.Contains("page", fields);
        Assert.Contains("pages", fields);
    }

    [Fact]
    public void KnownRootContexts_ArePageSiteAndList()
    {
        var roots = ScribanModelKnownFields.KnownRootContexts;

        Assert.Contains("page", roots);
        Assert.Contains("site", roots);
        Assert.Contains("list", roots);
    }

    [Fact]
    public void IsKnownField_ValidField_ReturnsTrue()
    {
        Assert.True(ScribanModelKnownFields.IsKnownField("page", "title"));
        Assert.True(ScribanModelKnownFields.IsKnownField("site", "name"));
        Assert.True(ScribanModelKnownFields.IsKnownField("page", "seo.title"));
        Assert.True(ScribanModelKnownFields.IsKnownField("page", "seo.document_title"));
        Assert.True(ScribanModelKnownFields.IsKnownField("page", "content_model.presentation.custom_heading"));
        Assert.True(ScribanModelKnownFields.IsKnownField("site", "base_path"));
        Assert.True(ScribanModelKnownFields.IsKnownField("site", "build_year"));
    }

    [Fact]
    public void IsKnownField_InvalidField_ReturnsFalse()
    {
        Assert.False(ScribanModelKnownFields.IsKnownField("page", "mispeeled_title"));
        Assert.False(ScribanModelKnownFields.IsKnownField("site", "namme"));
        Assert.False(ScribanModelKnownFields.IsKnownField("page", "nonexistent.deep.field"));
    }

    [Fact]
    public void IsKnownField_CustomLoopVar_ReturnsTrueForCommonPatterns()
    {
        Assert.True(ScribanModelKnownFields.IsKnownField("p", "title"));
        Assert.True(ScribanModelKnownFields.IsKnownField("item", "url"));
        Assert.True(ScribanModelKnownFields.IsKnownField("p", "fields"));
    }
}
