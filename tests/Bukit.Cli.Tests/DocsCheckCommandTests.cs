using Bukit.Cli.Commands.DocsCheck;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DocsCheckCommandTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly string _tempDir;

    public DocsCheckCommandTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-docs-check-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwoAndPrintsHelp()
    {
        var result = await InvokeAsync(BuildCommand(arguments: ["verify"]));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown subcommand: docs verify", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Usage: bukit docs check [options]", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RepoRootNotFound_ReturnsOne()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var result = await InvokeAsync(BuildCommand(arguments: ["check"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Could not find repository root", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ConfigFieldError_ReturnsOne()
    {
        var repoRoot = CreateRepoRoot();
        File.WriteAllText(Path.Combine(repoRoot, "README.md"), """
            ```yaml
            site.not_a_real_field: test
            ```
            """);
        Directory.SetCurrentDirectory(repoRoot);

        var result = await InvokeAsync(BuildCommand(
            options: new Dictionary<string, string?> { ["--config-fields"] = null },
            arguments: ["check"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("does not exist in site.yaml schema", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("docs check: errors=1", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WarningsOnly_ReturnsZero()
    {
        var repoRoot = CreateRepoRoot();
        File.WriteAllText(Path.Combine(repoRoot, "README.md"), "# Bukit\n");
        Directory.SetCurrentDirectory(repoRoot);

        var result = await InvokeAsync(BuildCommand(arguments: ["check"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("docs check: errors=0 warnings=", result.StdOut, StringComparison.Ordinal);
    }

    private string CreateRepoRoot()
    {
        var repoRoot = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        File.WriteAllText(Path.Combine(repoRoot, "bukit-core.slnx"), "<Solution />");
        return repoRoot;
    }

    private static CliBoundCommand BuildCommand(
        IReadOnlyDictionary<string, string?>? options = null,
        IReadOnlyList<string>? arguments = null)
    {
        return new CliBoundCommand(options ?? new Dictionary<string, string?>(), arguments ?? []);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(CliBoundCommand command)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await DocsCheckCommand.RunAsync(command);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
