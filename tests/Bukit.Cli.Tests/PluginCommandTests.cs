using System.Text;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class PluginCommandTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;

    public PluginCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        _configPath = Path.Combine(_rootDir, "site.yaml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_NoSubcommand_ReturnsTwo()
    {
        var exitCode = await PluginCommand.RunAsync(CliTestHelper.CreateCommand("plugin", new[] { "plugin" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_Help_ReturnsZero()
    {
        var exitCode = await PluginCommand.RunAsync(CliTestHelper.CreateCommand("plugin", new[] { "plugin", "help" }));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwo()
    {
        var exitCode = await PluginCommand.RunAsync(CliTestHelper.CreateCommand("plugin", new[] { "plugin", "unknown" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_List_UsesConfigAndShowsEnabledState()
    {
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         plugins:
                                           taxonomy:
                                             enabled: false
                                       content:
                                         provider: markdown
                                       """);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await PluginCommand.RunAsync(CliTestHelper.CreateCommand("plugin", new[] { "plugin", "list", "--config", _configPath }));

            Assert.Equal(0, exitCode);
            var text = writer.ToString();
            Assert.Contains("taxonomy@", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("enabled=false", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_List_ShowsExternalPluginConfigWhenDisabled()
    {
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         externalPlugins:
                                           sample:
                                             runtime: process
                                             entry: plugins/sample-plugin.exe
                                             hooks:
                                               - after-build
                                             enabled: false
                                       content:
                                         provider: markdown
                                       """);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await PluginCommand.RunAsync(CliTestHelper.CreateCommand("plugin", new[] { "plugin", "list", "--config", _configPath }));

            Assert.Equal(0, exitCode);
            var text = writer.ToString();
            Assert.Contains("external-config:", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sample", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("enabled=false", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("runtime=process", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hooks=after-build", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("negotiation=handshake-v2", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
