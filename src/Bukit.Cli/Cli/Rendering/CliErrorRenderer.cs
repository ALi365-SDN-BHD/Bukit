using Bukit.Cli.Cli.Parsing;

namespace Bukit.Cli.Cli.Rendering;

public static class CliErrorRenderer
{
    public static string Render(CliDiagnostic diagnostic)
    {
        return $"Error: {diagnostic.Message}";
    }
}
