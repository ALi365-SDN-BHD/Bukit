using Bukit.Shared.Notion;
using Xunit;
using IndependentBlocks = Bukit.Notion.Blocks;
using LegacyBlocks = Bukit.Shared.Notion;

namespace Bukit.Shared.Tests;

public sealed class LegacyNotionCompatibilityTests
{
    [Fact]
    public void LegacyTypes_RemainOwnedBySharedAssembly()
    {
        Assert.Equal("Bukit.Shared", typeof(HtmlToNotionBlockConverter).Assembly.GetName().Name);
        Assert.Equal("Bukit.Shared", typeof(NotionBlock).Assembly.GetName().Name);
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

    [Fact]
    public void CompatibilityMapper_CanonicalBlockCases_AreExhaustiveAndRoundTrip()
    {
        IndependentBlocks.NotionBlock[] cases =
        [
            new IndependentBlocks.Heading1Block("Heading 1"),
            new IndependentBlocks.Heading2Block("Heading 2"),
            new IndependentBlocks.Heading3Block("Heading 3"),
            new IndependentBlocks.ParagraphBlock([new IndependentBlocks.RichTextSegment("Paragraph", true, true, "https://example.com")]),
            new IndependentBlocks.BulletedListItemBlock("Bullet"),
            new IndependentBlocks.NumberedListItemBlock("Number"),
            new IndependentBlocks.QuoteBlock("Quote"),
            new IndependentBlocks.ImageBlock("https://example.com/image.png", "Caption"),
            new IndependentBlocks.ToggleBlock(
                "Toggle",
                [new IndependentBlocks.ParagraphBlock("Child")]),
            new IndependentBlocks.CodeBlock("code", "csharp"),
            new IndependentBlocks.CalloutBlock("Callout", "!")
        ];

        Assert.Equal(
            FindConcreteBlockTypes(typeof(IndependentBlocks.NotionBlock)),
            cases.Select(static block => block.GetType()).OrderBy(static type => type.FullName, StringComparer.Ordinal));

        foreach (var block in cases)
        {
            var roundTrip = NotionCompatibilityMapper.ToIndependent(
                NotionCompatibilityMapper.ToLegacy(block));
            Assert.Equivalent(block, roundTrip, strict: true);
        }
    }

    [Fact]
    public void CompatibilityMapper_LegacyBlockCases_AreExhaustiveAndRoundTrip()
    {
        LegacyBlocks.NotionBlock[] cases =
        [
            new LegacyBlocks.Heading1Block("Heading 1"),
            new LegacyBlocks.Heading2Block("Heading 2"),
            new LegacyBlocks.Heading3Block("Heading 3"),
            new LegacyBlocks.ParagraphBlock([new LegacyBlocks.RichTextSegment("Paragraph", true, true, "https://example.com")]),
            new LegacyBlocks.BulletedListItemBlock("Bullet"),
            new LegacyBlocks.NumberedListItemBlock("Number"),
            new LegacyBlocks.QuoteBlock("Quote"),
            new LegacyBlocks.ImageBlock("https://example.com/image.png", "Caption"),
            new LegacyBlocks.ToggleBlock(
                "Toggle",
                [new LegacyBlocks.ParagraphBlock("Child")]),
            new LegacyBlocks.CodeBlock("code", "csharp"),
            new LegacyBlocks.CalloutBlock("Callout", "!")
        ];

        Assert.Equal(
            FindConcreteBlockTypes(typeof(LegacyBlocks.NotionBlock)),
            cases.Select(static block => block.GetType()).OrderBy(static type => type.FullName, StringComparer.Ordinal));

        foreach (var block in cases)
        {
            var roundTrip = NotionCompatibilityMapper.ToLegacy(
                NotionCompatibilityMapper.ToIndependent(block));
            Assert.Equivalent(block, roundTrip, strict: true);
        }
    }

    private static IEnumerable<Type> FindConcreteBlockTypes(Type rootType)
        => rootType.Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && rootType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
}
