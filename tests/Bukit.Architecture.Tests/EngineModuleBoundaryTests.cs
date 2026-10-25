using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Bukit.Architecture.Tests;

/// <summary>
/// Guards the assemblies split out of Bukit.Engine. Engine orchestrates and may
/// reference every module; the modules sit below it and never reference Engine
/// or each other except where listed here.
/// </summary>
public sealed class EngineModuleBoundaryTests
{
    private const string Engine = "Bukit.Engine";
    private const string Primitives = "Bukit.Engine.Primitives";
    private const string Feeds = "Bukit.Engine.Feeds";
    private const string Analytics = "Bukit.Engine.Analytics";
    private const string Assets = "Bukit.Engine.Assets";
    private const string Audit = "Bukit.Engine.Audit";

    private static readonly string[] Modules = [Primitives, Feeds, Analytics, Assets, Audit];

    // Which other Engine modules each module may reference. Engine itself is never allowed.
    private static readonly Dictionary<string, string[]> AllowedModuleReferences = new(StringComparer.Ordinal)
    {
        [Primitives] = [],
        [Feeds] = [Primitives],
        [Analytics] = [Primitives],
        [Assets] = [Primitives],
        [Audit] = [Primitives, Feeds]
    };

    // Exact InternalsVisibleTo friends of Engine and each module.
    private static readonly Dictionary<string, string[]> ExpectedFriends = new(StringComparer.Ordinal)
    {
        [Engine] = ["Bukit.Engine.Tests", "bukit"],
        [Primitives] = [Engine, Analytics, Assets, Audit, Feeds, "Bukit.Engine.Tests", "bukit"],
        [Feeds] = [Engine, Audit, "Bukit.Engine.Tests"],
        [Analytics] = [Engine, "Bukit.Engine.Tests", "bukit"],
        [Assets] = [Engine, "Bukit.Engine.Tests"],
        [Audit] = [Engine, "Bukit.Engine.Tests", "bukit"]
    };

    // The only public types each module may export.
    private static readonly Dictionary<string, string[]> ExpectedExports = new(StringComparer.Ordinal)
    {
        [Primitives] = ["Bukit.Engine.BuildPathUtils"],
        [Feeds] = ["Bukit.Engine.RssGenerator", "Bukit.Engine.RssGenerator+Post"],
        [Analytics] = [],
        [Assets] = [],
        [Audit] = []
    };

    private static readonly string RepoRoot = FindRepoRoot();

    public static TheoryData<string> ModuleNames()
    {
        var data = new TheoryData<string>();
        foreach (string module in Modules)
        {
            data.Add(module);
        }

        return data;
    }

    [Fact]
    public void Engine_ReferencesEveryModule()
    {
        string[] references = ReferencedAssemblyNames(EngineModuleAssemblies.Engine);

        Assert.All(Modules, module => Assert.Contains(module, references));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_DoesNotReferenceEngineOrUnlistedModules(string module)
    {
        string[] engineFamily = [Engine, .. Modules];
        string[] referencedFamily = ReferencedAssemblyNames(Load(module))
            .Where(name => engineFamily.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.DoesNotContain(Engine, referencedFamily);
        Assert.All(referencedFamily, name => Assert.Contains(name, AllowedModuleReferences[module]));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleProject_DoesNotReferenceEngineOrUnlistedModules(string module)
    {
        string projectPath = Path.Combine(RepoRoot, "src", "Bukit-Core", module, $"{module}.csproj");
        string[] projectReferences = XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(
                ((string?)element.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
            .Where(name => name.StartsWith(Engine, StringComparison.Ordinal) &&
                !string.Equals(name, "Bukit.Engine.Abstractions", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(
            AllowedModuleReferences[module].Order(StringComparer.Ordinal),
            projectReferences.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(Engine)]
    [InlineData(Primitives)]
    [InlineData(Feeds)]
    [InlineData(Analytics)]
    [InlineData(Assets)]
    [InlineData(Audit)]
    public void InternalsVisibleTo_MatchesTheApprovedFriendList(string assemblyName)
    {
        string[] friends = Load(assemblyName)
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0].Trim())
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedFriends[assemblyName].Order(StringComparer.Ordinal), friends);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_ExportsOnlyApprovedPublicTypes(string module)
    {
        string[] exported = Load(module)
            .GetExportedTypes()
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedExports[module].Order(StringComparer.Ordinal), exported);
    }

    [Fact]
    public void Engine_ForwardsTheMovedPublicTypes()
    {
        string[] forwarded = EngineModuleAssemblies.Engine
            .GetForwardedTypes()
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Nested public types are forwarded together with their parent.
        Assert.Equal(
            ["Bukit.Engine.BuildPathUtils", "Bukit.Engine.RssGenerator", "Bukit.Engine.RssGenerator+Post"],
            forwarded);
    }

    private static Assembly Load(string name) =>
        EngineModuleAssemblies.All.Single(assembly => assembly.GetName().Name == name);

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();

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
