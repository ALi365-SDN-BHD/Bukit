using Bukit.Cli.Shared.Cli.Parsing;
using Bukit.Shared;

namespace Bukit.Cli;

/// <summary>
/// Handles the global <c>--secrets-stdin</c> flag: strips it from the arguments, then reads one JSON
/// object of name to string value from standard input into <see cref="SecretStore"/> before any
/// other work, so tokens never have to sit in the process environment.
/// </summary>
internal static class SecretsStdinOption
{
    internal static readonly IReadOnlyList<string> SupportedCommands = ["build", "dev", "doctor", "config check", "deploy"];

    internal static string[] Strip(string[] args, out bool requested)
    {
        requested = false;
        var output = new List<string>(args.Length);
        foreach (var arg in args)
        {
            if (string.Equals(arg, SecretStore.StdinFlag, StringComparison.Ordinal))
            {
                requested = true;
                continue;
            }

            output.Add(arg);
        }

        return [.. output];
    }

    internal static bool IsSupported(string? command, IReadOnlyList<string> commandArgs)
    {
        if (command is "build" or "dev" or "doctor" or "deploy")
        {
            return true;
        }

        // A usability gate, not a security boundary: `config check` is the only config
        // subcommand that reads tokens.
        if (command is "config")
        {
            return commandArgs.Contains("check", StringComparer.Ordinal);
        }

        return false;
    }

    /// <summary>Loads stdin secrets. Returns a diagnostic on failure; the message never echoes input.</summary>
    internal static CliDiagnostic? Load(string? command, IReadOnlyList<string> commandArgs)
        => Load(command, commandArgs, Console.IsInputRedirected, Console.OpenStandardInput);

    internal static CliDiagnostic? Load(
        string? command,
        IReadOnlyList<string> commandArgs,
        bool inputRedirected,
        Func<Stream> openInput)
    {
        if (!IsSupported(command, commandArgs))
        {
            return new CliDiagnostic(
                SecretsInputException.DiagnosticCode,
                $"--secrets-stdin is supported only by: {string.Join(", ", SupportedCommands.Select(static c => $"bukit {c}"))}.",
                ShowUsage: false);
        }

        if (!inputRedirected)
        {
            return new CliDiagnostic(
                SecretsInputException.DiagnosticCode,
                "--secrets-stdin requires standard input to be a pipe or file holding one JSON object, for example {\"NOTION_TOKEN\":\"...\"}.",
                ShowUsage: false);
        }

        try
        {
            using var input = openInput();
            SecretStore.LoadFromStream(input);
            return null;
        }
        catch (SecretsInputException ex)
        {
            return new CliDiagnostic(SecretsInputException.DiagnosticCode, ex.Message, ShowUsage: false);
        }
    }
}
