namespace Bukit.Plugin.Abstractions.Security;

public sealed record PluginFileSystemPermission(
    IReadOnlyList<string>? Read = null,
    IReadOnlyList<string>? Write = null)
{
    public IReadOnlyList<string> Read { get; init; } = Read ?? [];
    public IReadOnlyList<string> Write { get; init; } = Write ?? [];
}
