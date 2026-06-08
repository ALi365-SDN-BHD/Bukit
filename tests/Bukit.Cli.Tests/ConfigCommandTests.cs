using System.Text;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;

    public ConfigCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-config-command-tests-" + Guid.NewGuid().ToString("N"));
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
    public void Registry_IncludesConfigCheckCommand()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var config = registry.Resolve("config");

        Assert.NotNull(config);
        Assert.NotNull(config!.Subcommands);
        Assert.Contains(config.Subcommands!, sub => sub.Name == "check");
        Assert.Contains(config.Subcommands!, sub => sub.Name == "schema");
    }

    [Fact]
    public async Task Check_ValidConfig_ReturnsZero()
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

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[] { "config", "check", "--config", _configPath }));

            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("Config check passed", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(_configPath, output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Check_InvalidConfig_ReturnsOne()
    {
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         baseUrl: relative
                                       content:
                                         sources:
                                           - type: markdown
                                             name: page
                                             collection: page
                                             markdown:
                                               dir: content
                                       """);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[] { "config", "check", "--config", _configPath }));

            Assert.Equal(1, exitCode);
            var output = writer.ToString();
            Assert.Contains("Config error", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("baseUrl", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Check_MissingConfig_ReturnsOne()
    {
        var missingPath = Path.Combine(_rootDir, "missing.yaml");

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[] { "config", "check", "--config", missingPath }));

            Assert.Equal(1, exitCode);
            Assert.Contains("Config file not found", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Check_SiteUrlOverride_IsValidated()
    {
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         url: not-a-url
                                       content:
                                         sources:
                                           - type: markdown
                                             name: page
                                             collection: page
                                             markdown:
                                               dir: content
                                       """);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[]
            {
                "config", "check", "--config", _configPath, "--site-url", "https://example.com"
            }));

            Assert.Equal(0, exitCode);
            Assert.Contains("siteUrl=https://example.com", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Schema_WritesJsonSchemaToOutputFile()
    {
        var outputPath = Path.Combine(_rootDir, "site.schema.json");

        var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[]
        {
            "config", "schema", "--output", outputPath
        }));

        Assert.Equal(0, exitCode);
        var json = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("\"$schema\"", json, StringComparison.Ordinal);
        Assert.Contains("\"site\"", json, StringComparison.Ordinal);
        Assert.Contains("\"content\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_NoSubcommand_ReturnsTwo()
    {
        var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[] { "config" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwo()
    {
        var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[] { "config", "unknown" }));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task Schema_WithoutOutput_PrintsToConsole()
    {
        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config", new[] { "config", "schema" }));

            Assert.Equal(0, exitCode);
            Assert.Contains("\"$schema\"", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
