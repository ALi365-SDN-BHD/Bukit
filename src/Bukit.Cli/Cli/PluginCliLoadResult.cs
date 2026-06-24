using Bukit.Cli.Shared.Cli.Metadata;

namespace Bukit.Cli;

public sealed record PluginCliLoadResult(
    IReadOnlyList<CommandDescriptor> Descriptors,
    IReadOnlyList<PluginListRecord> Plugins)
{
    public static PluginCliLoadResult Empty { get; } = new([], []);
}

public sealed record PluginListRecord(
    string Id,
    string Version,
    bool Enabled,
    string Platform,
    IReadOnlyList<string> Commands);
