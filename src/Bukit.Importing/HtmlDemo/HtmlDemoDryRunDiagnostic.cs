namespace Bukit.Importing.HtmlDemo;

public sealed record HtmlDemoDryRunDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);
