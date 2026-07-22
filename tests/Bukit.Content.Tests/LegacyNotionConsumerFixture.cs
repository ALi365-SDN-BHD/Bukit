namespace Bukit.Content.Tests;

// Compiled as part of the test project so 1.x source consumers keep proving the old namespace surface.
internal static class LegacyNotionConsumerFixture
{
    internal static readonly Type[] PublicTypes =
    [
        typeof(Bukit.Content.Notion.NotionApiClient),
        typeof(Bukit.Content.Notion.NotionContentProvider),
        typeof(Bukit.Content.Notion.NotionPropertyParser),
        typeof(Bukit.Content.Notion.NotionProviderOptions),
        typeof(Bukit.Content.Notion.NotionBlocksRenderer),
        typeof(Bukit.Content.Notion.NotionBlockRendererRegistry),
        typeof(Bukit.Content.Notion.NotionRenderContext),
        typeof(Bukit.Content.Notion.NotionRichTextRenderer),
        typeof(Bukit.Content.Notion.BlockRenderers.ImageBlockRenderer),
        typeof(Bukit.Content.Notion.BlockRenderers.TableBlockRenderer)
    ];
}
