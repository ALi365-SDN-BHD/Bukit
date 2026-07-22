using Bukit.Notion.Conversion;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionAssemblyTests
{
    [Fact]
    public void Assembly_MustExposeIndependentHtmlConverter()
    {
        var converter = typeof(HtmlToNotionBlockConverter).Assembly.GetType(
            "Bukit.Notion.Conversion.HtmlToNotionBlockConverter",
            throwOnError: false);

        Assert.NotNull(converter);
        Assert.Equal("Bukit.Notion", converter.Assembly.GetName().Name);
        Assert.NotNull(converter.GetMethod("Convert", [typeof(string)]));
        Assert.NotNull(converter.GetMethod("ToBlocksJson", [typeof(string)]));
    }
}
