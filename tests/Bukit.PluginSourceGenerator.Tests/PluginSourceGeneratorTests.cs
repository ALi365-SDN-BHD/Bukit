using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Bukit.PluginSourceGenerator.Tests;

public sealed class PluginSourceGeneratorTests
{
    private const string Stubs = @"
using System;
using System.Collections.Generic;

namespace Bukit.Engine.Plugins
{
    public interface IBukitPlugin
    {
        string Name { get; }
        string Version { get; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class BukitPluginAttribute : Attribute
    {
    }
}

namespace Bukit.Engine.Plugins.Generated
{
    public interface IPluginSource
    {
        IEnumerable<IBukitPlugin> GetPlugins();
    }
}
";

    private static (string? GeneratedSource, string[] Diagnostics) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new PluginSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSource = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("GeneratedPluginSource.g.cs"))
            ?.ToString();

        return (generatedSource, diagnostics.Select(d => d.GetMessage()).ToArray());
    }

    [Fact]
    public void No_plugin_classes_should_generate_empty_GetPlugins()
    {
        var (generated, diags) = RunGenerator(Stubs);

        Assert.NotNull(generated);
        Assert.Contains("yield break;", generated);
        Assert.DoesNotContain("yield return new", generated);
    }

    [Fact]
    public void Valid_plugin_with_attribute_in_correct_namespace_should_be_included()
    {
        var source = Stubs + @"
namespace Bukit.Plugins.MyPlugin
{
    [Bukit.Engine.Plugins.BukitPlugin]
    public class MyPlugin : Bukit.Engine.Plugins.IBukitPlugin
    {
        public string Name => ""MyPlugin"";
        public string Version => ""1.0.0"";
    }
}
";

        var (generated, diags) = RunGenerator(source);

        Assert.NotNull(generated);
        Assert.Contains("yield return new Bukit.Plugins.MyPlugin.MyPlugin()", generated);
    }

    [Fact]
    public void Plugin_without_attribute_should_not_be_included()
    {
        var source = Stubs + @"
namespace Bukit.Plugins.OrphanPlugin
{
    public class OrphanPlugin : Bukit.Engine.Plugins.IBukitPlugin
    {
        public string Name => ""Orphan"";
        public string Version => ""0.0.0"";
    }
}
";

        var (generated, diags) = RunGenerator(source);

        Assert.NotNull(generated);
        Assert.DoesNotContain("yield return new", generated);
    }

    [Fact]
    public void Plugin_in_wrong_namespace_should_not_be_included()
    {
        var source = Stubs + @"
namespace MyApp.Plugins
{
    [Bukit.Engine.Plugins.BukitPlugin]
    public class OtherPlugin : Bukit.Engine.Plugins.IBukitPlugin
    {
        public string Name => ""Other"";
        public string Version => ""0.0.0"";
    }
}
";

        var (generated, diags) = RunGenerator(source);

        Assert.NotNull(generated);
        Assert.DoesNotContain("yield return new", generated);
    }

    [Fact]
    public void Abstract_plugin_should_not_be_included()
    {
        var source = Stubs + @"
namespace Bukit.Plugins.Abstracts
{
    [Bukit.Engine.Plugins.BukitPlugin]
    public abstract class AbstractPlugin : Bukit.Engine.Plugins.IBukitPlugin
    {
        public string Name => ""Abstract"";
        public string Version => ""0.0.0"";
    }
}
";

        var (generated, diags) = RunGenerator(source);

        Assert.NotNull(generated);
        Assert.DoesNotContain("yield return new", generated);
    }

    [Fact]
    public void Class_not_implementing_IBukitPlugin_should_not_be_included()
    {
        var source = Stubs + @"
namespace Bukit.Plugins.Random
{
    [Bukit.Engine.Plugins.BukitPlugin]
    public class RandomClass
    {
    }
}
";

        var (generated, diags) = RunGenerator(source);

        Assert.NotNull(generated);
        Assert.DoesNotContain("yield return new", generated);
    }

    [Fact]
    public void Multiple_valid_plugins_should_all_be_included()
    {
        var source = Stubs + @"
namespace Bukit.Plugins.Alpha
{
    [Bukit.Engine.Plugins.BukitPlugin]
    public class AlphaPlugin : Bukit.Engine.Plugins.IBukitPlugin
    {
        public string Name => ""Alpha"";
        public string Version => ""1.0.0"";
    }
}

namespace Bukit.Plugins.Beta
{
    [Bukit.Engine.Plugins.BukitPlugin]
    public class BetaPlugin : Bukit.Engine.Plugins.IBukitPlugin
    {
        public string Name => ""Beta"";
        public string Version => ""2.0.0"";
    }
}
";

        var (generated, diags) = RunGenerator(source);

        Assert.NotNull(generated);
        Assert.Contains("yield return new Bukit.Plugins.Alpha.AlphaPlugin()", generated);
        Assert.Contains("yield return new Bukit.Plugins.Beta.BetaPlugin()", generated);
    }
}
