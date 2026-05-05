using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Parsing;

public sealed record CliParseResult(
    CliCommandSpec Command,
    CliBoundCommand BoundCommand,
    IReadOnlyList<CliDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0;
}
