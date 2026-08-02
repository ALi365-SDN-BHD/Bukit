namespace Bukit.Plugin.Abstractions.Config;

public sealed record PluginResourceLimitOptions(
    int? MaxCpuTimeMs = null,
    long? MaxMemoryBytes = null);
