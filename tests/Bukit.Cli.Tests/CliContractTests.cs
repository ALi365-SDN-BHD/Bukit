using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliContractTests
{
    [Fact]
    public void Registry_CommandTree_RemainsStable()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        var tree = registry.Commands
            .Select(command =>
                $"{command.Name}:{string.Join(",", (command.Subcommands ?? []).Select(subcommand => subcommand.Name))}")
            .ToArray();
        var descriptorCommands = BukitCliDescriptors.CreateDescriptors()
            .Select(descriptor => descriptor.Spec.Name)
            .ToArray();

        Assert.Equal(
            [
                "build:",
                "doctor:",
                "config:check,schema",
                "preview:",
                "dev:",
                "clean:",
                "version:",
                "completion:",
                "seo:audit,diff",
                "geo:audit",
                "publish:audit,diff",
                "deploy:",
            ],
            tree);
        Assert.Equal(
            registry.Commands.Select(command => command.Name).OrderBy(name => name, StringComparer.Ordinal),
            descriptorCommands.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Parse_BindingContract_RemainsStable()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Arguments:
            [
                new CliArgumentSpec("source", "source"),
            ],
            Options:
            [
                new CliOptionSpec("--output", "output"),
                new CliOptionSpec("--force", "force", CliOptionType.Flag, ShortName: "-f"),
            ]);

        var result = CliParser.Parse(
            spec,
            ["content", "--OUTPUT=dist=preview", "-F", "--unknown=value"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("content", result.BoundCommand.GetArgument(0));
        Assert.Equal("dist=preview", result.BoundCommand.GetString("--output"));
        Assert.Equal("true", result.BoundCommand.GetString("--force"));
        Assert.Null(result.BoundCommand.GetString("--unknown"));
        Assert.Equal(["unknown-option"], result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void TestHelper_BindsImmediateSubcommandOptionsThroughPublicParser()
    {
        var command = CliTestHelper.CreateCommand(
            "config",
            ["config", "check", "--config", "site.yaml"]);

        Assert.Equal("check", command.GetArgument(0));
        Assert.Equal("site.yaml", command.GetString("--config"));
    }

    [Fact]
    public void Parse_NestedSubcommand_PropagatesLeafDiagnosticsToPublicResult()
    {
        var leaf = new CliCommandSpec(
            Name: "leaf",
            Description: "leaf",
            Options:
            [
                new CliOptionSpec("--count", "count", CliOptionType.Integer),
            ]);
        var group = new CliCommandSpec(
            Name: "group",
            Description: "group",
            Subcommands: [leaf]);
        var root = new CliCommandSpec(
            Name: "root",
            Description: "root",
            Subcommands: [group]);

        var result = CliParser.Parse(root, ["group", "leaf", "--count", "invalid"]);

        Assert.Same(root, result.Command);
        Assert.False(result.IsSuccess);
        Assert.Equal("group", result.BoundCommand.GetArgument(0));
        Assert.Equal(["invalid-option-value"], result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }
}
