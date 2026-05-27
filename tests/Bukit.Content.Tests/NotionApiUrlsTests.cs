using Bukit.Engine.Abstractions.Content;
using Bukit.Shared.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionApiUrlsTests
{
    [Fact]
    public void Pages_ReturnsCorrectUrl()
    {
        var url = NotionApiUrls.Pages("abc-123");

        Assert.Equal("https://api.notion.com/v1/pages/abc-123", url);
    }

    [Fact]
    public void DatabaseQuery_ReturnsCorrectUrl()
    {
        var url = NotionApiUrls.DatabaseQuery("db-456");

        Assert.Equal("https://api.notion.com/v1/databases/db-456/query", url);
    }

    [Fact]
    public void Database_ReturnsCorrectUrl()
    {
        var url = NotionApiUrls.Database("db-789");

        Assert.Equal("https://api.notion.com/v1/databases/db-789", url);
    }

    [Fact]
    public void BlockChildren_DefaultPageSize_ReturnsCorrectUrl()
    {
        var url = NotionApiUrls.BlockChildren("block-1");

        Assert.Equal("https://api.notion.com/v1/blocks/block-1/children?page_size=100", url);
    }

    [Fact]
    public void BlockChildren_CustomPageSize_ReturnsCorrectUrl()
    {
        var url = NotionApiUrls.BlockChildren("block-1", 50);

        Assert.Equal("https://api.notion.com/v1/blocks/block-1/children?page_size=50", url);
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.Equal("https://api.notion.com", NotionApiUrls.Base);
        Assert.Equal("v1", NotionApiUrls.ApiVersion);
        Assert.Equal("2022-06-28", NotionApiUrls.NotionVersion);
        Assert.Equal(100, NotionApiUrls.DefaultPageSize);
    }

    [Fact]
    public void Pages_WithEmptyPageId_StillGeneratesUrl()
    {
        var url = NotionApiUrls.Pages("");

        Assert.Equal("https://api.notion.com/v1/pages/", url);
    }
}
