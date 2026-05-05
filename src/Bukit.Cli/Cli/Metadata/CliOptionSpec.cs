namespace Bukit.Cli.Cli.Metadata;

public sealed record CliOptionSpec(
    string Name,
    string Description,
    CliOptionType Type = CliOptionType.String,
    string? ShortName = null,
    bool Required = false,
    string? ValueName = null,
    string? DefaultValueHelp = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? ConflictWith = null);
