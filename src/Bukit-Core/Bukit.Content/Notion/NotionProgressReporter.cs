using System.Diagnostics;
using Bukit.Shared;

namespace Bukit.Content.Notion;

/// <summary>
/// Thread-safe progress logging for long Notion phases, so large sites do not sit
/// silent for minutes. Logs at most every <see cref="Interval"/> items and once at the end.
/// </summary>
internal sealed class NotionProgressReporter
{
    internal const int Interval = 200;

    private readonly ILogger? _logger;
    private readonly string _phase;
    private readonly int _total;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _done;

    internal NotionProgressReporter(ILogger? logger, string phase, int total)
    {
        _logger = logger;
        _phase = phase;
        _total = total;
    }

    internal void Increment()
    {
        var done = Interlocked.Increment(ref _done);
        if (done % Interval == 0 && done < _total)
        {
            Log(done);
        }
    }

    internal void Complete()
    {
        if (_total > 0)
        {
            Log(Volatile.Read(ref _done));
        }
    }

    private void Log(int done) =>
        _logger?.Info($"event=notion.progress phase={_phase} done={done} total={_total} elapsed_ms={_stopwatch.ElapsedMilliseconds}");
}
