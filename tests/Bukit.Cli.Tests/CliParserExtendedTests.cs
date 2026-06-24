using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliParserExtendedTests
{
    [Fact]
    public void Parse_AllowedValues_InvalidValueProducesDiagnostic()
    {
        var spec = new CliCommandSpec(
            Name: "export",
            Description: "export",
            Options: new[]
            {
                new CliOptionSpec("--format", "format", CliOptionType.String, AllowedValues: new[] { "json", "text" }),
            });

        var result = CliParser.Parse(spec, new[] { "--format", "csv" });

        Assert.Contains(result.Diagnostics, d => d.Code == "invalid-option-value");
    }

    [Fact]
    public void Parse_AllowedValues_ValidValueAccepted()
    {
        var spec = new CliCommandSpec(
            Name: "export",
            Description: "export",
            Options: new[]
            {
                new CliOptionSpec("--format", "format", CliOptionType.String, AllowedValues: new[] { "json", "text" }),
            });

        var result = CliParser.Parse(spec, new[] { "--format", "json" });

        Assert.True(result.IsSuccess);
        Assert.Equal("json", result.BoundCommand.GetString("--format"));
    }

    [Fact]
    public void Parse_ShortName_ParsesCorrectly()
    {
        var spec = new CliCommandSpec(
            Name: "preview",
            Description: "preview",
            Options: new[]
            {
                new CliOptionSpec("--port", "port", CliOptionType.Integer, ShortName: "-p"),
            });

        var result = CliParser.Parse(spec, new[] { "-p", "8080" });

        Assert.True(result.IsSuccess);
        Assert.Equal(8080, result.BoundCommand.GetInt("--port"));
    }

    [Fact]
    public void Parse_FlagWithShortName_Works()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--force", "force", CliOptionType.Flag, ShortName: "-f"),
            });

        var result = CliParser.Parse(spec, new[] { "-f" });

        Assert.True(result.IsSuccess);
        Assert.True(result.BoundCommand.GetBool("--force"));
    }

    [Fact]
    public void Parse_MissingOptionValue_ProducesDiagnostic()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--output", "output", CliOptionType.String),
            });

        var result = CliParser.Parse(spec, new[] { "--output" });

        Assert.Contains(result.Diagnostics, d => d.Code == "missing-option-value");
    }

    [Fact]
    public void Parse_MultiplePositionalAndOptions_MixedCorrectly()
    {
        var spec = new CliCommandSpec(
            Name: "deploy",
            Description: "deploy",
            Arguments: new[]
            {
                new CliArgumentSpec("target", "target"),
                new CliArgumentSpec("path", "path"),
            },
            Options: new[]
            {
                new CliOptionSpec("--provider", "provider", CliOptionType.String),
                new CliOptionSpec("--force", "force", CliOptionType.Flag, ShortName: "-f"),
            });

        var result = CliParser.Parse(spec, new[] { "github", "--provider", "pages", "--force", "/some/path" });

        Assert.True(result.IsSuccess);
        Assert.Equal("github", result.BoundCommand.GetArgument(0));
        Assert.Equal("/some/path", result.BoundCommand.GetArgument(1));
        Assert.Equal("pages", result.BoundCommand.GetString("--provider"));
        Assert.True(result.BoundCommand.GetBool("--force"));
    }

    [Fact]
    public void Parse_EmptyArgs_ReturnsEmptyResult()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Arguments: new[] { new CliArgumentSpec("source", "source") });

        var result = CliParser.Parse(spec, Array.Empty<string>());

        Assert.NotNull(result);
        Assert.Null(result.BoundCommand.GetArgument(0));
    }

    [Fact]
    public void Parse_RequiredArgumentMissing_ProducesDiagnostic()
    {
        var spec = new CliCommandSpec(
            Name: "init",
            Description: "init",
            Arguments: new[] { new CliArgumentSpec("name", "name", Required: true) });

        var result = CliParser.Parse(spec, Array.Empty<string>());

        Assert.Contains(result.Diagnostics, d => d.Code == "missing-argument");
    }

    [Fact]
    public void Parse_RequiredOptionMissing_ProducesDiagnostic()
    {
        var spec = new CliCommandSpec(
            Name: "publish",
            Description: "publish",
            Options: new[]
            {
                new CliOptionSpec("--target", "target", Required: true),
            });

        var result = CliParser.Parse(spec, Array.Empty<string>());

        Assert.Contains(result.Diagnostics, d => d.Code == "missing-option");
    }

    [Fact]
    public void Parse_SubcommandRequiredOptionMissing_PropagatesDiagnostic()
    {
        var spec = new CliCommandSpec(
            Name: "import",
            Description: "import",
            Subcommands:
            [
                new CliCommandSpec(
                    Name: "html-demo",
                    Description: "html demo",
                    Options:
                    [
                        new CliOptionSpec("--theme", "theme", Required: true),
                    ])
            ]);

        var result = CliParser.Parse(spec, ["html-demo"]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "missing-option");
    }

    [Fact]
    public void Parse_OptionFollowedByOption_DoesNotConsumeNextOptionAsValue()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--config", "config path", CliOptionType.String),
                new CliOptionSpec("--clean", "clean build", CliOptionType.Flag),
            });

        var result = CliParser.Parse(spec, new[] { "--config", "--clean" });

        Assert.Contains(result.Diagnostics, d => d.Code == "missing-option-value");
        Assert.True(result.BoundCommand.GetBool("--clean"));
        Assert.Null(result.BoundCommand.GetString("--config"));
    }

    [Fact]
    public void Parse_StringOptionBeforeFlag_FlagNotConsumedAsValue()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--output", "output dir", CliOptionType.String),
                new CliOptionSpec("--force", "force build", CliOptionType.Flag, ShortName: "-f"),
            });

        var bound = CliBoundCommandFactory.Create(new[] { "--output", "-f" }, spec);

        Assert.Equal("true", bound.GetString("--force"));
        Assert.Null(bound.GetString("--output"));
    }

    [Fact]
    public void Parse_OptionEqualsValue_ParsesInlineValue()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--config", "config path", CliOptionType.String),
            });

        var result = CliParser.Parse(spec, new[] { "--config=path/to/file.yaml" });

        Assert.True(result.IsSuccess);
        Assert.Equal("path/to/file.yaml", result.BoundCommand.GetString("--config"));
    }

    [Fact]
    public void Parse_OptionEqualsValueWithMultipleEquals_ParsesCorrectly()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--filter", "filter expression", CliOptionType.String),
            });

        var result = CliParser.Parse(spec, new[] { "--filter=a=b=c" });

        Assert.True(result.IsSuccess);
        Assert.Equal("a=b=c", result.BoundCommand.GetString("--filter"));
    }

    [Fact]
    public void Parse_FlagOptionEqualsValue_ProducesDiagnostic()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--clean", "clean build", CliOptionType.Flag),
            });

        var result = CliParser.Parse(spec, new[] { "--clean=true" });

        Assert.Contains(result.Diagnostics, d => d.Code == "invalid-option-value");
    }

    [Fact]
    public void Parse_UnknownOptionEqualsValue_ReportsUnknownOption()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build");

        var result = CliParser.Parse(spec, new[] { "--unknown=value" });

        Assert.Contains(result.Diagnostics, d => d.Code == "unknown-option");
    }

    [Fact]
    public void Parse_MixedInlineAndSeparateValue_BothParsedCorrectly()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Options: new[]
            {
                new CliOptionSpec("--config", "config path", CliOptionType.String),
                new CliOptionSpec("--output", "output dir", CliOptionType.String),
                new CliOptionSpec("--force", "force build", CliOptionType.Flag),
            });

        var result = CliParser.Parse(spec, new[] { "--config=site.yaml", "--force", "--output", "dist" });

        Assert.True(result.IsSuccess);
        Assert.Equal("site.yaml", result.BoundCommand.GetString("--config"));
        Assert.True(result.BoundCommand.GetBool("--force"));
        Assert.Equal("dist", result.BoundCommand.GetString("--output"));
    }
}
