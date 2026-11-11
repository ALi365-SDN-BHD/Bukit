using System.Diagnostics;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Logs one <c>event=build.report.timing</c> info line per report-stage step and a final
/// <c>step=total</c> line with the wall-clock time of the scope. A step that throws is not
/// logged and the exception propagates unchanged.
/// </summary>
internal sealed class ReportStageTimer
{
    internal const string VariantScope = "variant";
    internal const string FinalizeScope = "finalize";

    private readonly ILogger _logger;
    private readonly string _prefix;
    private readonly long _started = Stopwatch.GetTimestamp();

    internal ReportStageTimer(ILogger logger, string scope, string? language = null)
    {
        _logger = logger;
        _prefix = language is null
            ? $"event=build.report.timing scope={scope}"
            : $"event=build.report.timing scope={scope} language={language}";
    }

    internal void Step(string step, Action action)
    {
        var started = Stopwatch.GetTimestamp();
        action();
        Log(step, started);
    }

    internal T Step<T>(string step, Func<T> action)
    {
        var started = Stopwatch.GetTimestamp();
        var result = action();
        Log(step, started);
        return result;
    }

    internal async Task StepAsync(string step, Func<Task> action)
    {
        var started = Stopwatch.GetTimestamp();
        await action().ConfigureAwait(false);
        Log(step, started);
    }

    internal async Task<T> StepAsync<T>(string step, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await action().ConfigureAwait(false);
        Log(step, started);
        return result;
    }

    internal void Complete() => Log("total", _started);

    private void Log(string step, long started)
    {
        var milliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _logger.Info($"{_prefix} step={step} duration_ms={milliseconds}");
    }
}
