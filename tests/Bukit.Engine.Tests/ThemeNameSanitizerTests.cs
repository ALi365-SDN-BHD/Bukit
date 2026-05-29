using Xunit;

namespace Bukit.Engine.Tests;

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

    [Fact]
    public void TrySanitize_Should_Accept_ValidName()
    {
        Assert.True(ThemeNameSanitizer.TrySanitize("my-theme_v2.1", out var sanitized, out var err));
        Assert.Equal("my-theme_v2.1", sanitized);
        Assert.Null(err);
    }
}
