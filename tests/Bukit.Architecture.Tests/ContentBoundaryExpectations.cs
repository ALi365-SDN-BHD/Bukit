namespace Bukit.Architecture.Tests;

internal static class ContentBoundaryExpectations
{
    internal static readonly string[] FriendAssemblies =
    [
        // Notion offline snapshot CLI tests drive whole builds through the Notion transport seam.
        "Bukit.Cli.Tests",
        "Bukit.Content.Notion.Tests",
        "Bukit.Content.Tests",
        "Bukit.Engine",
        "Bukit.Engine.Assets",
        "Bukit.Engine.Tests"
    ];
}
