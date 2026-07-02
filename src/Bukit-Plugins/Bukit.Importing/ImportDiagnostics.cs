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

public sealed class ImportException : Exception
{
    public ImportErrorKind Kind { get; }
    public ImportException(ImportErrorKind kind, string message) : base(message)
    {
        Kind = kind;
    }
    public ImportException(ImportErrorKind kind, string message, Exception inner) : base(message, inner)
    {
        Kind = kind;
    }
}

public enum ImportErrorKind
{
    UserInput,
    Internal
}
