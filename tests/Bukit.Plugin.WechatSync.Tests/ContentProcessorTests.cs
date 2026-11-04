using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class ContentProcessorTests
{
    [Fact]
    public void ProcessContent_DecodesTextEntitiesWithoutChangingAttributesOrCodeBlocks()
    {
        const string html =
            "<a title=\"say &amp;quot;hi&amp;quot;\">text &amp;quot;hi&amp;quot;</a>" +
            "<a title=\"a > &amp;quot;b&amp;quot;\">second &amp;quot;text&amp;quot;</a>" +
            "<p>a < b <code>&amp;quot;code&amp;quot;</code></p>" +
            "<pre><code>&amp;quot;pre&amp;quot;</code></pre>";

        var result = ContentProcessor.ProcessContent(html);

        Assert.Contains("title=\"say &amp;quot;hi&amp;quot;\"", result, StringComparison.Ordinal);
        Assert.Contains(">text \"hi\"</a>", result, StringComparison.Ordinal);
        Assert.Contains("title=\"a > &amp;quot;b&amp;quot;\"", result, StringComparison.Ordinal);
        Assert.Contains(">second \"text\"</a>", result, StringComparison.Ordinal);
        Assert.Contains("&amp;quot;code&amp;quot;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("title=\"say \"hi\"\"", result, StringComparison.Ordinal);
    }
}
