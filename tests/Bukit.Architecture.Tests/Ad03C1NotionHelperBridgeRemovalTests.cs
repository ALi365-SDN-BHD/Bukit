using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad03C1NotionHelperBridgeRemovalTests
{
    private const string SharedWriterTypeName = "Bukit.Shared.Notion.NotionBlockJsonWriter";
    private const string ContentHelperTypeName = "Bukit.Content.Notion.BlockRenderers.NotionBlockHelpers";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void LegacyNotionTestHelperBridges_AreAbsentFromAssembliesAndSource()
    {
        var sharedAssembly = typeof(Bukit.Shared.BukitException).Assembly;
        var contentAssembly = typeof(Bukit.Content.Notion.NotionApiClient).Assembly;

        Assert.Null(sharedAssembly.GetType(
            SharedWriterTypeName,
            throwOnError: false,
            ignoreCase: false));
        Assert.Null(contentAssembly.GetType(
            ContentHelperTypeName,
            throwOnError: false,
            ignoreCase: false));

        Assert.False(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Shared",
            "Notion",
            "NotionBlockJsonWriter.cs")));
        Assert.False(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Notion",
            "BlockRenderers",
            "NotionBlockHelpers.cs")));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
