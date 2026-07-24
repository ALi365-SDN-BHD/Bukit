namespace Bukit.Content.Tests;

// Compiled as part of the test project so the internal legacy bridge identities
// stay proven without treating them as public compile-time anchors on the 2.0 line.
internal static class LegacyNotionConsumerFixture
{
    private static readonly System.Reflection.Assembly ContentAssembly =
        typeof(Bukit.Content.Notion.NotionPropertyParser).Assembly;

    internal static readonly Type[] InternalBridgeTypes =
    [
        GetType("Bukit.Content.Notion.NotionApiClient"),
        GetType("Bukit.Content.Notion.NotionContentProvider"),
        GetType("Bukit.Content.Notion.NotionProviderOptions")
    ];

    private static Type GetType(string typeName)
        => ContentAssembly.GetType(
            typeName,
            throwOnError: true,
            ignoreCase: false)!;
}
