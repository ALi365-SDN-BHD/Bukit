using Bukit.Engine.Abstractions.Content;
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Bukit.Architecture.Tests;

public class DependencyMatrixTests
{
    private static readonly Assembly SharedAssembly = typeof(Bukit.Shared.ILogger).Assembly;
    private static readonly Assembly ConfigAssembly = typeof(Bukit.Config.AppConfig).Assembly;
    private static readonly Assembly AbstractionsAssembly = typeof(Bukit.Engine.Abstractions.Content.ContentDocument).Assembly;
    private static readonly Assembly RoutingAssembly = typeof(Bukit.Routing.RouteGenerator).Assembly;
    private static readonly Assembly ContentAssembly = typeof(Bukit.Content.IContentProvider).Assembly;
    private static readonly Assembly RenderingAssembly = typeof(Bukit.Rendering.SiteModel).Assembly;
    private static readonly Assembly EngineAssembly = typeof(Bukit.Engine.SiteEngine).Assembly;
    private static readonly Assembly CliSharedAssembly = typeof(Bukit.Cli.Shared.ConfigPathResolver).Assembly;
    private static readonly Assembly CliAssembly = typeof(Bukit.Cli.BukitCliSpecs).Assembly;
    private static readonly Assembly LabsCliAssembly = typeof(Bukit.Labs.Cli.LabsCliAssemblyMarker).Assembly;

    // ── Layer isolation ──────────────────────────────────────────

    [Fact]
    public void Shared_MustNotDependOn_AnyOtherBukitProject()
    {
        Types.InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Bukit.Config", "Bukit.Engine.", "Bukit.Cli",
                "Bukit.Content", "Bukit.Rendering", "Bukit.Routing"])
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void CliShared_MustOnlyDependOn_Shared()
    {
        Types.InAssembly(CliSharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Bukit.Config", "Bukit.Engine.", "Bukit.Content",
                "Bukit.Rendering", "Bukit.Routing", "Bukit.Cli.Commands", "Bukit.Labs"])
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Config_MustOnlyDependOn_Shared()
    {
        Types.InAssembly(ConfigAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Bukit.Engine.", "Bukit.Cli",
                "Bukit.Content", "Bukit.Rendering", "Bukit.Routing"])
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Abstractions_MustNotDependOn_EngineAssembly()
    {
        Types.InAssembly(AbstractionsAssembly)
            .That()
            .DoNotResideInNamespaceStartingWith("Bukit.Engine.Abstractions")
            .ShouldNot()
            .HaveDependencyOn("Bukit.Engine.")
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Routing_MustNotDependOn_Engine_Cli_Rendering()
    {
        Types.InAssembly(RoutingAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Bukit.Engine.", "Bukit.Cli", "Bukit.Rendering"])
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Content_MustNotDependOn_Engine_Cli_Rendering_Routing()
    {
        Types.InAssembly(ContentAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Bukit.Engine.", "Bukit.Cli", "Bukit.Rendering", "Bukit.Routing"])
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Rendering_MustNotDependOn_Engine_Cli()
    {
        Types.InAssembly(RenderingAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Bukit.Engine.", "Bukit.Cli"])
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Engine_MustNotDependOn_Cli()
    {
        Types.InAssembly(EngineAssembly)
            .ShouldNot()
            .HaveDependencyOn("Bukit.Cli")
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Cli_MustNotDirectlyDependOn_Content_Rendering_Routing()
    {
        Types.InAssembly(CliAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Bukit.Content", "Bukit.Rendering", "Bukit.Routing"])
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void Cli_MustNotDependOn_ExperimentalImporting()
    {
        Types.InAssembly(CliAssembly)
            .ShouldNot()
            .HaveDependencyOn("Bukit.Importing")
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void CoreCli_MustNotDependOn_LabsCli()
    {
        Types.InAssembly(CliAssembly)
            .ShouldNot()
            .HaveDependencyOn("Bukit.Labs")
            .GetResult()
            .ShouldBeSuccessful();
    }

    [Fact]
    public void CliAndLabsCli_MustDependOn_CliShared()
    {
        Assert.Contains(CliSharedAssembly.GetName().Name, CliAssembly.GetReferencedAssemblies().Select(a => a.Name));
        Assert.Contains(CliSharedAssembly.GetName().Name, LabsCliAssembly.GetReferencedAssemblies().Select(a => a.Name));
    }

    [Fact]
    public void LabsCli_MustNotDependOn_CoreCli()
    {
        Assert.DoesNotContain(
            CliAssembly.GetName().Name,
            LabsCliAssembly.GetReferencedAssemblies().Select(a => a.Name));
    }

    [Fact]
    public void LabsCli_MayDependOn_Importing()
    {
        var references = LabsCliAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Bukit.Importing", references);
    }

    // ── Naming conventions ───────────────────────────────────────

    [Fact]
    public void Interfaces_MustStartWith_I()
    {
        Types.InAssembly(AbstractionsAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult()
            .ShouldBeSuccessful();
    }

    // ── InternalsVisibleTo guard ──────────────────────────────────

    [Fact]
    public void InternalsVisibleTo_MustOnlyExposeTo_TestAssemblies()
    {
        var allowedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Bukit.Engine.Tests",
            "Bukit.Engine.Abstractions.Tests",
            "Bukit.Content.Tests",
            "Bukit.Cli.Tests",
            "Bukit.Rendering.Tests",
            "Bukit.Routing.Tests",
            "Bukit.Config.Tests",
            "Bukit.Shared.Tests",
            "Bukit.Theme.Tests",
            "Bukit.Architecture.Tests",
            "Bukit.Theme.Benchmarks"
        };

        var allAssemblies = new[]
        {
            SharedAssembly, ConfigAssembly, AbstractionsAssembly,
            RoutingAssembly, ContentAssembly, RenderingAssembly,
            EngineAssembly, CliSharedAssembly, CliAssembly
        };

        foreach (var asm in allAssemblies)
        {
            var internalsVisibleTo = asm.GetCustomAttributes(
                typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute), false);

            foreach (System.Runtime.CompilerServices.InternalsVisibleToAttribute attr in internalsVisibleTo)
            {
                var target = attr.AssemblyName.Split(',')[0].Trim();
                Assert.True(
                    allowedTargets.Contains(target),
                    $"Assembly '{asm.GetName().Name}' exposes internals to '{target}', " +
                    "which is not in the allowed whitelist.");
            }
        }
    }
}

internal static class TestResultExtensions
{
    public static void ShouldBeSuccessful(this TestResult result)
    {
        Assert.True(result.IsSuccessful, result.FailingTypeNames?.Count > 0
            ? $"Architecture violation: {string.Join(", ", result.FailingTypeNames ?? [])}"
            : "Architecture test failed.");
    }
}
