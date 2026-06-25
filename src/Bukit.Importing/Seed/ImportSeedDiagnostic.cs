namespace Bukit.Importing.Seed;

public sealed record ImportSeedDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);
