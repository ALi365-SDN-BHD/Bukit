using Xunit;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;

namespace Bukit.Cli.Tests;

public sealed class CompletionCommandTests
{
    [Fact]
    public void Render_Bash_IncludesTopLevelCommands()
    {
        var script = CompletionCommand.Render("bash");

        Assert.Contains("build", script, StringComparison.Ordinal);
        Assert.Contains("doctor", script, StringComparison.Ordinal);
        Assert.Contains("config", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Bash_ContainsCompleteFunction()
    {
        var script = CompletionCommand.Render("bash");

        Assert.Contains("_bukit_completion()", script, StringComparison.Ordinal);
        Assert.Contains("complete -F _bukit_completion bukit", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Zsh_ProducesCompletion()
    {
        var script = CompletionCommand.Render("zsh");

        Assert.StartsWith("#compdef bukit", script, StringComparison.Ordinal);
        Assert.Contains("_arguments", script, StringComparison.Ordinal);
        Assert.Contains("build", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Fish_ProducesCompletion()
    {
        var script = CompletionCommand.Render("fish");

        Assert.Contains("complete -c bukit", script, StringComparison.Ordinal);
        Assert.Contains("build", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_InvalidShell_ReturnsEmpty()
    {
        var script = CompletionCommand.Render("powershell");

        Assert.Empty(script);
    }

    [Fact]
    public void Render_NullShell_ReturnsEmpty()
    {
        var script = CompletionCommand.Render(null!);

        Assert.Empty(script);
    }

    [Fact]
    public async Task RunAsync_InvalidShell_ReturnsTwo()
    {
        var exitCode = await CompletionCommand.RunAsync(CliTestHelper.CreateCommand("completion", new[] { "completion", "invalid" }));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void Registry_IncludesCoreCommands()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        Assert.NotNull(registry.Resolve("build"));
        Assert.NotNull(registry.Resolve("doctor"));
        Assert.NotNull(registry.Resolve("config"));
        Assert.NotNull(registry.Resolve("preview"));
        Assert.NotNull(registry.Resolve("clean"));
        Assert.NotNull(registry.Resolve("version"));
        Assert.NotNull(registry.Resolve("completion"));
        Assert.NotNull(registry.Resolve("seo"));
        Assert.NotNull(registry.Resolve("geo"));
        Assert.NotNull(registry.Resolve("publish"));
        Assert.NotNull(registry.Resolve("deploy"));
        Assert.Null(registry.Resolve("lint"));
    }
}
