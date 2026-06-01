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

    [Fact]
    public void Specs_ImportHtmlDemo_IncludeDocumentedPositiveFlags()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var import = registry.Resolve("import");
        var htmlDemo = import!.Subcommands!.Single(s => s.Name == "html-demo");
        var options = htmlDemo.Options!.Select(o => o.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("--extract-content", options);
        Assert.Contains("--generate-seed", options);
        Assert.Contains("--preserve-html", options);
        Assert.Contains("--report", options);
    }
}
