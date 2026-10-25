using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad03C4ContentNotionBridgeInternalizationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] RemovedBridgeTypeNames =
    [
        "Bukit.Content.Notion.NotionApiClient",
        "Bukit.Content.Notion.NotionContentProvider",
        "Bukit.Content.Notion.NotionProviderOptions",
        "Bukit.Content.Notion.NotionCompatibilityQueries",
        "Bukit.Content.Notion.NotionCompatibilityPage"
    ];

    [Fact]
    public void LegacyContentNotionBridgeCluster_IsRemovedWithConsolidatedDependencyBoundary()
    {
        Assembly contentAssembly =
            typeof(Bukit.Content.IContentProvider).Assembly;
        string[] exportedTypeNames = contentAssembly.GetExportedTypes()
            .Select(type => type.FullName!)
            .ToArray();

        Assert.Equal("Bukit.Content", contentAssembly.GetName().Name);
        Assert.All(RemovedBridgeTypeNames, typeName =>
        {
            Type? type = contentAssembly.GetType(
                typeName,
                throwOnError: false,
                ignoreCase: false);

            Assert.Null(type);
            Assert.DoesNotContain(typeName, exportedTypeNames);
        });

        XDocument contentProject = ReadProject("Bukit.Content");
        string[] contentReferences = GetProjectReferences(contentProject);
        Assert.DoesNotContain("Bukit.Content.Notion", contentReferences);
        Assert.Contains("Bukit.Notion", contentReferences);

        XDocument engineProject = ReadProject("Bukit.Engine");
        Assert.DoesNotContain(
            "Bukit.Content.Notion",
            GetProjectReferences(engineProject));

        Assert.False(File.Exists(Path.Combine(
            RepoRoot, "src/Bukit-Core/Bukit.Content.Notion/Bukit.Content.Notion.csproj")));
        Assert.Same(contentAssembly, typeof(Bukit.Content.Notion.NotionContentSource).Assembly);

        Assert.Equal(
            ContentBoundaryExpectations.FriendAssemblies,
            GetFriendAssemblies(contentAssembly));
    }

    private static XDocument ReadProject(string projectName)
        => XDocument.Load(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            projectName,
            $"{projectName}.csproj"));

    private static string[] GetProjectReferences(XDocument project)
        => project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")?.Value.Replace(
                    '\\',
                    Path.DirectorySeparatorChar)) ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] GetFriendAssemblies(Assembly assembly)
        => assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .Order(StringComparer.Ordinal)
            .ToArray();

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

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
