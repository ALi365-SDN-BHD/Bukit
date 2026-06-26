using Bukit.Notion;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionPluginConstantsTests
{
    [Fact]
    public void AllowedTokenEnvironmentVariables_OnlyAllowsNotionToken()
    {
        Assert.Equal(["NOTION_TOKEN"], NotionPluginConstants.AllowedTokenEnvironmentVariables);
        Assert.True(NotionPluginConstants.IsAllowedTokenEnvironmentVariable("NOTION_TOKEN"));
        Assert.False(NotionPluginConstants.IsAllowedTokenEnvironmentVariable("BUKIT_NOTION_TOKEN"));
    }
}
