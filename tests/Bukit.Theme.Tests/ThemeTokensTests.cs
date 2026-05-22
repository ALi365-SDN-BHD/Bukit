using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeTokensTests
{
    [Fact]
    public void Merge_ChildOverridesParent()
    {
        var parent = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#000000", ["background"] = "#ffffff" }
        };
        var child = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#ff0000" }
        };

        var merged = child.Merge(parent);
        Assert.NotNull(merged.Colors);
        Assert.Equal("#ff0000", merged.Colors["primary"]);
        Assert.Equal("#ffffff", merged.Colors["background"]);
    }

    [Fact]
    public void Merge_ChildNull_UsesParent()
    {
        var parent = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#000000" }
        };
        var child = new ThemeTokens();

        var merged = child.Merge(parent);
        Assert.NotNull(merged.Colors);
        Assert.Equal("#000000", merged.Colors["primary"]);
    }

    [Fact]
    public void Merge_ParentNull_UsesChild()
    {
        var child = new ThemeTokens
        {
            Colors = new Dictionary<string, string> { ["primary"] = "#ff0000" }
        };

        var merged = child.Merge(new ThemeTokens());
        Assert.NotNull(merged.Colors);
        Assert.Equal("#ff0000", merged.Colors["primary"]);
    }
}
