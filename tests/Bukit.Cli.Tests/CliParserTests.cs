using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliParserTests
{
    private static readonly CliCommandSpec PreviewSpec =
        new(
            Name: "preview",
            Description: "本地预览 dist",
            Options: new[]
            {
                new CliOptionSpec("--port", "预览端口", CliOptionType.Integer, ValueName: "port"),
                new CliOptionSpec("--strict-port", "严格端口", CliOptionType.Flag),
                new CliOptionSpec("--log-format", "日志格式", CliOptionType.String, AllowedValues: new[] { "text", "json" })
            });

    [Fact]
    public void Parse_ReturnsError_WhenIntegerOptionIsInvalid()
    {
        var result = CliParser.Parse(PreviewSpec, new[] { "--port", "abc" });
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid-option-value");
    }

    [Fact]
    public void Parse_ReturnsError_WhenRequiredArgumentMissing()
    {
        var spec = new CliCommandSpec(
            Name: "theme",
            Description: "主题相关命令",
            Arguments: new[] { new CliArgumentSpec("name", "主题名", Required: true) });

        var result = CliParser.Parse(spec, Array.Empty<string>());

        Assert.Contains(result.Diagnostics, d => d.Code == "missing-argument");
    }

    [Fact]
    public void Parse_DetectsConflictingOptions()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--clean", "clean", CliOptionType.Flag, ConflictWith: "--no-clean"),
                new CliOptionSpec("--no-clean", "no clean", CliOptionType.Flag, ConflictWith: "--clean")
            });

        var result = CliParser.Parse(spec, new[] { "--clean", "--no-clean" });

        Assert.Contains(result.Diagnostics, d => d.Code == "conflicting-options");
    }

    [Fact]
    public void Parse_Succeeds_WithValidIntOption()
    {
        var result = CliParser.Parse(PreviewSpec, new[] { "--port", "8080" });

        Assert.True(result.IsSuccess);
        Assert.Equal(8080, result.BoundCommand.GetInt("--port"));
    }

    [Fact]
    public void Parse_RejectsUnknownOption()
    {
        var result = CliParser.Parse(PreviewSpec, new[] { "--nonexistent", "value" });

        Assert.Contains(result.Diagnostics, d => d.Code == "unknown-option");
    }

    [Fact]
    public void Parse_PreservesDiagnosticOrder()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Arguments:
            [
                new CliArgumentSpec("source", "source", Required: true),
            ],
            Options:
            [
                new CliOptionSpec("--count", "count", CliOptionType.Integer),
                new CliOptionSpec("--value", "value"),
                new CliOptionSpec("--output", "output", Required: true),
                new CliOptionSpec("--clean", "clean", CliOptionType.Flag, ConflictWith: "--no-clean"),
                new CliOptionSpec("--no-clean", "no clean", CliOptionType.Flag, ConflictWith: "--clean"),
            ]);

        var result = CliParser.Parse(
            spec,
            ["--unknown", "--count", "not-an-integer", "--value", "--clean", "--no-clean"]);

        Assert.Equal(
            [
                "unknown-option",
                "invalid-option-value",
                "missing-option-value",
                "missing-argument",
                "missing-option",
                "conflicting-options",
                "conflicting-options",
            ],
            result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }
}
