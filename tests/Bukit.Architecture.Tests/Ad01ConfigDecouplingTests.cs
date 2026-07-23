using System.Reflection;
using System.Xml.Linq;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class Ad01ConfigDecouplingTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void EngineAbstractions_ProjectAndAssembly_DoNotReferenceConfig()
    {
        var projectPath = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Engine.Abstractions",
            "Bukit.Engine.Abstractions.csproj");
        var projectReferences = XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(
                element.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .ToArray();

        Assert.DoesNotContain("Bukit.Config", projectReferences, StringComparer.OrdinalIgnoreCase);

        var assemblyReferences = typeof(BuildContext).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();
        Assert.DoesNotContain("Bukit.Config", assemblyReferences, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildContext_DoesNotExposeConcreteConfig()
    {
        Assert.Null(typeof(BuildContext).GetProperty(
            "Config",
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void EngineAndCli_RetainTheirExplicitConfigDependencies()
    {
        Assert.Contains(
            "Bukit.Config",
            typeof(SiteEngine).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
            StringComparer.OrdinalIgnoreCase);
        Assert.Contains(
            "Bukit.Config",
            typeof(Bukit.Cli.BukitCliSpecs).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SiteEngine_ListRouteApi_UsesOnlyExplicitInputs()
    {
        var methods = typeof(SiteEngine)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, nameof(SiteEngine.GetListRoutes), StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(
            methods,
            method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(BuildContext)));

        Assert.Contains(methods, method =>
        {
            var parameters = method.GetParameters();
            return parameters.Length == 4 &&
                parameters[0].ParameterType == typeof(IReadOnlyList<RoutedContentDocument>) &&
                parameters[1].ParameterType == typeof(IReadOnlyDictionary<string, CollectionConfig>) &&
                parameters[2].ParameterType == typeof(string) &&
                parameters[3].ParameterType == typeof(ThemeTemplateResolver);
        });
    }

    [Fact]
    public void StaticPluginRegistration_RemainsReflectionFree()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Engine",
            "Plugins",
            "PluginRegistry.cs"));

        Assert.Contains(
            "new BuiltInPluginSource(config, analyticsBuildState)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Activator.CreateInstance", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetExportedTypes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTypes()", source, StringComparison.Ordinal);
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
