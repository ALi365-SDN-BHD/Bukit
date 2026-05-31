namespace Bukit.Importing;

public sealed record ImportDiagnostic(
    ImportDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? FilePath = null,
    int? LineNumber = null);

public enum ImportDiagnosticSeverity
{
    Info,
    Warning,
    Error
}
