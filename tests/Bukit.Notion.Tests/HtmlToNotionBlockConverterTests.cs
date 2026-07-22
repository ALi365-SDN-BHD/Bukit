using System.Text.Json;
using Bukit.Notion.Blocks;
using Bukit.Notion.Conversion;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class HtmlToNotionBlockConverterTests
{
    [Fact]
    public void Convert_ProducesIndependentBlockTypes()
    {
        var blocks = HtmlToNotionBlockConverter.Convert(
            "<h2>Intro</h2><p>Hello <strong>bold</strong></p>");

        var heading = Assert.IsType<Heading2Block>(blocks[0]);
        Assert.Equal("Intro", heading.Text);
        var paragraph = Assert.IsType<ParagraphBlock>(blocks[1]);
        Assert.Collection(
            paragraph.Segments,
            segment => Assert.Equal("Hello", segment.Text),
            segment =>
            {
                Assert.Equal("bold", segment.Text);
                Assert.True(segment.Bold);
            });
    }

    [Fact]
    public void ToBlocksJson_PreservesNotionWireShape()
    {
        var json = HtmlToNotionBlockConverter.ToBlocksJson(
            "<img src=\"https://example.com/hero.png\" alt=\"Hero\" />");

        using var document = JsonDocument.Parse(json);
        var block = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("block", block.GetProperty("object").GetString());
        Assert.Equal("image", block.GetProperty("type").GetString());
        Assert.Equal(
            "https://example.com/hero.png",
            block.GetProperty("image").GetProperty("external").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Convert_PreCodeCompletesWithoutLosingText()
    {
        var blocks = await Task.Run(() =>
                HtmlToNotionBlockConverter.Convert("<pre><code>line1\nline2</code></pre>"))
            .WaitAsync(TimeSpan.FromSeconds(2));

        var code = Assert.IsType<CodeBlock>(Assert.Single(blocks));
        Assert.Equal("line1\nline2", code.Code);
    }
}
