using Bukit.Shared.Notion;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class LegacyNotionCompatibilityTests
{
    [Fact]
    public void LegacyTypes_RemainOwnedBySharedAssembly()
    {
        Assert.Equal("Bukit.Shared", typeof(HtmlToNotionBlockConverter).Assembly.GetName().Name);
        Assert.Equal("Bukit.Shared", typeof(NotionBlock).Assembly.GetName().Name);
        Assert.Equal("Bukit.Shared", typeof(NotionApiUrls).Assembly.GetName().Name);
    }

    [Theory]
    [InlineData("<h1>Title</h1><p>Body</p>")]
    [InlineData("<ul><li>One</li><li>Two</li></ul>")]
    [InlineData("<pre><code>line1\nline2</code></pre>")]
    [InlineData("<img src=\"https://example.com/image.png\" alt=\"Image\" />")]
    public void LegacyConverter_DelegatesWithoutChangingJson(string html)
    {
        var legacy = HtmlToNotionBlockConverter.ToBlocksJson(html);
        var independent = Bukit.Notion.Conversion.HtmlToNotionBlockConverter.ToBlocksJson(html);

        Assert.Equal(independent, legacy);
    }
}
