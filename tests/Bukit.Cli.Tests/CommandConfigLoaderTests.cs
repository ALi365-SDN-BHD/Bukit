using System.Text;
using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Parsing;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class CommandConfigLoaderTests : IDisposable
{
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalErr = Console.Error;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public CommandConfigLoaderTests()
    {
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalErr);
        _stdout.Dispose();
        _stderr.Dispose();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    public void Load_LoadsOnceAndReplaysEveryLevelInOrderWithoutDeduplication(string format)
    {
        int calls = 0;
        var config = Config("debug");
        var result = CommandConfigLoader.Load("one", format, false, (path, logger) =>
        {
            Assert.Equal("one", path);
            calls++;
            logger.Warn("duplicate");
            logger.Debug("debug");
            logger.Info("info");
            logger.Warn("duplicate");
            logger.Error("error");
            Assert.Empty(_stderr.ToString());
            return config;
        });
        Assert.Same(config, result.Config);
        Assert.Equal(1, calls);
        string[] messages = ["duplicate", "debug", "info", "duplicate", "error"];
        string[] levels = ["Warn", "Debug", "Info", "Warn", "Error"];
        var lines = _stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5, lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            if (format == "json")
            {
                using var json = JsonDocument.Parse(lines[i]);
                Assert.Equal(messages[i], json.RootElement.GetProperty("msg").GetString());
                Assert.Equal(levels[i], json.RootElement.GetProperty("level").GetString());
            }
            else Assert.Equal($"[{levels[i].ToLowerInvariant()}] {messages[i]}", lines[i]);
        }
        Assert.Empty(_stdout.ToString());
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "[warn] pending\n")]
    public void Load_ConfiguredErrorAndCiRetainExistingLevelRules(bool ci, string expected)
    {
        CommandConfigLoader.Load("one", "text", ci, (_, logger) =>
        {
            logger.Warn("pending");
            return Config("error");
        });
        Assert.Equal(expected.Replace("\n", Environment.NewLine), _stderr.ToString());
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    public void Load_FailureReplaysOnceWithWarnBootstrapAndRethrowsSameException(string format)
    {
        var original = new ConfigException("sentinel", DiagnosticCode.ConfigInvalidValue);
        int calls = 0;
        var thrown = Assert.Throws<ConfigException>(() => CommandConfigLoader.Load("one", format, false, (_, logger) =>
        {
            calls++;
            logger.Debug("filtered");
            logger.Info("filtered");
            logger.Warn("first");
            logger.Warn("first");
            throw original;
        }));
        Assert.Same(original, thrown);
        Assert.Equal(1, calls);
        var lines = _stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        foreach (var line in lines)
        {
            if (format == "text") Assert.Equal("[warn] first", line);
            else
            {
                using var json = JsonDocument.Parse(line);
                Assert.Equal("Warn", json.RootElement.GetProperty("level").GetString());
                Assert.Equal("first", json.RootElement.GetProperty("msg").GetString());
            }
        }
        CommandConfigLoader.Load("two", "text", false, (_, _) => Config("debug"));
        Assert.Equal(string.Join(Environment.NewLine, lines) + Environment.NewLine, _stderr.ToString());
    }

    [Fact]
    public void Load_FailureReplayWriterFailureDoesNotReplaceOriginalException()
    {
        var original = new IOException("original load error");
        Console.SetError(new FailingWriter());
        try
        {
            Assert.Same(original, Assert.Throws<IOException>(() => CommandConfigLoader.Load("one", "json", false, (_, logger) =>
            {
                logger.Warn("pending");
                throw original;
            })));
        }
        finally { Console.SetError(_stderr); }
        CommandConfigLoader.Load("two", "text", false, (_, _) => Config("info"));
        Assert.Empty(_stderr.ToString());
    }

    [Fact]
    public void Load_SuccessReplayWriterFailurePropagatesWithoutFailureReplay()
    {
        var writer = new FailingWriter();
        Console.SetError(writer);
        try
        {
            var thrown = Assert.Throws<IOException>(() => CommandConfigLoader.Load("one", "text", false, (_, logger) =>
            {
                logger.Warn("pending");
                return Config("warn");
            }));
            Assert.Equal("output failed", thrown.Message);
            Assert.Equal(1, writer.Calls);
        }
        finally { Console.SetError(_stderr); }
    }

    [Theory]
    [InlineData(null, LogLevel.Info)]
    [InlineData(" ERROR ", LogLevel.Error)]
    [InlineData("unknown", LogLevel.Info)]
    public void ParseLogLevel_PreservesNormalization(string? value, LogLevel expected)
        => Assert.Equal(expected, CommandConfigLoader.ParseLogLevel(value, false));

    private static AppConfig Config(string level) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test" },
        Content = new ContentConfig(),
        Logging = new LoggingConfig { Level = level }
    };

    [Fact]
    public async Task PublicDescriptorFactory_BuildStillHonorsItsBoundJsonOption()
    {
        string root = Path.Combine(Path.GetTempPath(), "bukit-bound-logger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "site.yaml");
        try
        {
            File.WriteAllText(path, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      markdown:\n        dir: content\nbuild:\n  followSymlinks: true\n");
            var descriptor = BukitCliDescriptors.CreateDescriptors().Single(d => d.Spec.Name == "build");
            var parsed = CliParser.Parse(descriptor.Spec, ["--config", path, "--jobs", "0", "--log-format=json"]);
            Assert.True(parsed.IsSuccess);
            await Assert.ThrowsAsync<CommandArgumentException>(() => descriptor.DispatchAsync(parsed));
            using var json = JsonDocument.Parse(_stderr.ToString());
            Assert.Equal("Warn", json.RootElement.GetProperty("level").GetString());
        }
        finally { TestCleanup.DeleteDirectory(root); }
    }

    private sealed class FailingWriter : TextWriter
    {
        public int Calls { get; private set; }
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value)
        {
            Calls++;
            throw new IOException("output failed");
        }
    }
}
