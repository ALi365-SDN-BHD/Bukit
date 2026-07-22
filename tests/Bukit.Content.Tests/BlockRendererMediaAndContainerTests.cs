using System.Text.Json;
using Bukit.Content.Notion.BlockRenderers;
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

    [Fact]
    public void NotionBlockHelpers_CoverTextColorFileAndVideoUrlBranches()
    {
        using var nonArray = JsonDocument.Parse("{}");
        using var colorDoc = JsonDocument.Parse("""{"color":"orange_background"}""");
        using var defaultColorDoc = JsonDocument.Parse("""{"color":"default"}""");
        using var externalFile = JsonDocument.Parse("""{"type":"external","external":{"url":"https://cdn.example.com/a.png"}}""");
        using var internalFile = JsonDocument.Parse("""{"type":"file","file":{"url":"https://cdn.example.com/b.png"}}""");
        using var unsupportedFile = JsonDocument.Parse("""{"type":"emoji","emoji":"x"}""");

        Assert.Equal(string.Empty, NotionBlockHelpers.ExtractPlainText(nonArray.RootElement));
        Assert.Equal(" class=\"notion-orange_background\"", NotionBlockHelpers.GetBlockColorClass(colorDoc.RootElement));
        Assert.Equal(string.Empty, NotionBlockHelpers.GetBlockColorClass(defaultColorDoc.RootElement));
        Assert.Equal(NotionColorPalette.BlueBg, NotionBlockHelpers.NotionBlockColorToCssBackground("blue"));
        Assert.Equal("https://cdn.example.com/a.png", NotionBlockHelpers.ExtractFileUrl(externalFile.RootElement));
        Assert.Equal("https://cdn.example.com/b.png", NotionBlockHelpers.ExtractFileUrl(internalFile.RootElement));
        Assert.Null(NotionBlockHelpers.ExtractFileUrl(unsupportedFile.RootElement));

        Assert.True(NotionBlockHelpers.IsYouTubeUrl("https://youtu.be/abc123?t=1", out var shortEmbed));
        Assert.Equal("https://www.youtube.com/embed/abc123", shortEmbed);
        Assert.True(NotionBlockHelpers.IsYouTubeUrl("https://www.youtube.com/embed/xyz789", out var existingEmbed));
        Assert.Equal("https://www.youtube.com/embed/xyz789", existingEmbed);
        Assert.False(NotionBlockHelpers.IsYouTubeUrl("https://www.youtube.com/watch?x=1", out var missingIdEmbed));
        Assert.Equal(string.Empty, missingIdEmbed);
        Assert.False(NotionBlockHelpers.IsYouTubeUrl("https://video.example.com/watch?v=abc", out _));

        Assert.Null(NotionBlockHelpers.ExtractQueryParam("https://example.test/path", "v"));
        Assert.Null(NotionBlockHelpers.ExtractQueryParam("https://example.test/path?novalue&x=1", "novalue"));
        Assert.Equal("hello world", NotionBlockHelpers.ExtractQueryParam("https://example.test/path?v=hello%20world", "v"));
    }
}
