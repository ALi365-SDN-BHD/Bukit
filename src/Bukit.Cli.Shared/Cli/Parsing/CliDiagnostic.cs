namespace Bukit.Cli.Shared.Cli.Parsing;

public sealed record CliDiagnostic(string Code, string Message, bool ShowUsage = true);
