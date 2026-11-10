using System.Collections.Concurrent;
using System.Diagnostics;

namespace Bukit.Engine;

internal sealed record BuildStageMetrics(
    IReadOnlyDictionary<string, long> DurationsMs,
    IReadOnlyDictionary<string, int> Counts)
{
    internal static BuildStageMetrics Empty { get; } = new(
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

    internal static BuildStageMetrics Merge(params BuildStageMetrics[] metrics)
    {
        if (metrics.Length == 0)
        {
            return Empty;
        }

        var durations = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var metric in metrics)
        {
            foreach (var kv in metric.DurationsMs)
            {
                durations[kv.Key] = durations.TryGetValue(kv.Key, out var existing)
                    ? existing + kv.Value
                    : kv.Value;
            }

            foreach (var kv in metric.Counts)
            {
                counts[kv.Key] = counts.TryGetValue(kv.Key, out var existing)
                    ? existing + kv.Value
                    : kv.Value;
            }
        }

        return new BuildStageMetrics(durations, counts);
    }
}

internal sealed class BuildStageMetricsCollector
{
    private readonly ConcurrentDictionary<string, long> _durationsMs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _elapsedTicks = new(StringComparer.OrdinalIgnoreCase);

    internal void AddDuration(string stage, long durationMs)
    {
        _durationsMs.AddOrUpdate(stage, durationMs, (_, current) => current + durationMs);
    }

    /// <summary>
    /// Adds a duration in <see cref="Stopwatch"/> ticks. Ticks are summed first and converted
    /// to milliseconds once in <see cref="Snapshot"/>, so many sub-millisecond durations are
    /// not each truncated to zero.
    /// </summary>
    internal void AddElapsedTicks(string stage, long ticks)
    {
        _elapsedTicks.AddOrUpdate(stage, ticks, (_, current) => current + ticks);
    }

    internal void Increment(string stage, int delta = 1)
    {
        _counts.AddOrUpdate(stage, delta, (_, current) => current + delta);
    }

    internal BuildStageMetrics Snapshot()
    {
        var durations = new Dictionary<string, long>(_durationsMs, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _elapsedTicks)
        {
            var milliseconds = (long)(kv.Value * 1000.0 / Stopwatch.Frequency);
            durations[kv.Key] = durations.TryGetValue(kv.Key, out var existing)
                ? existing + milliseconds
                : milliseconds;
        }

        return new BuildStageMetrics(
            durations,
            new Dictionary<string, int>(_counts, StringComparer.OrdinalIgnoreCase));
    }

    internal BuildStageMetricsCollector Merge(BuildStageMetrics metrics)
    {
        foreach (var kv in metrics.DurationsMs)
        {
            AddDuration(kv.Key, kv.Value);
        }

        foreach (var kv in metrics.Counts)
        {
            Increment(kv.Key, kv.Value);
        }

        return this;
    }
}
