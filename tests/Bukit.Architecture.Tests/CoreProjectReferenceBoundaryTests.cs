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

        string[] projects = Directory
            .EnumerateFiles(coreRoot, "*.csproj", SearchOption.AllDirectories)
            .ToArray();
        // Plan §2 fixes the Core source-project count at 18; a count change must be reviewed with this guard.
        Assert.Equal(18, projects.Length);

        string[] offenders = projects
            .SelectMany(project => XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => ResolveForbiddenTarget(project, include!, pluginsRoot, labsRoot))
                .Where(target => target is not null)
                .Select(target => Path.GetRelativePath(repoRoot, target!)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData(@"..\..\Bukit-Plugins\Bukit.Clone\Bukit.Clone.csproj")]
    [InlineData("../../Bukit-Plugins/Bukit.Clone/Bukit.Clone.csproj")]
    [InlineData(@"..\..\Bukit-Labs\Bukit.Labs.Cli\Bukit.Labs.Cli.csproj")]
    [InlineData("../../Bukit-Labs/Bukit.Labs.Cli/Bukit.Labs.Cli.csproj")]
    public void ForbiddenReferences_AreDetectedInBothSeparatorStyles(string include)
    {
        string repoRoot = FindRepoRoot();
        string project = Path.Combine(
            repoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Content",
            "Bukit.Content.csproj");

        Assert.NotNull(ResolveForbiddenTarget(
            project,
            include,
            Path.Combine(repoRoot, "src", "Bukit-Plugins"),
            Path.Combine(repoRoot, "src", "Bukit-Labs")));
    }

    private static string? ResolveForbiddenTarget(
        string projectPath,
        string include,
        string pluginsRoot,
        string labsRoot)
    {
        string normalized = include.Replace('\\', Path.DirectorySeparatorChar);
        string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, normalized));
        return IsUnder(target, pluginsRoot) || IsUnder(target, labsRoot) ? target : null;
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
