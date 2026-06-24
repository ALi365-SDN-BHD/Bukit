namespace Bukit.Plugin.Abstractions.Runtime;

public sealed record PluginInvokeContext(
    string RootDir,
    string WorkingDir,
    string? ConfigPath = null,
    string? OutputDir = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public IReadOnlyDictionary<string, string> Environment { get; init; } = Environment ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
