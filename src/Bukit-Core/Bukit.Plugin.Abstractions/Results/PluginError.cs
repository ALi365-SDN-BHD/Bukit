using System.Text.Json;

namespace Bukit.Plugin.Abstractions.Results;

public sealed record PluginError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, JsonElement>? Details = null)
{
    public IReadOnlyDictionary<string, JsonElement> Details { get; init; } = Details ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
