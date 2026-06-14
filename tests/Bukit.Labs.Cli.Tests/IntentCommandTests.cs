using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection(IntentApplierCollection.Name)]
public sealed class IntentCommandTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly string _rootDir;

    public IntentCommandTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-intent-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_NoSubcommand_PrintsHelpAndReturnsZero()
    {
        var result = await InvokeAsync(new CliBoundCommand(new Dictionary<string, string?>(), []));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("bukit intent", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("bukit intent apply", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwoAndPrintsHelp()
    {
        var result = await InvokeAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["mystery"]));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown intent subcommand: mystery", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("bukit intent validate", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Validate_MissingIntentPath_ReturnsTwo()
    {
        var result = await InvokeAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["validate"]));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing intent path.", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Validate_InvalidIntent_ReturnsOneAndPrintsErrors()
    {
        using var scope = new CurrentDirectoryScope(_rootDir);

        var intentPath = WriteIntent(
            """
            site:
              name: demo
              title: Demo
              base_url: /
            content:
              kind: mystery
            theme:
              name: starter
            """);

        var result = await InvokeAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["validate", intentPath]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("content.kind must be markdown|notion.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Apply_ValidIntentUnderSites_WritesConfigAndPrintsTip()
    {
        using var scope = new CurrentDirectoryScope(_rootDir);

        Directory.CreateDirectory(Path.Combine(_rootDir, "sites"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "starter"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "content"));

        var intentPath = WriteIntent(
            """
            site:
              name: demo
              title: Demo
              base_url: /
            content:
              kind: markdown
              markdown:
                dir: content
            theme:
              name: starter
            """);

        var outPath = Path.Combine(_rootDir, "sites", "demo.yaml");

        var result = await InvokeAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--out"] = outPath },
            ["apply", intentPath]));

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outPath));
        Assert.Contains("Wrote config:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Tip: use `bukit build --site <name>`", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    private string WriteIntent(string content)
    {
        var path = Path.Combine(_rootDir, "intent.yaml");
        File.WriteAllText(path, content.Replace("\r\n", "\n"));
        return path;
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
            var exitCode = await IntentCommand.RunAsync(command);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string _original;

        public CurrentDirectoryScope(string directory)
        {
            _original = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(directory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_original);
        }
    }
}
