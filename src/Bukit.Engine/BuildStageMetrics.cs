using System.Collections.Concurrent;

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

    internal void AddDuration(string stage, long durationMs)
    {
        _durationsMs.AddOrUpdate(stage, durationMs, (_, current) => current + durationMs);
    }

    internal void Increment(string stage, int delta = 1)
    {
        _counts.AddOrUpdate(stage, delta, (_, current) => current + delta);
    }

    internal BuildStageMetrics Snapshot()
    {
        return new BuildStageMetrics(
            new Dictionary<string, long>(_durationsMs, StringComparer.OrdinalIgnoreCase),
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
