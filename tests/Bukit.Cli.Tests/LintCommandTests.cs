using Xunit;
using Bukit.Cli.Commands;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class LintCommandTests : IDisposable
{
    private readonly string _root;
    private readonly string _configPath;

    public LintCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "bukit-lint-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "content"));
        _configPath = Path.Combine(_root, "site.yaml");
        File.WriteAllText(_configPath, """
                                      site:
                                        name: lint
                                        title: Lint
                                      content:
                                        provider: markdown
                                        markdown:
                                          dir: content
                                      """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ValidSite_ReturnsZero()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "content", "ok.md"), """
            ---
            title: OK
            ---
            # OK
            Body.
            """);

        var exitCode = await LintCommand.RunAsync(new ArgReader(new[] { "lint", "--config", _configPath }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_MissingMarkdownTitle_ReturnsOne()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "content", "bad.md"), "Body without heading.");

        var exitCode = await LintCommand.RunAsync(new ArgReader(new[] { "lint", "--config", _configPath }));

        Assert.Equal(1, exitCode);
    }
}
