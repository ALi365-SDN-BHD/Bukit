namespace Bukit.PluginHost.ProcessTree;

/// <summary>Aggregated resource usage across an entire process tree.</summary>
internal readonly record struct ProcessTreeUsage(
    TimeSpan CpuTime,
    long PeakMemoryBytes);
