using Bukit.Cli.Cli.Metadata;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliProgramFlowTests
{
    [Fact]
    public void Specs_IncludeBuild_AndPreview()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        Assert.NotNull(registry.Resolve("build"));
        Assert.NotNull(registry.Resolve("preview"));
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
    public void Specs_ExcludeImportHtmlDemo()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        Assert.Null(registry.Resolve("import"));
    }
}
