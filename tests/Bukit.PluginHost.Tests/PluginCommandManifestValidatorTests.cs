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
        var exception = Record.Exception(() => _validator.ValidateRuntimeCommands(
            "echo",
            staticCommands: [],
            runtimeCommands: [new PluginCommandSpec("runtime", "Runtime command")]));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateRuntimeCommands_RuntimeSubset_AllowsNestedDeclarations()
    {
        var exception = Record.Exception(() => _validator.ValidateRuntimeCommands(
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
            ]));

        Assert.Null(exception);
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

    [Fact]
    public void ValidateRuntimeCommands_DuplicateStaticArgument_ThrowsWithPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Arguments:
                        [
                            new PluginArgumentSpec("value", "Value"),
                            new PluginArgumentSpec("value", "Duplicate")
                        ])
                ],
                runtimeCommands: [new PluginCommandSpec("echo", "Echo")]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("declares duplicate argument for command echo: value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_DuplicateStaticOption_ThrowsWithPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Options:
                        [
                            new PluginOptionSpec("--upper", "flag", "Uppercase"),
                            new PluginOptionSpec("--upper", "flag", "Duplicate")
                        ])
                ],
                runtimeCommands: [new PluginCommandSpec("echo", "Echo")]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("declares duplicate option for command echo: --upper", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_DuplicateNestedArgument_ReportsFullPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Subcommands:
                        [
                            new PluginCommandSpec(
                                "nested",
                                "Nested",
                                Arguments:
                                [
                                    new PluginArgumentSpec("value", "Value"),
                                    new PluginArgumentSpec("value", "Duplicate")
                                ])
                        ])
                ],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Subcommands: [new PluginCommandSpec("nested", "Nested")])
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("declares duplicate argument for command echo nested: value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_DuplicateNestedOption_ReportsFullPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Subcommands:
                        [
                            new PluginCommandSpec(
                                "nested",
                                "Nested",
                                Options:
                                [
                                    new PluginOptionSpec("--upper", "flag", "Uppercase"),
                                    new PluginOptionSpec("--upper", "flag", "Duplicate")
                                ])
                        ])
                ],
                runtimeCommands:
                [
                    new PluginCommandSpec(
                        "echo",
                        "Echo",
                        Subcommands: [new PluginCommandSpec("nested", "Nested")])
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("declares duplicate option for command echo nested: --upper", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_DuplicateTopLevelRuntimeCommand_ThrowsWithPath()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands: [new PluginCommandSpec("echo", "Echo")],
                runtimeCommands:
                [
                    new PluginCommandSpec("echo", "Runtime echo"),
                    new PluginCommandSpec("echo", "Duplicate runtime echo")
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("runtime manifest declares duplicate command: echo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_DuplicateNestedRuntimeCommand_ReportsFullPath()
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
                        "Runtime echo",
                        Subcommands:
                        [
                            new PluginCommandSpec("nested", "First nested"),
                            new PluginCommandSpec("nested", "Duplicate nested")
                        ])
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("runtime manifest declares duplicate command: echo nested", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimeCommands_DuplicateRuntimeCommandWithEmptyStaticCommands_Throws()
    {
        ConfigException exception = Assert.Throws<ConfigException>(() =>
            _validator.ValidateRuntimeCommands(
                "echo",
                staticCommands: [],
                runtimeCommands:
                [
                    new PluginCommandSpec("runtime", "Runtime command"),
                    new PluginCommandSpec("runtime", "Duplicate runtime command")
                ]));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("runtime manifest declares duplicate command: runtime", exception.Message, StringComparison.Ordinal);
    }
}
