using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class CoreProjectReferenceBoundaryTests
{
    [Fact]
    public void CoreProjects_MustNotReferencePluginsOrLabs()
    {
        string repoRoot = FindRepoRoot();
        string coreRoot = Path.Combine(repoRoot, "src", "Bukit-Core");
        string pluginsRoot = Path.Combine(repoRoot, "src", "Bukit-Plugins");
        string labsRoot = Path.Combine(repoRoot, "src", "Bukit-Labs");

        string[] offenders = Directory
            .EnumerateFiles(coreRoot, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(project => XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include!))))
            .Where(target => IsUnder(target, pluginsRoot) || IsUnder(target, labsRoot))
            .Select(target => Path.GetRelativePath(repoRoot, target))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
        Assert.True(IsUnder(Path.Combine(pluginsRoot, "Bukit.Clone", "Bukit.Clone.csproj"), pluginsRoot));
        Assert.False(IsUnder(Path.Combine(coreRoot, "Bukit.Shared", "Bukit.Shared.csproj"), pluginsRoot));
    }

    private static bool IsUnder(string path, string root)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }

    private static string FindRepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "bukit-core.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
