using Xunit.Abstractions;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class PlatformPathHelperTests(ITestOutputHelper output)
{
    [Fact]
    public void PathComparison_OnWindows_ReturnsOrdinalIgnoreCase()
    {
        if (!OperatingSystem.IsWindows())
        {
            output.WriteLine("BUKIT_NOT_APPLICABLE: Windows path comparison");
            return;
        }

        Assert.Equal(StringComparison.OrdinalIgnoreCase, PlatformPathHelper.PathComparison);
    }

    [Fact]
    public void PathComparison_OnNonWindows_ReturnsOrdinal()
    {
        if (OperatingSystem.IsWindows())
        {
            output.WriteLine("BUKIT_NOT_APPLICABLE: non-Windows path comparison");
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
