namespace Bukit.Cli.Commands;

/// <summary>
/// Commands that run before secrets, configuration, plugins, logging or telemetry: help, and
/// the hidden git credential helper whose stdout must carry only the credential protocol.
/// </summary>
internal static class EarlyCommands
{
    /// <summary>The exit code when an early command handled <paramref name="args"/>, else null.</summary>
    internal static async Task<int?> TryRunAsync(string[] args)
    {
        if (args is [GitCredentialCommand.Name, .. var helperArgs])
        {
            await using var input = Console.OpenStandardInput();
            await using var output = Console.OpenStandardOutput();
            return await GitCredentialCommand.RunAsync(helperArgs, input, output, Console.Error).ConfigureAwait(false);
        }

        if (args is [] or ["help" or "--help" or "-h", ..])
        {
            HelpPrinter.Print();
            return 0;
        }

        return null;
    }
}
