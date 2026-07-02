namespace Bukit.Plugin.Abstractions.Security;

public sealed record PluginEnvironmentPermission(
    IReadOnlyList<string>? Read = null)
{
    public IReadOnlyList<string> Read { get; init; } = Read ?? [];
}
