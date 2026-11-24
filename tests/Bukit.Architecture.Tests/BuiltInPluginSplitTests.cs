using System.Reflection;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class BuiltInPluginSplitTests
{
    private static readonly Assembly Engine = typeof(SiteEngine).Assembly;

    [Theory]
    [InlineData("DataFileLimits")]
    [InlineData("DataDirectoryWalker")]
    [InlineData("DataFileReaders")]
    [InlineData("DocumentTraversalState")]
    [InlineData("ProjectionBudgetState")]
    [InlineData("DataJsonProjector")]
    [InlineData("DataYamlProjector")]
    [InlineData("ImageVariantGenerator")]
    [InlineData("ImageVariantFreshness")]
    [InlineData("ImageVariantOwnership")]
    [InlineData("ImageVariantPlan")]
    [InlineData("ResponsiveImageHtmlTransform")]
    public void Responsibility_IsTopLevelInternalAndNotAPlugin(string name)
    {
        var type = Helper(name);
        Assert.True(type.IsNotPublic);
        Assert.False(type.IsNested);
        Assert.DoesNotContain(type, Engine.GetExportedTypes());
        Assert.False(typeof(IBukitPlugin).IsAssignableFrom(type));
    }

    [Fact]
    public void SupportingTypes_RemainInternalImplementationDetails()
    {
        foreach (var name in new[] { "DataTraversalState", "VerifiedDataFileStream", "BudgetedReadStream",
                     "PrefixReadStream", "StrictBoundedTextReader", "VariantFreshness", "VariantFreshnessJsonContext" })
        {
            var type = Helper(name);
            Assert.True(type.IsNotPublic);
            Assert.False(type.IsNested);
            Assert.False(typeof(IBukitPlugin).IsAssignableFrom(type));
        }
    }

    [Theory]
    [InlineData("DataFilesPlugin", "data-files", "IBukitPlugin", "IDerivePagesPlugin", "IDerivePagesAsyncPlugin")]
    [InlineData("ImageProcessingPlugin", "image-processing", "IBukitPlugin", "IAfterBuildAsyncPlugin", "IHtmlTransformPlugin")]
    public void Entries_KeepIdentityAndInterfaces(string typeName, string name, params string[] interfaces)
    {
        var type = Helper(typeName);
        Assert.True(type.IsNotPublic);
        Assert.True(type.IsSealed);
        Assert.Equal(interfaces.Order(StringComparer.Ordinal), type.GetInterfaces().Select(i => i.Name).Order(StringComparer.Ordinal));
        var constructor = Assert.Single(type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        var arguments = constructor.GetParameters().Select(parameter => parameter.DefaultValue).ToArray();
        arguments[0] = new AppConfig { Site = new SiteConfig { Name = "test", Title = "Test" }, Content = new ContentConfig() };
        var plugin = Assert.IsAssignableFrom<IBukitPlugin>(constructor.Invoke(arguments));
        Assert.Equal(name, plugin.Name);
        Assert.Equal("1.0.0", plugin.Version);
    }

    [Fact]
    public void DataFileSafetyAndTraversalSeams_AreOwnedByHelpers()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Assert.NotNull(Helper("DataFileReaders").GetMethod("OpenVerifiedDataFile", flags));
        var walker = Helper("DataDirectoryWalker");
        foreach (var name in new[] { "TakeBoundedSorted", "TakeBoundedSortedWithinEntryBudget" })
        {
            Assert.NotNull(walker.GetMethod(name, flags));
            Assert.Null(Helper("DataFilesPlugin").GetMethod(name, flags));
        }
        Assert.Null(Helper("DataFilesPlugin").GetMethod("OpenVerifiedDataFile", flags));
        Assert.NotNull(Helper("DataFilesPlugin").GetField("_openDataFile", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [Theory]
    [InlineData("ImageProcessingPlugin.cs")]
    [InlineData("ImageProcessing/ImageVariantFreshness.cs")]
    [InlineData("ImageProcessing/ImageVariantGenerator.cs")]
    [InlineData("ImageProcessing/ImageVariantOwnership.cs")]
    [InlineData("ImageProcessing/ImageVariantPlan.cs")]
    [InlineData("ImageProcessing/ResponsiveImageHtmlTransform.cs")]
    [InlineData("../../../Bukit.Engine.Assets/ImageOptimizer.cs")]
    public void ImageProcessing_NeverStartsAnExternalProcess(string relativePath)
    {
        // Resizing and asset optimization are SkiaSharp-only; no external image tool may be probed or run.
        var identifiers = Source(relativePath).DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(identifier => identifier.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("ExternalToolProcessRunner", identifiers);
        Assert.DoesNotContain("ProcessStartInfo", identifiers);
        Assert.DoesNotContain("Process", identifiers);
        Assert.Null(Helper("ImageVariantGenerator").GetMethod(
            "FindResizeToolAsync", BindingFlags.Static | BindingFlags.NonPublic));
    }

    private static Type Helper(string name)
    {
        foreach (var candidate in new[]
                 {
                     "Bukit.Engine.Plugins.BuiltIn." + name,
                     "Bukit.Engine.Plugins.BuiltIn.DataFiles." + name,
                     "Bukit.Engine.Plugins.BuiltIn.ImageProcessing." + name
                 })
        {
            if (Engine.GetType(candidate) is { } type)
            {
                return type;
            }
        }

        throw new InvalidOperationException("Missing built-in plugin helper type: " + name);
    }

    private static Microsoft.CodeAnalysis.SyntaxNode Source(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn", relativePath);
        Assert.True(File.Exists(path), "Missing extracted source: " + relativePath);
        return CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
    }
}
