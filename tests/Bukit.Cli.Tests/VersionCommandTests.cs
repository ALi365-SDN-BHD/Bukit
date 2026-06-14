using System.Text;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class VersionCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsZero()
    {
        var exitCode = await Bukit.Cli.Commands.VersionCommand.RunAsync(CliTestHelper.CreateCommand("version", Array.Empty<string>()));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithHelp_PrintsUsageAndReturnsZero()
    {
        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            var exitCode = await Bukit.Cli.Commands.VersionCommand.RunAsync(
                new Bukit.Cli.Shared.Cli.Binding.CliBoundCommand(
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["--help"] = "true"
                    },
                    Array.Empty<string>()));

            Assert.Equal(0, exitCode);
            Assert.Contains("Usage: bukit version", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
