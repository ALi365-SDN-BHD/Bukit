namespace Bukit.Plugin.Abstractions.Manifest;

public sealed record PluginCommandSpec(
    string Name,
    string Description,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<PluginArgumentSpec>? Arguments = null,
    IReadOnlyList<PluginOptionSpec>? Options = null,
    IReadOnlyList<PluginCommandSpec>? Subcommands = null)
{
    public IReadOnlyList<string> Aliases { get; init; } = Aliases ?? [];
    public IReadOnlyList<PluginArgumentSpec> Arguments { get; init; } = Arguments ?? [];
    public IReadOnlyList<PluginOptionSpec> Options { get; init; } = Options ?? [];
    public IReadOnlyList<PluginCommandSpec> Subcommands { get; init; } = Subcommands ?? [];
}
