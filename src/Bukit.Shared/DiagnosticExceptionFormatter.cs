namespace Bukit.Shared;

public static class DiagnosticExceptionFormatter
{
    public static string Format(BukitException ex)
    {
        if (ex.Code is null)
        {
            return ex.Message;
        }

        return $"[{DiagnosticCodeFormatter.Format(ex.Code.Value)}] {ex.Message}";
    }
}
