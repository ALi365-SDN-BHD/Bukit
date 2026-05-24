using Xunit;
using Bukit.Cli.Commands;

namespace Bukit.Cli.Tests;

public sealed class CompletionCommandTests
{
    [Fact]
    public void Render_Bash_IncludesTopLevelCommands()
    {
        var script = CompletionCommand.Render("bash");

        Assert.Contains("build", script, StringComparison.Ordinal);
        Assert.Contains("lint", script, StringComparison.Ordinal);
        Assert.Contains("config", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_IncludesLintAndCompletionCommands()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        Assert.NotNull(registry.Resolve("lint"));
        Assert.NotNull(registry.Resolve("completion"));
    }
}
