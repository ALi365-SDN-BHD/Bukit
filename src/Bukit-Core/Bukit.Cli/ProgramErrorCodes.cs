using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli;

/// <summary>Keeps the top-level entry point's type coupling below the analyzer threshold.</summary>
internal static class ProgramErrorCodes
{
    internal static string? Format(DiagnosticCode? code)
        => code.HasValue ? DiagnosticCodeFormatter.Format(code.Value) : null;

    internal static bool IsSnapshot(Exception exception)
        => NotionSnapshot.IsSnapshotError(exception);

    internal static string? SnapshotCode(Exception exception)
        => NotionSnapshot.CodeOf(exception);
}
