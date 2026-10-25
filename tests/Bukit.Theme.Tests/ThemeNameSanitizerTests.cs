using Xunit;

namespace Bukit.Theme.Tests;

public sealed class ThemeNameSanitizerTests
{
    [Fact]
    public void TrySanitize_Should_Reject_DotDotSegment()
    {
        Assert.False(ThemeNameSanitizer.TrySanitize("..", out _, out var err1));
        Assert.NotNull(err1);

        Assert.False(ThemeNameSanitizer.TrySanitize("../foo", out _, out var err2));
        Assert.NotNull(err2);
    }

    [Fact]
    public void TrySanitize_Should_Reject_AbsolutePath()
    {
        Assert.False(ThemeNameSanitizer.TrySanitize("/usr/local", out _, out var err1));
        Assert.NotNull(err1);

        Assert.False(ThemeNameSanitizer.TrySanitize("C:\\foo", out _, out var err2));
        Assert.NotNull(err2);
    }

    [Fact]
    public void TrySanitize_Should_Reject_PathSeparator()
    {
        Assert.False(ThemeNameSanitizer.TrySanitize("foo/bar", out _, out var err1));
        Assert.NotNull(err1);

        Assert.False(ThemeNameSanitizer.TrySanitize("foo\\bar", out _, out var err2));
        Assert.NotNull(err2);
    }

    [Fact]
    public void TrySanitize_Should_Reject_ControlChars()
    {
        Assert.False(ThemeNameSanitizer.TrySanitize("foo\u0001bar", out _, out var err1));
        Assert.NotNull(err1);

        Assert.False(ThemeNameSanitizer.TrySanitize("foo\tbar", out _, out var err2));
        Assert.NotNull(err2);
    }

    [Fact]
    public void TrySanitize_Should_Reject_WindowsDeviceName()
    {
        Assert.False(ThemeNameSanitizer.TrySanitize("con", out _, out var err1));
        Assert.NotNull(err1);

        Assert.False(ThemeNameSanitizer.TrySanitize("nul", out _, out var err2));
        Assert.NotNull(err2);

        Assert.False(ThemeNameSanitizer.TrySanitize("COM1", out _, out var err3));
        Assert.NotNull(err3);
    }

    [Fact]
    public void TrySanitize_Should_Reject_NullOrWhitespace()
    {
        Assert.False(ThemeNameSanitizer.TrySanitize(null, out _, out var err1));
        Assert.NotNull(err1);

        Assert.False(ThemeNameSanitizer.TrySanitize("   ", out _, out var err2));
        Assert.NotNull(err2);
    }

    [Theory]
    [InlineData(" PrN ")]
    [InlineData("aux")]
    [InlineData("COM0")]
    [InlineData("Lpt9")]
    [InlineData("com١")]
    public void TrySanitize_PreservesReservedDeviceDiagnostic(string raw)
    {
        Assert.False(ThemeNameSanitizer.TrySanitize(raw, out var sanitized, out var error));
        Assert.Equal(string.Empty, sanitized);
        Assert.Equal($"theme name '{raw}' is a reserved Windows device name.", error);
    }

    [Theory]
    [InlineData("COM1.txt")]
    [InlineData("com10")]
    [InlineData("lptx")]
    public void TrySanitize_PreservesOriginalDeviceNameLimits(string raw)
    {
        Assert.True(ThemeNameSanitizer.TrySanitize(raw, out var sanitized, out var error));
        Assert.Equal(raw, sanitized);
        Assert.Null(error);
    }

    [Fact]
    public void TrySanitize_Should_Accept_ValidName()
    {
        Assert.True(ThemeNameSanitizer.TrySanitize("my-theme_v2.1", out var sanitized, out var err));
        Assert.Equal("my-theme_v2.1", sanitized);
        Assert.Null(err);
    }
}
