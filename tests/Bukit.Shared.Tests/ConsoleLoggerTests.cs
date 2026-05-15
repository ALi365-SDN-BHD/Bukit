using System.Text.Json;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class ConsoleLoggerTests
{
    [Fact]
    public void Constructor_DefaultFormat_ProducesTextOutput()
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var output = CaptureStderr(() => logger.Info("hello"));
        Assert.Contains("[info] hello", output);
    }

    [Fact]
    public void Constructor_JsonFormat_DoesNotThrow()
    {
        var logger = new ConsoleLogger(LogLevel.Info, "json");
        var output = CaptureStderr(() => logger.Info("hello"));
        Assert.NotEmpty(output);
    }

    [Fact]
    public void Info_WritesToStderr()
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var output = CaptureStderr(() => logger.Info("test message"));
        Assert.Contains("[info] test message", output);
    }

    [Fact]
    public void Warn_WritesToStderr()
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var output = CaptureStderr(() => logger.Warn("warning message"));
        Assert.Contains("[warn] warning message", output);
    }

    [Fact]
    public void Error_WritesToStderr()
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var output = CaptureStderr(() => logger.Error("error message"));
        Assert.Contains("[error] error message", output);
    }

    [Fact]
    public void JsonFormat_ProducesValidJson()
    {
        var logger = new ConsoleLogger(LogLevel.Info, "json");
        var output = CaptureStderr(() => logger.Info("test message"));

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.Equal("Info", root.GetProperty("level").GetString());
        Assert.Equal("test message", root.GetProperty("msg").GetString());
        Assert.True(root.TryGetProperty("ts", out _));
    }

    [Fact]
    public void Debug_WhenLevelIsInfo_IsFiltered()
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var output = CaptureStderr(() => logger.Debug("debug message"));
        Assert.DoesNotContain("debug message", output);
    }

    [Fact]
    public void Debug_WhenLevelIsDebug_IsShown()
    {
        var logger = new ConsoleLogger(LogLevel.Debug);
        var output = CaptureStderr(() => logger.Debug("debug message"));
        Assert.Contains("[debug] debug message", output);
    }

    [Fact]
    public void Write_NullMessage_DoesNotThrow()
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var ex = Record.Exception(() => logger.Info(null!));
        Assert.Null(ex);
    }

    [Fact]
    public void Write_EmptyMessage_DoesNotThrow()
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var ex = Record.Exception(() => logger.Info(""));
        Assert.Null(ex);
    }

    private static string CaptureStderr(Action action)
    {
        var original = Console.Error;
        try
        {
            using var writer = new StringWriter();
            Console.SetError(writer);
            action();
            return writer.ToString().Trim();
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
