namespace Bukit.Shared.Tests;

// Compiled as part of the test project so 1.x source consumers keep proving the old namespace surface.
internal static class LegacyNotionConsumerFixture
{
    internal static readonly Type[] PublicTypes =
    [
        typeof(Bukit.Shared.Notion.NotionBlock),
        typeof(Bukit.Shared.Notion.RichTextSegment),
        typeof(Bukit.Shared.Notion.HtmlToNotionBlockConverter),
        typeof(Bukit.Shared.Notion.NotionApiUrls)
    ];
}
