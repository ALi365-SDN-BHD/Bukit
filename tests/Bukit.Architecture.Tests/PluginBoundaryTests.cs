using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class PluginBoundaryTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] PluginHostForbiddenProjectReferences =
    [
        "Bukit.Cli",
        "Bukit.Cli.Shared",
        "Bukit.Config",
        "Bukit.Content",
        "Bukit.Engine",
        "Bukit.Engine.Abstractions",
        "Bukit.Rendering",
        "Bukit.Routing",
        "Bukit.Labs.Cli",
        "Bukit.Clone",
        "Bukit.Importing",
        "Bukit.IndexNow",
        "Bukit.WechatSyncing",
        "Bukit.Plugin.Echo",
        "Bukit.Plugin.Import",
        "Bukit.Plugin.IndexNow",
        "Bukit.Plugin.WechatSync",
        "WordCountSectionPlugin"
    ];

    private static readonly string[] OfficialPluginForbiddenProjectReferences =
    [
        "Bukit.Cli",
        "Bukit.Cli.Shared",
        "Bukit.Config",
        "Bukit.Content",
        "Bukit.Engine",
        "Bukit.Engine.Abstractions",
        "Bukit.PluginHost",
        "Bukit.Rendering",
        "Bukit.Routing",
        "Bukit.Labs.Cli",
        "WordCountSectionPlugin"
    ];

    private static readonly string[] PluginDomainForbiddenProjectReferences =
    [
        "Bukit.Cli",
        "Bukit.Cli.Shared",
        "Bukit.Config",
        "Bukit.Content",
        "Bukit.Engine",
        "Bukit.Engine.Abstractions",
        "Bukit.Plugin.Abstractions",
        "Bukit.PluginHost",
        "Bukit.Plugin.Echo",
        "Bukit.Plugin.Import",
        "Bukit.Plugin.IndexNow",
        "Bukit.Plugin.WechatSync",
        "Bukit.Rendering",
        "Bukit.Routing",
        "Bukit.Labs.Cli",
        "WordCountSectionPlugin"
    ];

    private static readonly string[] ImportingWorkflowAllowedProjectReferences =
    [
        "Bukit.Cli.Shared",
        "Bukit.Config",
        "Bukit.Engine",
        "Bukit.Engine.Abstractions",
        "Bukit.Notion",
        "Bukit.Shared"
    ];

    private static readonly string[] ImportingWorkflowForbiddenProjectReferences =
    [
        "Bukit.Cli",
        "Bukit.Clone",
        "Bukit.Content",
        "Bukit.Plugin.Abstractions",
        "Bukit.PluginHost",
        "Bukit.Plugin.Echo",
        "Bukit.Plugin.Import",
        "Bukit.Rendering",
        "Bukit.Routing",
        "Bukit.Labs.Cli",
        "WordCountSectionPlugin"
    ];

    private static readonly string[] PluginHostForbiddenAssemblyReferences =
    [
        "Bukit.Cli",
        "Bukit.Cli.Shared",
        "Bukit.Config",
        "Bukit.Content",
        "Bukit.Engine",
        "Bukit.Engine.Abstractions",
        "Bukit.Rendering",
        "Bukit.Routing",
        "bukit-labs",
        "Bukit.Clone",
        "Bukit.Importing",
        "Bukit.IndexNow",
        "bukit-plugin-echo",
        "bukit-plugin-import",
        "bukit-plugin-indexnow",
        "bukit-plugin-wechat-sync",
        "WordCountSectionPlugin"
    ];

    private static readonly string[] OfficialPluginForbiddenAssemblyReferences =
    [
        "Bukit.Cli",
        "Bukit.Cli.Shared",
        "Bukit.Config",
        "Bukit.Content",
        "Bukit.Engine",
        "Bukit.Engine.Abstractions",
        "Bukit.PluginHost",
        "Bukit.Rendering",
        "Bukit.Routing",
        "bukit-labs",
        "WordCountSectionPlugin"
    ];

    private static readonly string[] PluginDomainForbiddenAssemblyReferences =
    [
        "Bukit.Cli",
        "Bukit.Cli.Shared",
        "Bukit.Config",
        "Bukit.Content",
        "Bukit.Engine",
        "Bukit.Engine.Abstractions",
        "Bukit.Plugin.Abstractions",
        "Bukit.PluginHost",
        "bukit-plugin-echo",
        "bukit-plugin-import",
        "bukit-plugin-indexnow",
        "bukit-plugin-wechat-sync",
        "Bukit.Rendering",
        "Bukit.Routing",
        "bukit-labs",
        "WordCountSectionPlugin"
    ];

    private static readonly string[] ImportingWorkflowForbiddenAssemblyReferences =
    [
        "Bukit.Cli",
        "Bukit.Clone",
        "Bukit.Content",
        "Bukit.Plugin.Abstractions",
        "Bukit.PluginHost",
        "bukit-plugin-echo",
        "bukit-plugin-import",
        "Bukit.Rendering",
        "Bukit.Routing",
        "bukit-labs",
        "WordCountSectionPlugin"
    ];

    [Fact]
    public void PluginHostProject_OnlyReferencesProtocolAbstractionsAndShared()
    {
        var references = ReadProjectReferenceNames("src/Bukit-Core/Bukit.PluginHost/Bukit.PluginHost.csproj");

        Assert.Equal(["Bukit.Plugin.Abstractions", "Bukit.Shared"], references);
        AssertDoesNotContainAny(references, PluginHostForbiddenProjectReferences, "Bukit.PluginHost project reference");
    }

    [Fact]
    public void PluginAbstractionsProject_DoesNotReferenceBukitRuntimeProjects()
    {
        var references = ReadProjectReferenceNames("src/Bukit-Core/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj");
        var bukitReferences = references
            .Where(reference => reference.StartsWith("Bukit.", StringComparison.Ordinal) || reference == "WordCountSectionPlugin")
            .ToArray();

        Assert.Empty(bukitReferences);
    }

    [Fact]
    public void OfficialProcessPluginProjects_DoNotReferenceHostRuntimeLabsOrLegacyPlugins()
    {
        var pluginProjects = new[]
        {
            "src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj",
            "src/Bukit-Plugins/Bukit.Plugin.Import/Bukit.Plugin.Import.csproj",
            "src/Bukit-Plugins/Bukit.Plugin.IndexNow/Bukit.Plugin.IndexNow.csproj",
            "src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj"
        };

        foreach (string project in pluginProjects)
        {
            var references = ReadProjectReferenceNames(project);

            Assert.Contains("Bukit.Plugin.Abstractions", references);
            AssertDoesNotContainAny(references, OfficialPluginForbiddenProjectReferences, project);
        }
    }

    [Fact]
    public void PluginDomainProjects_DoNotReferenceHostRuntimeLabsOrOfficialPluginImplementations()
    {
        var domainProjects = new[]
        {
            "src/Bukit-Plugins/Bukit.Clone/Bukit.Clone.csproj",
            "src/Bukit-Plugins/Bukit.IndexNow/Bukit.IndexNow.csproj",
            "src/Bukit-Plugins/Bukit.WechatSyncing/Bukit.WechatSyncing.csproj"
        };

        foreach (string project in domainProjects)
        {
            var references = ReadProjectReferenceNames(project);

            AssertDoesNotContainAny(references, PluginDomainForbiddenProjectReferences, project);
        }
    }

    [Fact]
    public void ImportingWorkflowProject_MayReferenceCoreWorkflowButNotHostLabsOrPluginImplementations()
    {
        var references = ReadProjectReferenceNames("src/Bukit-Plugins/Bukit.Importing/Bukit.Importing.csproj");

        Assert.Equal(ImportingWorkflowAllowedProjectReferences, references);
        AssertDoesNotContainAny(references, ImportingWorkflowForbiddenProjectReferences, "Bukit.Importing project reference");
    }

    [Fact]
    public void PluginHostAssembly_DoesNotDependOnCoreRuntimeLabsOrPluginImplementations()
        => AssertAssemblyDoesNotReferenceAny(
            typeof(Bukit.PluginHost.PluginConfigLoader).Assembly,
            PluginHostForbiddenAssemblyReferences);

    [Fact]
    public void PluginAbstractionsAssembly_DoesNotDependOnOtherBukitAssemblies()
    {
        var references = GetReferencedAssemblyNames(typeof(Bukit.Plugin.Abstractions.PluginJsonSerializerContext).Assembly)
            .Where(reference => reference.StartsWith("Bukit.", StringComparison.Ordinal) ||
                reference.StartsWith("bukit-", StringComparison.Ordinal) ||
                reference == "WordCountSectionPlugin")
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(references);
    }

    [Fact]
    public void OfficialProcessPluginAssemblies_DoNotDependOnHostRuntimeLabsOrLegacyPlugins()
    {
        var assemblies = new[]
        {
            typeof(Bukit.Plugin.Echo.EchoPluginMarker).Assembly,
            typeof(Bukit.Plugin.Import.ImportPluginApp).Assembly,
            typeof(Bukit.Plugin.IndexNow.IndexNowPluginApp).Assembly,
            typeof(Bukit.Plugin.WechatSync.WechatSyncPluginApp).Assembly
        };

        foreach (Assembly assembly in assemblies)
        {
            AssertAssemblyDoesNotReferenceAny(assembly, OfficialPluginForbiddenAssemblyReferences);
        }
    }

    [Fact]
    public void PluginDomainAssemblies_DoNotDependOnHostRuntimeLabsOrOfficialPluginImplementations()
    {
        var assemblies = new[]
        {
            typeof(Bukit.Clone.CloneDomainBlueprint).Assembly,
            typeof(Bukit.IndexNow.IndexNowSubmissionWorkflow).Assembly,
            typeof(Bukit.WechatSyncing.WechatSyncWorkflow).Assembly
        };

        foreach (Assembly assembly in assemblies)
        {
            AssertAssemblyDoesNotReferenceAny(assembly, PluginDomainForbiddenAssemblyReferences);
        }
    }

    [Fact]
    public void ImportingWorkflowAssembly_MayDependOnCoreWorkflowButNotHostLabsOrPluginImplementations()
        => AssertAssemblyDoesNotReferenceAny(
            typeof(Bukit.Importing.RouteMapConfig).Assembly,
            ImportingWorkflowForbiddenAssemblyReferences);

    [Fact]
    public void LegacySrcPluginsDirectory_DoesNotContainFormalPluginProjects()
    {
        string legacyDir = Path.Combine(RepoRoot, "src", "plugins");
        if (!Directory.Exists(legacyDir))
        {
            return;
        }

        string[] projectFiles = Directory
            .EnumerateFiles(legacyDir, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepoRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(projectFiles);
    }

    [Fact]
    public void SplitSolutions_ContainOnlyProjectsFromTheirOwnedLayer()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot, "bukit.slnx")), "Legacy bukit.slnx should not be restored.");

        const string publicApiDriftProject = "tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj";
        string[] coreProjects = ReadSolutionProjectPaths("bukit-core.slnx");
        Assert.Equal(1, coreProjects.Count(path => path == publicApiDriftProject));
        Assert.All(
            coreProjects.Where(path => path != publicApiDriftProject),
            path => Assert.StartsWith("src/Bukit-Core/", path, StringComparison.Ordinal));

        AssertSolutionProjectsStartWith("bukit-labs.slnx", "src/Bukit-Labs/");
        AssertSolutionProjectsStartWith("bukit-test.slnx", "tests/");

        string[] pluginProjects = ReadSolutionProjectPaths("bukit-plugins.slnx");
        var allowedPluginProjects = new HashSet<string>(StringComparer.Ordinal)
        {
            "src/Bukit-Plugins/Bukit.Clone/Bukit.Clone.csproj",
            "src/Bukit-Plugins/Bukit.Importing/Bukit.Importing.csproj",
            "src/Bukit-Plugins/Bukit.IndexNow/Bukit.IndexNow.csproj",
            "src/Bukit-Plugins/Bukit.Plugin.Echo/Bukit.Plugin.Echo.csproj",
            "src/Bukit-Plugins/Bukit.Plugin.Import/Bukit.Plugin.Import.csproj",
            "src/Bukit-Plugins/Bukit.Plugin.IndexNow/Bukit.Plugin.IndexNow.csproj",
            "src/Bukit-Plugins/Bukit.Plugin.WechatSync/Bukit.Plugin.WechatSync.csproj",
            "src/Bukit-Plugins/Bukit.WechatSyncing/Bukit.WechatSyncing.csproj",
            "src/Bukit-Plugins/WordCountSectionPlugin/WordCountSectionPlugin.csproj"
        };

        Assert.All(pluginProjects, path => Assert.StartsWith("src/Bukit-Plugins/", path, StringComparison.Ordinal));
        Assert.All(pluginProjects, path => Assert.Contains(path, allowedPluginProjects));
    }

    private static void AssertSolutionProjectsStartWith(string solutionPath, string expectedPrefix)
    {
        foreach (string path in ReadSolutionProjectPaths(solutionPath))
        {
            Assert.StartsWith(expectedPrefix, path, StringComparison.Ordinal);
        }
    }

    private static void AssertAssemblyDoesNotReferenceAny(Assembly assembly, IReadOnlyCollection<string> forbidden)
    {
        string[] references = GetReferencedAssemblyNames(assembly);
        AssertDoesNotContainAny(references, forbidden, $"{assembly.GetName().Name} assembly reference");
    }

    private static string[] GetReferencedAssemblyNames(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => name.Length > 0)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static string[] ReadProjectReferenceNames(string relativeProjectPath)
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, relativeProjectPath));
        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => ProjectNameFromInclude(include!))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadSolutionProjectPaths(string relativeSolutionPath)
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, relativeSolutionPath));
        return document
            .Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ProjectNameFromInclude(string include)
    {
        string normalized = include.Replace('\\', '/');
        string fileName = normalized.Split('/').Last();
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static void AssertDoesNotContainAny(
        IReadOnlyCollection<string> actual,
        IReadOnlyCollection<string> forbidden,
        string label)
    {
        string[] offenders = actual
            .Where(item => forbidden.Contains(item, StringComparer.Ordinal))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0, $"{label} contains forbidden reference(s): {string.Join(", ", offenders)}");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "bukit-core.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
