using Xunit;

namespace Bukit.Shared.Tests;

public sealed class PlatformPathHelperTests
{
    [Fact]
    public void PathComparison_OnWindows_ReturnsOrdinalIgnoreCase()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.False(OperatingSystem.IsWindows(), "Test only runs on Windows");
            return;
        }

        Assert.Equal(StringComparison.OrdinalIgnoreCase, PlatformPathHelper.PathComparison);
    }

    [Fact]
    public void PathComparison_OnNonWindows_ReturnsOrdinal()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.True(OperatingSystem.IsWindows(), "Test only runs on non-Windows");
            return;
        }

        Assert.Equal(StringComparison.Ordinal, PlatformPathHelper.PathComparison);
    }

    [Fact]
    public void PathComparison_IsCorrectForCurrentPlatform()
    {
        var expected = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Assert.Equal(expected, PlatformPathHelper.PathComparison);
    }

    [Fact]
    public void PathComparison_IsNotCurrentCulture()
    {
        Assert.NotEqual(StringComparison.CurrentCulture, PlatformPathHelper.PathComparison);
        Assert.NotEqual(StringComparison.CurrentCultureIgnoreCase, PlatformPathHelper.PathComparison);
    }

    [Fact]
    public void PathComparison_IsOrdinalBased()
    {
        Assert.True(
            PlatformPathHelper.PathComparison == StringComparison.Ordinal ||
            PlatformPathHelper.PathComparison == StringComparison.OrdinalIgnoreCase);
    }
}
