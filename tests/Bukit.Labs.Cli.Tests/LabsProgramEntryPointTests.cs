using System.Reflection;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class LabsProgramEntryPointTests
{
    [Fact]
    public async Task Main_NoArgs_PrintsHelpAndReturnsZero()
    {
        var result = await InvokeEntryPointAsync([]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: bukit-labs <command> [options]", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_UnknownCommand_ReturnsTwo()
    {
        var result = await InvokeEntryPointAsync(["unknown"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown labs command: unknown", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_ImportCommand_ReturnsUnknown()
    {
        var result = await InvokeEntryPointAsync(["import", "mystery"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown labs command: import", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_WebhookHelp_ReturnsZero()
    {
        var result = await InvokeEntryPointAsync(["webhook", "help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: bukit webhook [start] [options]", result.StdOut, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeEntryPointAsync(string[] args)
    {
        var entryPoint = typeof(IntentCommand).Assembly.EntryPoint ?? throw new InvalidOperationException("Missing Bukit.Labs.Cli entry point.");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = entryPoint.Invoke(null, [args]);
            var exitCode = result switch
            {
                Task<int> task => await task,
                Task task => await AwaitAndReturnZeroAsync(task),
                int code => code,
                _ => throw new InvalidOperationException($"Unsupported entry point return type: {result?.GetType().FullName ?? "null"}")
            };

            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static async Task<int> AwaitAndReturnZeroAsync(Task task)
    {
        await task;
        return 0;
    }
}
