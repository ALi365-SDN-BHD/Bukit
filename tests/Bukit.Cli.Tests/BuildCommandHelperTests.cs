using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class BuildCommandHelperTests
{
    private static readonly MethodInfo s_tryParsePositiveInt = typeof(BuildCommand)
        .GetMethod("TryParsePositiveInt", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly MethodInfo s_parseLogLevel = typeof(BuildCommand)
        .GetMethod("ParseLogLevel", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    [Fact]
    public void TryParsePositiveInt_WhenMissing_ReturnsNull()
    {
        var value = (int?)s_tryParsePositiveInt.Invoke(null, [null]);
        Assert.Null(value);
    }

    [Fact]
    public void TryParsePositiveInt_WhenPositive_ReturnsNumber()
    {
        var value = (int?)s_tryParsePositiveInt.Invoke(null, [" 7 "]);
        Assert.Equal(7, value);
    }

    [Fact]
    public void TryParsePositiveInt_WhenInvalid_ThrowsCommandArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => s_tryParsePositiveInt.Invoke(null, ["0"]));
        Assert.IsType<CommandArgumentException>(ex.InnerException);
        Assert.Equal("--jobs must be a positive integer", ex.InnerException!.Message);
    }

    [Theory]
    [InlineData("debug", false, LogLevel.Debug)]
    [InlineData("info", false, LogLevel.Info)]
    [InlineData("warn", false, LogLevel.Warn)]
    [InlineData("error", false, LogLevel.Error)]
    [InlineData("unknown", false, LogLevel.Info)]
    [InlineData("debug", true, LogLevel.Warn)]
    public void ParseLogLevel_ReturnsExpectedLevel(string? configured, bool isCi, LogLevel expected)
    {
        var level = (LogLevel)s_parseLogLevel.Invoke(null, [configured, isCi])!;
        Assert.Equal(expected, level);
    }
}
