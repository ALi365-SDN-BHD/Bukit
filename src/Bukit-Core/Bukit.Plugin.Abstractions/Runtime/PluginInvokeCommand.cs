using System.Text.Json;

namespace Bukit.Plugin.Abstractions.Runtime;

public sealed record PluginInvokeCommand(
    string Name,
    IReadOnlyList<string>? Path = null,
    IReadOnlyList<string>? Arguments = null,
    IReadOnlyDictionary<string, JsonElement>? Options = null)
{
    public IReadOnlyList<string> Path { get; init; } = Path ?? [];
    public IReadOnlyList<string> Arguments { get; init; } = Arguments ?? [];
    public IReadOnlyDictionary<string, JsonElement> Options { get; init; } = Options ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
