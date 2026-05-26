using Bukit.Cli;
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Cli.Metadata;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DevCommandExtendedTests
{
    [Fact]
    public void CliParser_DevSpec_ParsesAllOptions()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var devSpec = registry.Resolve("dev");
        Assert.NotNull(devSpec);

        var result = CliParser.Parse(devSpec!, new[]
        {
            "--config", "my-site.yaml",
            "--site", "en",
            "--host", "0.0.0.0",
            "--port", "4000",
            "--output", "dist-dev",
            "--no-watch"
        });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);

        var bound = result.BoundCommand;
        Assert.Equal("my-site.yaml", bound.GetString("--config"));
        Assert.Equal("en", bound.GetString("--site"));
        Assert.Equal("0.0.0.0", bound.GetString("--host"));
        Assert.Equal(4000, bound.GetInt("--port"));
        Assert.Equal("dist-dev", bound.GetString("--output"));
        Assert.True(bound.GetBool("--no-watch"));
    }

    [Fact]
    public void CliParser_DevSpec_ValidatesPortAsInteger()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var devSpec = registry.Resolve("dev");
        Assert.NotNull(devSpec);

        var result = CliParser.Parse(devSpec!, new[] { "--port", "abc" });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid-option-value");
    }

    [Fact]
    public void CliParser_DevSpec_RejectsUnknownOption()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var devSpec = registry.Resolve("dev");
        Assert.NotNull(devSpec);

        var result = CliParser.Parse(devSpec!, new[] { "--unknown-flag" });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "unknown-option");
    }

    [Fact]
    public void CliParser_DevSpec_DefaultsMissingOptions()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var devSpec = registry.Resolve("dev");
        Assert.NotNull(devSpec);

        var result = CliParser.Parse(devSpec!, new[] { "--port", "8080" });

        Assert.True(result.IsSuccess);
        var bound = result.BoundCommand;
        Assert.Equal(8080, bound.GetInt("--port"));
        Assert.Null(bound.GetString("--host"));
        Assert.False(bound.GetBool("--no-watch"));
        Assert.Null(bound.GetString("--config"));
        Assert.Null(bound.GetString("--output"));
    }

    [Fact]
    public void CliParser_DevSpec_HelpFlagIsParsed()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var devSpec = registry.Resolve("dev");
        Assert.NotNull(devSpec);

        var emptyResult = CliParser.Parse(devSpec!, Array.Empty<string>());
        Assert.True(emptyResult.IsSuccess);
    }

    [Fact]
    public void DevCommand_ExtractOptions_FromBoundCommand_ReadsAllValues()
    {
        var bound = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = "prod.yaml",
                ["--site"] = "zh",
                ["--host"] = "127.0.0.1",
                ["--port"] = "3000",
                ["--output"] = "custom-dist",
                ["--no-watch"] = "true"
            },
            Array.Empty<string>());

        var (configPath, site, host, port, noWatch, outputOverride) = DevCommand.ExtractOptions(bound);

        Assert.Equal("prod.yaml", configPath);
        Assert.Equal("zh", site);
        Assert.Equal("127.0.0.1", host);
        Assert.Equal(3000, port);
        Assert.True(noWatch);
        Assert.Equal("custom-dist", outputOverride);
    }

    [Fact]
    public void DevCommand_ExtractOptions_FromBoundCommand_DefaultsWhenMissing()
    {
        var bound = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        var (configPath, site, host, port, noWatch, outputOverride) = DevCommand.ExtractOptions(bound);

        Assert.Null(configPath);
        Assert.Null(site);
        Assert.Equal("localhost", host);
        Assert.Equal(35729, port);
        Assert.False(noWatch);
        Assert.Null(outputOverride);
    }
}
