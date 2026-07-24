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
        var capture = await ImportConsoleCapture.CaptureAsync(action);
        return new ImportPluginConsoleCaptureResult<ImportCommandResult>(
            capture.Result,
            capture.Exception,
            capture.StdOutLines,
            capture.StdErrLines);
    }
}
