namespace Bukit.Cli.Cli.Metadata;

public sealed record CliArgumentSpec(
    string Name,
    string Description,
    bool Required = false,
    string? DefaultValueHelp = null);
