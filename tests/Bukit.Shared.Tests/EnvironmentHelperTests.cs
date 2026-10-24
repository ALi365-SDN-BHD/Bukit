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
    [InlineData(" tRuE ", true)]
    [InlineData(" YeS ", true)]
    [InlineData(" 1 ", true)]
    [InlineData("on", false)]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("random", false)]
    public void IsAutoSummaryEnabled_VariousValues_ReturnsExpected(string? envValue, bool expected)
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

    [Theory]
    [InlineData("secret_abc123")]
    [InlineData("  secret_abc123  ")]
    public void GetNotionToken_WhenSet_ReturnsValue(string value)
    {
        SetEnv(EnvironmentHelper.NotionTokenKey, value, original =>
        {
            var result = EnvironmentHelper.GetNotionToken();
            Assert.Equal(value, result);
        });
    }

    [Fact]
    public void GetNotionToken_WhenNotSet_ReturnsNull()
    {
        SetEnv(EnvironmentHelper.NotionTokenKey, null, original =>
        {
            var result = EnvironmentHelper.GetNotionToken();
            Assert.Null(result);
        });
    }

    [Fact]
    public void GetNotionToken_WhenEmpty_ReturnsEmptyString()
    {
        SetEnv(EnvironmentHelper.NotionTokenKey, "", original =>
        {
            var result = EnvironmentHelper.GetNotionToken();
            Assert.Equal("", result);
        });
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("  +42  ", 42)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("2147483648", 200)]
    [InlineData("-2147483649", 200)]
    [InlineData("1.5", 200)]
    [InlineData("1,000", 200)]
    public void GetAutoSummaryMaxLength_IntegerBoundaries_ReturnExpected(string value, int expected)
    {
        SetEnv(EnvironmentHelper.AutoSummaryMaxLenKey, value, _ =>
            Assert.Equal(expected, EnvironmentHelper.GetAutoSummaryMaxLength()));
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
