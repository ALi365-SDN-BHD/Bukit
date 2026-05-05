namespace Bukit.Cli.Cli.Parsing;

public sealed record CliDiagnostic(string Code, string Message, bool ShowUsage = true);
