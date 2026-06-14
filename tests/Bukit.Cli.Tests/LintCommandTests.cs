using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class LintCommandTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;

    public LintCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-lint-command-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        _configPath = Path.Combine(_rootDir, "site.yaml");
        Directory.CreateDirectory(Path.Combine(_rootDir, "content"));
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_ValidMarkdown_ReturnsZero()
    {
        WriteConfig();
        File.WriteAllText(Path.Combine(_rootDir, "content", "hello.md"), """
            ---
            title: Hello
            ---
            Body
            """);

        var result = await InvokeAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--config"] = _configPath },
            []));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Lint passed.", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MarkdownWithoutTitleOrHeading_ReturnsOne()
    {
        WriteConfig();
        File.WriteAllText(Path.Combine(_rootDir, "content", "broken.md"), "plain body only");

        var result = await InvokeAsync(new CliBoundCommand(
            new Dictionary<string, string?> { ["--config"] = _configPath },
            []));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("missing front matter title and first-level heading", result.StdOut, StringComparison.Ordinal);
    }

    private void WriteConfig()
    {
        File.WriteAllText(_configPath, """
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            """);
    }

    private static async Task<(int ExitCode, string StdOut)> InvokeAsync(CliBoundCommand command)
    {
        using var stdout = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(stdout);
            var exitCode = await LintCommand.RunAsync(command);
            return (exitCode, stdout.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
