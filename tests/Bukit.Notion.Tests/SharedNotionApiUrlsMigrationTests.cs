using Bukit.Notion;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class SharedNotionApiUrlsMigrationTests
{
    [Fact]
    public void Base_IsCorrect()
    {
        Assert.Equal("https://api.notion.com", NotionApiUrls.Base);
    }

    [Fact]
    public void ApiVersion_IsCorrect()
    {
        Assert.Equal("v1", NotionApiUrls.ApiVersion);
    }

    [Fact]
    public void NotionVersion_IsCorrect()
    {
        Assert.Equal("2022-06-28", NotionApiUrls.NotionVersion);
    }

    [Fact]
    public void DefaultPageSize_Is100()
    {
        Assert.Equal(100, NotionApiUrls.DefaultPageSize);
    }

    [Fact]
    public void Pages_ReturnsCorrectUrl()
    {
        Assert.Equal("https://api.notion.com/v1/pages/abc-123", NotionApiUrls.Pages("abc-123"));
    }

    [Fact]
    public void DatabaseQuery_ReturnsCorrectUrl()
    {
        Assert.Equal("https://api.notion.com/v1/databases/db-456/query", NotionApiUrls.DatabaseQuery("db-456"));
    }

    [Fact]
    public void Database_ReturnsCorrectUrl()
    {
        Assert.Equal("https://api.notion.com/v1/databases/db-789", NotionApiUrls.Database("db-789"));
    }

    [Fact]
    public void BlockChildren_DefaultPageSize_ReturnsCorrectUrl()
    {
        Assert.Equal("https://api.notion.com/v1/blocks/block-1/children?page_size=100", NotionApiUrls.BlockChildren("block-1"));
    }

    [Fact]
    public void BlockChildren_CustomPageSize_ReturnsCorrectUrl()
    {
        Assert.Equal("https://api.notion.com/v1/blocks/block-1/children?page_size=50", NotionApiUrls.BlockChildren("block-1", 50));
    }
}
