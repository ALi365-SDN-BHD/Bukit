using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionColorPaletteTests
{
    [Theory]
    [InlineData("gray", "#787774")]
    [InlineData("Gray", "#787774")]
    [InlineData("GRAY", "#787774")]
    [InlineData("brown", "#64473A")]
    [InlineData("orange", "#D9730D")]
    [InlineData("yellow", "#DFAB01")]
    [InlineData("green", "#0F7B6C")]
    [InlineData("blue", "#0B6E99")]
    [InlineData("purple", "#6940A5")]
    [InlineData("pink", "#AD1A72")]
    [InlineData("red", "#E03E3E")]
    public void ToForeground_KnownColors_ReturnsCorrectHex(string color, string expected)
    {
        var result = NotionColorPalette.ToForeground(color);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToForeground_UnknownColor_ReturnsInherit()
    {
        var result = NotionColorPalette.ToForeground("nonexistent");

        Assert.Equal("inherit", result);
    }

    [Fact]
    public void ToForeground_EmptyString_ReturnsInherit()
    {
        var result = NotionColorPalette.ToForeground("");

        Assert.Equal("inherit", result);
    }

    [Theory]
    [InlineData("gray_background", "#F1F1EF")]
    [InlineData("Gray_background", "#F1F1EF")]
    [InlineData("brown_background", "#F4EEEE")]
    [InlineData("orange_background", "#FBECDD")]
    [InlineData("yellow_background", "#FBF3DB")]
    [InlineData("green_background", "#EDF3EC")]
    [InlineData("blue_background", "#E7F3F8")]
    [InlineData("purple_background", "#F6F3F9")]
    [InlineData("pink_background", "#F9F0F5")]
    [InlineData("red_background", "#FDEBEC")]
    public void ToBackground_BackgroundVariants_ReturnsCorrectHex(string color, string expected)
    {
        var result = NotionColorPalette.ToBackground(color);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("gray", "#F1F1EF")]
    [InlineData("brown", "#F4EEEE")]
    [InlineData("orange", "#FBECDD")]
    [InlineData("yellow", "#FBF3DB")]
    [InlineData("green", "#EDF3EC")]
    [InlineData("blue", "#E7F3F8")]
    [InlineData("purple", "#F6F3F9")]
    [InlineData("pink", "#F9F0F5")]
    [InlineData("red", "#FDEBEC")]
    public void ToBackground_PlainColorNames_ReturnsBackgroundHex(string color, string expected)
    {
        var result = NotionColorPalette.ToBackground(color);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToBackground_UnknownColor_ReturnsDefaultBg()
    {
        var result = NotionColorPalette.ToBackground("nonexistent");

        Assert.Equal("#F7F6F3", result);
    }

    [Fact]
    public void ToBackground_EmptyString_ReturnsDefaultBg()
    {
        var result = NotionColorPalette.ToBackground("");

        Assert.Equal("#F7F6F3", result);
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.Equal("#787774", NotionColorPalette.GrayFg);
        Assert.Equal("#D9730D", NotionColorPalette.OrangeFg);
        Assert.Equal("#E03E3E", NotionColorPalette.RedFg);
        Assert.Equal("#F1F1EF", NotionColorPalette.GrayBg);
        Assert.Equal("#FDEBEC", NotionColorPalette.RedBg);
        Assert.Equal("#F7F6F3", NotionColorPalette.DefaultBg);
    }
}
