using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class NotionBoundaryTests
{
    [Fact]
    public void Notion_Project_MustExist_AndRemainBclOnly()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Notion",
            "Bukit.Notion.csproj");

        Assert.True(File.Exists(projectPath), $"Missing Notion project: {projectPath}");

        var project = XDocument.Load(projectPath);
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact]
    public void Shared_MayReferenceNotion_OnlyForOneXCompatibility()
    {
        var repoRoot = FindRepoRoot();
        var project = XDocument.Load(Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Shared",
            "Bukit.Shared.csproj"));

        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)) ?? string.Empty)
            .ToArray();

        Assert.Equal(["Bukit.Notion"], references);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Bukit repository root.");
    }
}
