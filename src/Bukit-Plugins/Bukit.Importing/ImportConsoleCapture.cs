namespace Bukit.Importing;

/// <summary>
/// Captures Console.Out and Console.Error output while executing an async action.
/// Shared utility used by both ImportCommandWorkflow and ImportPluginConsoleCapture.
/// </summary>
public sealed record ImportConsoleCaptureResult<T>(
    T? Result,
    Exception? Exception,
    IReadOnlyList<string> StdOutLines,
    IReadOnlyList<string> StdErrLines);

/// <summary>
/// Thread-safe console output capture. Redirects Console.Out and Console.Error
/// to in-memory buffers, executes the action, then restores the original streams.
/// <para>
/// Logging convention: lower-level classes (ImportAnalyzer, HtmlDemoImporter,
/// ImportReportWriter, ContentDraftWriter) write progress/diagnostic output
/// directly to Console.Out/Console.Error. The workflow layer (ImportCommandWorkflow)
/// and the plugin adapter (ImportPluginConsoleCapture) wrap those calls with
/// this utility so that all console output is captured as structured messages
/// rather than written to the actual process stdout/stderr.
/// </para>
/// </summary>
public static class ImportConsoleCapture
{
    public static async Task<ImportConsoleCaptureResult<T>> CaptureAsync<T>(
        Func<Task<T>> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        T? result = default;
        Exception? exception = null;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            result = await action();
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return new ImportConsoleCaptureResult<T>(
            result,
            exception,
            ReadLines(stdout.ToString()).ToArray(),
            ReadLines(stderr.ToString()).ToArray());
    }

    private static IEnumerable<string> ReadLines(string value)
    {
        using var reader = new StringReader(value);
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}
