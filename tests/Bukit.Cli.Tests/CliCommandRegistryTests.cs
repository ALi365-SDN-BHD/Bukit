using Bukit.Cli.Cli.Metadata;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliCommandRegistryTests
{
    [Fact]
    public void Resolve_FindsCommandByName_AndAlias()
    {
        var build = new CliCommandSpec(
            Name: "build",
            Description: "生成静态站点",
            Aliases: new[] { "b" },
            Arguments: Array.Empty<CliArgumentSpec>(),
            Options: Array.Empty<CliOptionSpec>(),
            Subcommands: Array.Empty<CliCommandSpec>());

        var registry = new CliCommandRegistry(new[] { build });

        Assert.Same(build, registry.Resolve("build"));
        Assert.Same(build, registry.Resolve("b"));
    }
}
