namespace Bukit.Engine.Abstractions.Plugins;

public sealed record PluginExecutionInfo(
    string Name,
    string Hook,
    long DurationMs,
    bool Success,
    string? Error);

