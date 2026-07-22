namespace Bukit.Content.Tests;

// Compiled as part of the test project so the remaining legacy namespace surface stays proven on the 2.0 line.
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
        typeof(Bukit.Content.Notion.NotionRenderContext)
    ];
}
