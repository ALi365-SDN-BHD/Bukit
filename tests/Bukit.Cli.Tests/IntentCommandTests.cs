using Bukit.Cli.Commands;
using Bukit.Cli.Intent;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class IntentCommandTests : IDisposable
{
    private readonly string _tempDir;

    public IntentCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-intent-cmd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task RunAsync_NoSubcommand_PrintsHelp()
    {
        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent" }));

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task RunAsync_Help_PrintsHelp()
    {
        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "help" }));

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsError()
    {
        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "unknown" }));

        Assert.Equal(2, code);
    }

    [Fact]
    public async Task RunAsync_Validate_MissingIntentPath_ReturnsError()
    {
        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "validate" }));

        Assert.Equal(2, code);
    }

    [Fact]
    public async Task RunAsync_Validate_InvalidIntentFile_ReturnsError()
    {
        var intentPath = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(intentPath, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: invalid
theme:
  name: starter
""");

        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "validate", intentPath }));

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task RunAsync_Validate_ValidIntent_ReturnsSuccess()
    {
        var intentPath = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(intentPath, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: markdown
theme:
  name: starter
""");

        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "validate", intentPath }));

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task RunAsync_Apply_MissingIntentPath_ReturnsError()
    {
        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "apply" }));

        Assert.Equal(2, code);
    }

    [Fact]
    public async Task RunAsync_Apply_ValidIntent_WritesOutput()
    {
        var intentPath = Path.Combine(_tempDir, "intent.yaml");
        var outPath = Path.Combine(_tempDir, "site.yaml");
        File.WriteAllText(intentPath, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: markdown
theme:
  name: starter
""");

        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "apply", intentPath, "--out", outPath }));

        Assert.Equal(0, code);
        Assert.True(File.Exists(outPath));
    }

    [Fact]
    public async Task RunAsync_Apply_InvalidIntent_ReturnsError()
    {
        var intentPath = Path.Combine(_tempDir, "intent.yaml");
        var outPath = Path.Combine(_tempDir, "site.yaml");
        File.WriteAllText(intentPath, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: invalid
theme:
  name: starter
""");

        var code = await IntentCommand.RunAsync(CliTestHelper.CreateCommand("intent", new[] { "intent", "apply", intentPath, "--out", outPath }));

        Assert.Equal(1, code);
    }
}
