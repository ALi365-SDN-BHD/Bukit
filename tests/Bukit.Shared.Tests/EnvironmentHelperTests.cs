using Xunit;

namespace Bukit.Shared.Tests;

public sealed class EnvironmentHelperTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("random", false)]
    public void IsAutoSummaryEnabled_VariousValues_ReturnsExpected(string envValue, bool expected)
    {
        SetEnv(EnvironmentHelper.AutoSummaryKey, envValue, original =>
        {
            var result = EnvironmentHelper.IsAutoSummaryEnabled();
            Assert.Equal(expected, result);
        });
    }

    [Fact]
    public void GetAutoSummaryMaxLength_ValidValue_ReturnsParsedInt()
    {
        SetEnv(EnvironmentHelper.AutoSummaryMaxLenKey, "500", original =>
        {
            var result = EnvironmentHelper.GetAutoSummaryMaxLength();
            Assert.Equal(500, result);
        });
    }

    [Fact]
    public void GetAutoSummaryMaxLength_InvalidValue_ReturnsDefault()
    {
        SetEnv(EnvironmentHelper.AutoSummaryMaxLenKey, "not-a-number", original =>
        {
            var result = EnvironmentHelper.GetAutoSummaryMaxLength();
            Assert.Equal(200, result);
        });
    }

    [Fact]
    public void GetAutoSummaryMaxLength_ZeroValue_ReturnsDefault()
    {
        SetEnv(EnvironmentHelper.AutoSummaryMaxLenKey, "0", original =>
        {
            var result = EnvironmentHelper.GetAutoSummaryMaxLength();
            Assert.Equal(200, result);
        });
    }

    [Fact]
    public void GetAutoSummaryMaxLength_NegativeValue_ReturnsDefault()
    {
        SetEnv(EnvironmentHelper.AutoSummaryMaxLenKey, "-1", original =>
        {
            var result = EnvironmentHelper.GetAutoSummaryMaxLength();
            Assert.Equal(200, result);
        });
    }

    [Fact]
    public void GetAutoSummaryMaxLength_EmptyEnv_ReturnsDefault()
    {
        SetEnv(EnvironmentHelper.AutoSummaryMaxLenKey, null, original =>
        {
            var result = EnvironmentHelper.GetAutoSummaryMaxLength();
            Assert.Equal(200, result);
        });
    }

    private static void SetEnv(string key, string? value, Action<string?> action)
    {
        var original = Environment.GetEnvironmentVariable(key);
        try
        {
            if (value is null)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
            else
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            action(original);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, original);
        }
    }
}
