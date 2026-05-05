using Bukit.Cli.Cli.Metadata;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Cli.Rendering;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliRenderingTests
{
    [Fact]
    public void RenderHelp_IncludesUsage_Arguments_AndOptions()
    {
        var spec = new CliCommandSpec(
            Name: "preview",
            Description: "本地预览 dist",
            Arguments: new[] { new CliArgumentSpec("dir", "目录", Required: false) },
            Options: new[] { new CliOptionSpec("--port", "预览端口", CliOptionType.Integer, ValueName: "port") });

        var text = CliHelpRenderer.Render(spec, "bukit preview");

        Assert.Contains("Usage:", text);
        Assert.Contains("bukit preview", text);
        Assert.Contains("--port <port>", text);
    }

    [Fact]
    public void RenderError_PrefixesPrimaryMessage()
    {
        var text = CliErrorRenderer.Render(new CliDiagnostic("invalid-option-value", "Invalid value for --port: abc"));
        Assert.Contains("Error:", text);
        Assert.Contains("Invalid value for --port: abc", text);
    }
}
