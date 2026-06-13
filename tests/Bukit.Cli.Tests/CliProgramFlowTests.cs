using Bukit.Cli.Shared.Cli.Metadata;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliProgramFlowTests
{
    [Fact]
    public void Specs_IncludeBuild_Preview_AndDev()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        Assert.NotNull(registry.Resolve("build"));
        Assert.NotNull(registry.Resolve("preview"));
        Assert.NotNull(registry.Resolve("dev"));
    }

    [Fact]
    public void Specs_IncludeCoreQualityGateCommands()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        Assert.NotNull(registry.Resolve("seo"));
        Assert.NotNull(registry.Resolve("geo"));
        Assert.NotNull(registry.Resolve("publish"));
        Assert.NotNull(registry.Resolve("deploy"));
    }

    [Fact]
    public void Descriptors_DispatchCoreQualityGateCommands()
    {
        var descriptors = BukitCliDescriptors.CreateDescriptors();

        Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(descriptors, "seo"));
        Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(descriptors, "geo"));
        Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(descriptors, "publish"));
        Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(descriptors, "deploy"));
        Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(descriptors, "dev"));
    }

    [Fact]
    public void Specs_ExcludePlugin_AndTheme()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        Assert.Null(registry.Resolve("plugin"));
        Assert.Null(registry.Resolve("theme"));
    }

    [Fact]
    public void Specs_ExcludeImport_AndNotion()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        Assert.Null(registry.Resolve("import"));
        Assert.Null(registry.Resolve("notion"));
    }

    [Fact]
    public void Specs_ExcludeExperimentalCommands()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        foreach (var name in new[] { "clone", "import", "notion", "plugin", "intent", "visual", "webhook", "data", "theme", "docs", "route" })
        {
            Assert.Null(registry.Resolve(name));
        }
    }

    [Fact]
    public void Specs_ExcludeImportHtmlDemo()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        Assert.Null(registry.Resolve("import"));
    }
}
