using Xunit;

namespace Bukit.Config.Tests;

public sealed class SharedAssetsPathTests
{
    [Theory]
    [InlineData("assets", "assets/")]
    [InlineData("assets/", "assets/")]
    [InlineData("\\assets\\", "assets/")]
    [InlineData("./fonts/", "fonts/")]
    [InlineData("assets//x", "assets/x/")]
    [InlineData("assets/./x", "assets/x/")]
    [InlineData(".", "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("../assets", "../assets/")]
    public void Normalize_ProducesRelativePrefix(string input, string expected)
    {
        Assert.Equal(expected, SharedAssetsPath.Normalize(input));
    }

    [Theory]
    [InlineData("/assets/", true)]
    [InlineData("\\assets\\", true)]
    [InlineData("C:\\assets", true)]
    [InlineData("//server/share", true)]
    [InlineData("assets/", false)]
    [InlineData("./assets/", false)]
    public void IsAbsolute_DetectsEveryPlatformPrefix(string input, bool expected)
    {
        Assert.Equal(expected, SharedAssetsPath.IsAbsolute(input));
    }
}
