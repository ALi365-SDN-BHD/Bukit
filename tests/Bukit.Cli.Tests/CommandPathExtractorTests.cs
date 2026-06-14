using Bukit.Cli.Shared.Cli.Metadata;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CommandPathExtractorTests
{
    [Fact]
    public void ExtractAllCommandPaths_ReturnsNestedPathsInTraversalOrder()
    {
        var serve = CreateSpec("serve");
        var preview = CreateSpec("preview", subcommands: [serve]);
        var build = CreateSpec("build", subcommands: [preview]);
        var clean = CreateSpec("clean");
        var registry = new CliCommandRegistry([build, clean]);

        var paths = CommandPathExtractor.ExtractAllCommandPaths(registry);

        Assert.Equal(["build", "build preview", "build preview serve", "clean"], paths);
    }

    [Fact]
    public void ExtractCommandOptions_ReturnsOptionsForResolvedPath()
    {
        var publish = CreateSpec(
            "publish",
            options:
            [
                new CliOptionSpec("--target", "target", CliOptionType.String),
            ]);
        var build = CreateSpec("build", subcommands: [publish]);
        var registry = new CliCommandRegistry([build]);

        var options = CommandPathExtractor.ExtractCommandOptions("build publish", registry);

        Assert.Same(publish.Options, options);
    }

    [Fact]
    public void ExtractCommandOptions_UnknownPath_ReturnsNull()
    {
        var registry = new CliCommandRegistry([CreateSpec("build")]);

        Assert.Null(CommandPathExtractor.ExtractCommandOptions("build publish", registry));
    }

    [Fact]
    public void ResolveSpec_UsesRootAliasesAndIgnoresExtraWhitespace()
    {
        var publish = CreateSpec("publish");
        var build = CreateSpec("build", aliases: ["b"], subcommands: [publish]);
        var registry = new CliCommandRegistry([build]);

        var spec = CommandPathExtractor.ResolveSpec("  b   PUBLISH  ", registry);

        Assert.Same(publish, spec);
    }

    [Fact]
    public void ResolveSpec_InvalidPath_ReturnsNull()
    {
        var build = CreateSpec("build");
        var registry = new CliCommandRegistry([build]);

        Assert.Null(CommandPathExtractor.ResolveSpec("", registry));
        Assert.Null(CommandPathExtractor.ResolveSpec("   ", registry));
        Assert.Null(CommandPathExtractor.ResolveSpec("missing", registry));
        Assert.Null(CommandPathExtractor.ResolveSpec("build publish", registry));
    }

    private static CliCommandSpec CreateSpec(
        string name,
        IReadOnlyList<string>? aliases = null,
        IReadOnlyList<CliOptionSpec>? options = null,
        IReadOnlyList<CliCommandSpec>? subcommands = null)
    {
        return new CliCommandSpec(name, $"{name} command", aliases, Options: options, Subcommands: subcommands);
    }
}
