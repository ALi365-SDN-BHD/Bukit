using NotionColorPalette = Bukit.Notion.Rendering.NotionColorPalette;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class BlockRendererMediaAndContainerTests
{
    [Fact]
    public void NotionColorPalette_MapsForegroundBackgroundAndFallbacks()
    {
        Assert.Equal(NotionColorPalette.GrayFg, NotionColorPalette.ToForeground("gray"));
        Assert.Equal(NotionColorPalette.BrownFg, NotionColorPalette.ToForeground("brown"));
        Assert.Equal(NotionColorPalette.OrangeFg, NotionColorPalette.ToForeground("orange"));
        Assert.Equal(NotionColorPalette.YellowFg, NotionColorPalette.ToForeground("yellow"));
        Assert.Equal(NotionColorPalette.GreenFg, NotionColorPalette.ToForeground("green"));
        Assert.Equal(NotionColorPalette.BlueFg, NotionColorPalette.ToForeground("blue"));
        Assert.Equal(NotionColorPalette.PurpleFg, NotionColorPalette.ToForeground("purple"));
        Assert.Equal(NotionColorPalette.PinkFg, NotionColorPalette.ToForeground("pink"));
        Assert.Equal(NotionColorPalette.RedFg, NotionColorPalette.ToForeground("red"));
        Assert.Equal("inherit", NotionColorPalette.ToForeground("unknown"));

        Assert.Equal(NotionColorPalette.GrayBg, NotionColorPalette.ToBackground("gray_background"));
        Assert.Equal(NotionColorPalette.BrownBg, NotionColorPalette.ToBackground("brown"));
        Assert.Equal(NotionColorPalette.OrangeBg, NotionColorPalette.ToBackground("orange_background"));
        Assert.Equal(NotionColorPalette.YellowBg, NotionColorPalette.ToBackground("yellow"));
        Assert.Equal(NotionColorPalette.GreenBg, NotionColorPalette.ToBackground("green_background"));
        Assert.Equal(NotionColorPalette.BlueBg, NotionColorPalette.ToBackground("blue"));
        Assert.Equal(NotionColorPalette.PurpleBg, NotionColorPalette.ToBackground("purple_background"));
        Assert.Equal(NotionColorPalette.PinkBg, NotionColorPalette.ToBackground("pink"));
        Assert.Equal(NotionColorPalette.RedBg, NotionColorPalette.ToBackground("red_background"));
        Assert.Equal(NotionColorPalette.DefaultBg, NotionColorPalette.ToBackground("unknown"));
    }
}
