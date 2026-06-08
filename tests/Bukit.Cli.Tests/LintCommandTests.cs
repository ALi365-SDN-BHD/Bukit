using System.Reflection;
using Xunit;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class LintCommandTests : IDisposable
{
    private readonly string _root;
    private readonly string _configPath;

    private static readonly MethodInfo s_hasTitle = typeof(LintCommand)
        .GetMethod("HasTitle", BindingFlags.NonPublic | BindingFlags.Static)!;

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
                                        sources:
                                          - type: markdown
                                            name: page
                                            collection: page
                                            markdown:
                                              dir: content
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

    [Theory]
    [InlineData("---\ntitle: Hello\n---\n# Hello\n", true)]
    [InlineData("---\ntitle: Hello\n---\nBody.", true)]
    [InlineData("# Just heading\nBody.", true)]
    [InlineData("Body without heading or front matter.", false)]
    [InlineData("---\ndate: 2024-01-01\n---\nNo title field.", false)]
    public void HasTitle_ReturnsExpected(string markdown, bool expected)
    {
        var result = (bool)s_hasTitle.Invoke(null, new object[] { markdown })!;
        Assert.Equal(expected, result);
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

        var exitCode = await LintCommand.RunAsync(CliTestHelper.CreateCommand("lint", new[] { "lint", "--config", _configPath }));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_MissingMarkdownTitle_ReturnsOne()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "content", "bad.md"), "Body without heading.");

        var exitCode = await LintCommand.RunAsync(CliTestHelper.CreateCommand("lint", new[] { "lint", "--config", _configPath }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_MissingConfig_ReturnsOne()
    {
        var exitCode = await LintCommand.RunAsync(CliTestHelper.CreateCommand("lint", new[] { "lint", "--config", Path.Combine(_root, "nonexistent.yaml") }));

        Assert.Equal(1, exitCode);
    }
}
