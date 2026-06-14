using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class ProgramEntryPointTests
{
    [Fact]
    public async Task Main_NoArgs_PrintsHelpAndReturnsZero()
    {
        var result = await CommandTestSupport.InvokeEntryPointAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: bukit-labs <command> [options]", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_UnknownCommand_ReturnsTwoAndPrintsHint()
    {
        var result = await CommandTestSupport.InvokeEntryPointAsync("mystery");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown labs command: mystery", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("bukit-labs --help", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_CloneWithoutTokens_DispatchesAndReturnsTwo()
    {
        var result = await CommandTestSupport.InvokeEntryPointAsync("clone");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing required option: --tokens <file>", result.StdErr, StringComparison.Ordinal);
    }
}
