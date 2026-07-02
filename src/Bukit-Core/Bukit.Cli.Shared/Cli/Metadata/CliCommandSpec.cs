namespace Bukit.Cli.Shared.Cli.Metadata;

public sealed record CliCommandSpec(
    string Name,
    string Description,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<CliArgumentSpec>? Arguments = null,
    IReadOnlyList<CliOptionSpec>? Options = null,
    IReadOnlyList<CliCommandSpec>? Subcommands = null);
