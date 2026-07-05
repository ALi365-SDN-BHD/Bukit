using Bukit.Importing;

namespace Bukit.Plugin.Import;

public sealed record ImportPluginConsoleCaptureResult<T>(
    T? Result,
    Exception? Exception,
    IReadOnlyList<string> StdOutLines,
    IReadOnlyList<string> StdErrLines);

public static class ImportPluginConsoleCapture
{
    public static async Task<ImportPluginConsoleCaptureResult<ImportCommandResult>> CaptureAsync(
        Func<Task<ImportCommandResult>> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        ImportCommandResult? result = null;
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

        return new ImportPluginConsoleCaptureResult<ImportCommandResult>(
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
