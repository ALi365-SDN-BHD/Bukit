using Bukit.Plugin.Abstractions.Manifest;
using Bukit.PluginHost;
using Bukit.Shared;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginCommandManifestValidatorTests
{
    private readonly PluginCommandManifestValidator _validator = new();

    [Fact]
    public void ValidateRuntimeCommands_EmptyStaticCommands_AllowsAnyRuntimeCommands()
    {
        _validator.ValidateRuntimeCommands(
            "echo",
            staticCommands: [],
            runtimeCommands: [new PluginCommandSpec("runtime", "Runtime command")]);
    }

    [Fact]
    public void ValidateRuntimeCommands_RuntimeSubset_AllowsNestedDeclarations()
    {
        _validator.ValidateRuntimeCommands(
            "echo",
            staticCommands:
            [
                new PluginCommandSpec(
                    "echo",
                    "Echo command",
                    Aliases: ["say"],
                    Arguments: [new PluginArgumentSpec("value", "Value", Required: true)],
                    Options: [new PluginOptionSpec("--upper", "flag", "Uppercase")],
                    Subcommands: [new PluginCommandSpec("nested", "Nested command")])
            ],
            runtimeCommands:
            [
                new PluginCommandSpec(
                    "echo",
                    "Runtime echo",
                    Aliases: ["say"],
                    Arguments: [new PluginArgumentSpec("value", "Value", Required: true)],
                    Options: [new PluginOptionSpec("--upper", "flag", "Uppercase")],
                    Subcommands: [new PluginCommandSpec("nested", "Runtime nested")])
            ]);
    }

    [Fact]
    public void ValidateRuntimeCommands_DuplicateStaticCommand_ThrowsWithPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec("echo", "First"),
                    new PluginCommandSpec("echo", "Duplicate")
                ],
                runtimeCommands: []));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("declares duplicate command: echo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_UnknownRuntimeCommand_ThrowsWithPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands: [new PluginCommandSpec("echo", "Echo")],
                runtimeCommands: [new PluginCommandSpec("hidden", "Hidden")]));

        Assert.Equal(DiagnosticCode.PluginCapabilityMissing, exception.Code);
        Assert.Contains("runtime manifest command is not declared in plugin.yaml: hidden", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_UndeclaredRuntimeAlias_Throws()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands: [new PluginCommandSpec("echo", "Echo", Aliases: ["say"])],
                runtimeCommands: [new PluginCommandSpec("echo", "Echo", Aliases: ["speak"])]));

        Assert.Equal(DiagnosticCode.PluginCapabilityMissing, exception.Code);
        Assert.Contains("runtime manifest alias is not declared in plugin.yaml for command echo: speak", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_UndeclaredRuntimeArgument_Throws()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands: [new PluginCommandSpec("echo", "Echo")],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Arguments: [new PluginArgumentSpec("extra", "Extra")])
                ]));

        Assert.Equal(DiagnosticCode.PluginCapabilityMissing, exception.Code);
        Assert.Contains("runtime manifest argument is not declared in plugin.yaml for command echo: extra", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_RuntimeArgumentBecomesRequired_Throws()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Arguments: [new PluginArgumentSpec("value", "Value")])
                ],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Arguments: [new PluginArgumentSpec("value", "Value", Required: true)])
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("runtime manifest argument required=true conflicts with plugin.yaml for command echo: value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_UndeclaredRuntimeOption_Throws()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands: [new PluginCommandSpec("echo", "Echo")],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Options: [new PluginOptionSpec("--upper", "flag", "Uppercase")])
                ]));

        Assert.Equal(DiagnosticCode.PluginCapabilityMissing, exception.Code);
        Assert.Contains("runtime manifest option is not declared in plugin.yaml for command echo: --upper", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_RuntimeOptionTypeMismatch_Throws()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Options: [new PluginOptionSpec("--upper", "flag", "Uppercase")])
                ],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Options: [new PluginOptionSpec("--upper", "string", "Uppercase")])
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("runtime manifest option type conflicts with plugin.yaml for command echo: --upper", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_RuntimeOptionBecomesRequired_Throws()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Options: [new PluginOptionSpec("--upper", "flag", "Uppercase")])
                ],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Options: [new PluginOptionSpec("--upper", "flag", "Uppercase", Required: true)])
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("runtime manifest option required=true conflicts with plugin.yaml for command echo: --upper", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_NestedUnknownCommand_ReportsFullPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Subcommands: [new PluginCommandSpec("nested", "Nested")])
                ],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Subcommands: [new PluginCommandSpec("hidden", "Hidden")])
                ]));

        Assert.Contains("runtime manifest command is not declared in plugin.yaml: echo hidden", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_RejectsEmptyPluginIdOrNullCollections()
    {
        Assert.Equal(
            "pluginId",
            Assert.Throws<ArgumentException>(() =>
                _validator.ValidateRuntimeCommands(" ", staticCommands: [], runtimeCommands: [])).ParamName);
        Assert.Equal(
            "staticCommands",
            Assert.Throws<ArgumentNullException>(() =>
                _validator.ValidateRuntimeCommands("echo", staticCommands: null!, runtimeCommands: [])).ParamName);
        Assert.Equal(
            "runtimeCommands",
            Assert.Throws<ArgumentNullException>(() =>
                _validator.ValidateRuntimeCommands("echo", staticCommands: [], runtimeCommands: null!)).ParamName);
    }
}
