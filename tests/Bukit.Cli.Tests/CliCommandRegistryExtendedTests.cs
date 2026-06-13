using Bukit.Cli.Shared.Cli.Metadata;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliCommandRegistryExtendedTests
{
    [Fact]
    public void Resolve_Null_ReturnsNull()
    {
        var registry = new CliCommandRegistry(Array.Empty<CliCommandSpec>());

        Assert.Null(registry.Resolve(null));
    }

    [Fact]
    public void Resolve_EmptyString_ReturnsNull()
    {
        var registry = new CliCommandRegistry(Array.Empty<CliCommandSpec>());

        Assert.Null(registry.Resolve(""));
    }

    [Fact]
    public void Resolve_Whitespace_ReturnsNull()
    {
        var registry = new CliCommandRegistry(Array.Empty<CliCommandSpec>());

        Assert.Null(registry.Resolve("  "));
    }

    [Fact]
    public void Resolve_Nonexistent_ReturnsNull()
    {
        var build = new CliCommandSpec(Name: "build", Description: "build");
        var registry = new CliCommandRegistry(new[] { build });

        Assert.Null(registry.Resolve("nonexistent"));
    }

    [Fact]
    public void Constructor_EmptyCommandsList_DoesNotThrow()
    {
        var registry = new CliCommandRegistry(Array.Empty<CliCommandSpec>());

        Assert.Null(registry.Resolve("anything"));
    }

    [Fact]
    public void Resolve_CaseInsensitive_FindsCommand()
    {
        var build = new CliCommandSpec(Name: "Build", Description: "build");
        var registry = new CliCommandRegistry(new[] { build });

        Assert.Same(build, registry.Resolve("build"));
        Assert.Same(build, registry.Resolve("BUILD"));
        Assert.Same(build, registry.Resolve("Build"));
    }

    [Fact]
    public void Constructor_DuplicateAliases_SecondOverwritesFirst()
    {
        var first = new CliCommandSpec(Name: "build", Description: "first", Aliases: new[] { "b" });
        var second = new CliCommandSpec(Name: "bundle", Description: "second", Aliases: new[] { "b" });

        var registry = new CliCommandRegistry(new[] { first, second });

        Assert.Same(second, registry.Resolve("b"));
    }
}
