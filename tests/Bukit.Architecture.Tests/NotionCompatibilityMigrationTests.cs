using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class NotionCompatibilityMigrationTests
{
    [Fact]
    public void CanonicalSourceAndOptions_BelongToContentWithTheExistingProviderContracts()
    {
        var content = typeof(Bukit.Content.IContentProvider).Assembly;
        var source = content.GetType("Bukit.Content.Notion.NotionContentSource");
        var options = content.GetType("Bukit.Content.Notion.NotionContentSourceOptions");

        Assert.NotNull(source);
        Assert.NotNull(options);
        Assert.True(source.IsPublic);
        Assert.True(options.IsPublic);
        Assert.Same(content, source.Assembly);
        Assert.Same(content, options.Assembly);
        Assert.True(typeof(Bukit.Content.IContentProvider).IsAssignableFrom(source));
        var fallback = Assert.Single(source.GetInterfaces(),
            type => type.FullName == "Bukit.Content.Notion.INotionRelationFallbackResolverProvider");
        Assert.True(fallback.IsNotPublic);
        Assert.Null(source.GetProperty("RelationFallbackResolver", BindingFlags.Public | BindingFlags.Instance));
        Assert.All(source.GetInterfaceMap(fallback).TargetMethods, method => Assert.True(method.IsPrivate));
    }

    [Fact]
    public void LegacyShells_AreAbsentRatherThanForwarded()
    {
        var content = typeof(Bukit.Content.IContentProvider).Assembly;
        string[] names =
        [
            "NotionApiClient", "NotionCompatibilityQueries", "NotionCompatibilityPage",
            "NotionContentProvider", "NotionPropertyParser", "NotionProviderOptions"
        ];
        Assert.All(names, name => Assert.Null(content.GetType($"Bukit.Content.Notion.{name}")));
        Assert.DoesNotContain(content.GetForwardedTypes(),
            type => type.Namespace == "Bukit.Content.Notion");
        Assert.DoesNotContain(content.GetReferencedAssemblies(),
            assembly => assembly.Name == "Bukit.Content.Notion");
    }

    [Fact]
    public void RemovedProject_HasNoSolutionOrDirectProjectReferences()
    {
        var root = FindRepoRoot();
        Assert.False(File.Exists(Path.Combine(root, "src/Bukit-Core/Bukit.Content.Notion/Bukit.Content.Notion.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "src/Bukit-Core/Bukit.Content/Notion/NotionContentSource.cs")));
        Assert.DoesNotContain("Bukit.Content.Notion.csproj", File.ReadAllText(Path.Combine(root, "bukit-core.slnx")), StringComparison.Ordinal);
        string[] projects =
        [
            "src/Bukit-Core/Bukit.Content/Bukit.Content.csproj",
            "tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj",
            "tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj"
        ];
        Assert.All(projects, path => Assert.DoesNotContain(
            XDocument.Load(Path.Combine(root, path)).Descendants("ProjectReference"),
            reference => (reference.Attribute("Include")?.Value ?? string.Empty)
                .EndsWith("Bukit.Content.Notion.csproj", StringComparison.Ordinal)));
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
        throw new DirectoryNotFoundException("Could not locate the Bukit repository root.");
    }
}
