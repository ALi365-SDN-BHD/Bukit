using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;

namespace Bukit.Cli.Shared.Cli.Parsing;

public abstract record CliParseResult(
    CliCommandSpec Command,
    CliBoundCommand BoundCommand,
    IReadOnlyList<CliDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0;
}

public sealed record SimpleParseResult(
    CliCommandSpec Command,
    CliBoundCommand BoundCommand,
    IReadOnlyList<CliDiagnostic> Diagnostics)
    : CliParseResult(Command, BoundCommand, Diagnostics);

public sealed record SubcommandParseResult(
    CliCommandSpec Command,
    CliBoundCommand BoundCommand,
    IReadOnlyList<CliDiagnostic> Diagnostics,
    string SubcommandName,
    CliParseResult InnerResult)
    : CliParseResult(Command, BoundCommand, Diagnostics);
