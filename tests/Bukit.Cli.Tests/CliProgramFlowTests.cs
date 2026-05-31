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
    public void Specs_IncludePlugin_AndTheme()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var plugin = registry.Resolve("plugin");
        Assert.NotNull(plugin);
        Assert.NotEmpty(plugin!.Subcommands!);

        var theme = registry.Resolve("theme");
        Assert.NotNull(theme);
        Assert.NotEmpty(theme!.Subcommands!);
    }

    [Fact]
    public void Specs_IncludeImportSeed_AndNotionPush()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        var import = registry.Resolve("import");
        Assert.NotNull(import);
        Assert.Contains(import!.Subcommands!, s => s.Name == "seed");

        var notion = registry.Resolve("notion");
        Assert.NotNull(notion);
        Assert.Contains(notion!.Subcommands!, s => s.Name == "push");
    }
}
