using System.Globalization;
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
    public void JsonFormat_IncludesTraceAndSpanFromEnvironment()
    {
        var originalTrace = Environment.GetEnvironmentVariable("BUKIT_TRACE_ID");
        var originalSpan = Environment.GetEnvironmentVariable("BUKIT_SPAN_ID");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_TRACE_ID", "trace-123");
            Environment.SetEnvironmentVariable("BUKIT_SPAN_ID", "span-456");

            var logger = new ConsoleLogger(LogLevel.Info, "json");
            var output = CaptureStderr(() => logger.Info("traced"));

            using var doc = JsonDocument.Parse(output);
            Assert.Equal("trace-123", doc.RootElement.GetProperty("traceId").GetString());
            Assert.Equal("span-456", doc.RootElement.GetProperty("spanId").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_TRACE_ID", originalTrace);
            Environment.SetEnvironmentVariable("BUKIT_SPAN_ID", originalSpan);
        }
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

    [Fact]
    public void Constructor_InvalidFormatEnum_DoesNotThrow()
    {
        var logger = new ConsoleLogger(LogLevel.Info, "xml");
        var output = CaptureStderr(() => logger.Info("hello"));
        Assert.Contains("[info] hello", output);
    }

    [Fact]
    public void Constructor_EmptyStringFormat_FallsBackToText()
    {
        var logger = new ConsoleLogger(LogLevel.Info, "   ");
        var output = CaptureStderr(() => logger.Info("hello"));
        Assert.Contains("[info] hello", output);
    }

    [Fact]
    public void Warn_WhenLevelIsWarn_DebugAndInfoAreFiltered()
    {
        var logger = new ConsoleLogger(LogLevel.Warn);
        Assert.Empty(CaptureStderr(() => logger.Debug("debug")));
        Assert.Empty(CaptureStderr(() => logger.Info("info")));
        Assert.Contains("[warn] warning", CaptureStderr(() => logger.Warn("warning")));
    }

    [Fact]
    public void Error_WhenLevelIsError_LowerLevelsAreFiltered()
    {
        var logger = new ConsoleLogger(LogLevel.Error);
        Assert.Empty(CaptureStderr(() => logger.Warn("warn")));
        Assert.Contains("[error] err", CaptureStderr(() => logger.Error("err")));
    }

    [Fact]
    public void JsonFormat_DebugLevel_ProducesValidJson()
    {
        var logger = new ConsoleLogger(LogLevel.Debug, "json");
        var output = CaptureStderr(() => logger.Debug("dbg"));
        using var doc = System.Text.Json.JsonDocument.Parse(output);
        Assert.Equal("Debug", doc.RootElement.GetProperty("level").GetString());
        Assert.Equal("dbg", doc.RootElement.GetProperty("msg").GetString());
    }

    [Fact]
    public void TextFormat_UsesExactPrefixFormat()
    {
        var logger = new ConsoleLogger(LogLevel.Debug);
        var output = CaptureStderr(() => logger.Debug("hello"));
        Assert.Equal("[debug] hello", output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void JsonFormat_ControlCharactersRoundTrip_AndBlankCorrelationIdsAreOmitted(string? correlationId)
    {
        var originalTrace = Environment.GetEnvironmentVariable("BUKIT_TRACE_ID");
        var originalSpan = Environment.GetEnvironmentVariable("BUKIT_SPAN_ID");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_TRACE_ID", correlationId);
            Environment.SetEnvironmentVariable("BUKIT_SPAN_ID", correlationId);
            const string message = "quote=\" slash=\\ line=\n tab=\t control=\u0001 中文";
            var before = DateTimeOffset.UtcNow;
            var output = CaptureStderr(() => new ConsoleLogger(LogLevel.Info, " JSON ").Warn(message));
            var after = DateTimeOffset.UtcNow;

            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            Assert.Equal("Warn", root.GetProperty("level").GetString());
            Assert.Equal(message, root.GetProperty("msg").GetString());
            Assert.False(root.TryGetProperty("traceId", out _));
            Assert.False(root.TryGetProperty("spanId", out _));
            Assert.DoesNotContain('\n', output);
            var timestamp = DateTimeOffset.ParseExact(root.GetProperty("ts").GetString()!, "O", CultureInfo.InvariantCulture);
            Assert.InRange(timestamp, before, after);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_TRACE_ID", originalTrace);
            Environment.SetEnvironmentVariable("BUKIT_SPAN_ID", originalSpan);
        }
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
