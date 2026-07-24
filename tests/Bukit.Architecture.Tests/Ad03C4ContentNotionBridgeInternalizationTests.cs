using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad03C4ContentNotionBridgeInternalizationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] InternalBridgeTypeNames =
    [
        "Bukit.Content.Notion.NotionApiClient",
        "Bukit.Content.Notion.NotionContentProvider",
        "Bukit.Content.Notion.NotionProviderOptions"
    ];

    [Fact]
    public void LegacyContentNotionBridgeCluster_IsInternalWithExistingDependencyBoundary()
    {
        Assembly contentAssembly =
            typeof(Bukit.Content.Notion.NotionPropertyParser).Assembly;
        string[] exportedTypeNames = contentAssembly.GetExportedTypes()
            .Select(type => type.FullName!)
            .ToArray();

        Assert.Equal("Bukit.Content", contentAssembly.GetName().Name);
        Assert.All(InternalBridgeTypeNames, typeName =>
        {
            Type type = contentAssembly.GetType(
                typeName,
                throwOnError: true,
                ignoreCase: false)!;

            Assert.False(type.IsPublic, $"Legacy bridge remains public: {typeName}");
            Assert.DoesNotContain(typeName, exportedTypeNames);
        });

        XDocument contentProject = ReadProject("Bukit.Content");
        string[] contentReferences = GetProjectReferences(contentProject);
        Assert.Contains("Bukit.Content.Notion", contentReferences);
        Assert.Contains("Bukit.Notion", contentReferences);

        XDocument engineProject = ReadProject("Bukit.Engine");
        Assert.DoesNotContain(
            "Bukit.Content.Notion",
            GetProjectReferences(engineProject));

        XDocument adapterProject = ReadProject("Bukit.Content.Notion");
        string[] adapterFriends = GetProjectFriends(adapterProject);
        Assert.DoesNotContain("Bukit.Engine", adapterFriends);
        Assert.DoesNotContain("Bukit.Engine.Tests", adapterFriends);

        Assert.Equal(
            [
                "Bukit.Content.Tests",
                "Bukit.Engine",
                "Bukit.Engine.Tests"
            ],
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

    private static string[] GetProjectFriends(XDocument project)
        => project.Descendants("InternalsVisibleTo")
            .Select(friend => friend.Attribute("Include")?.Value ?? string.Empty)
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
