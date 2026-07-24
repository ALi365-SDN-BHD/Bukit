using System.Reflection;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad03C2SharedNotionApiUrlsRemovalTests
{
    private const string LegacyTypeName = "Bukit.Shared.Notion.NotionApiUrls";
    private const string CanonicalTypeName = "Bukit.Notion.NotionApiUrls";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void LegacySharedUrlFacade_IsAbsentAndCanonicalOwnerRemainsPublic()
    {
        Assembly sharedAssembly = typeof(Bukit.Shared.Notion.NotionBlock).Assembly;
        Assembly notionAssembly = typeof(Bukit.Notion.NotionApiUrls).Assembly;
        string[] sharedExports = sharedAssembly.GetExportedTypes()
            .Select(static type => type.FullName!)
            .ToArray();

        Assert.Null(sharedAssembly.GetType(
            LegacyTypeName,
            throwOnError: false,
            ignoreCase: false));
        Assert.DoesNotContain(LegacyTypeName, sharedExports);
        Assert.False(File.Exists(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Shared",
            "Notion",
            "NotionApiUrls.cs")));

        Type canonicalType = notionAssembly.GetType(
            CanonicalTypeName,
            throwOnError: true,
            ignoreCase: false)!;
        Assert.True(canonicalType.IsPublic);
        Assert.Contains(canonicalType, notionAssembly.GetExportedTypes());
        Assert.Equal("Bukit.Notion", canonicalType.Assembly.GetName().Name);
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
