namespace Bukit.Content.Notion;

public static class NotionColorPalette
{
    public const string GrayFg = Bukit.Notion.Rendering.NotionColorPalette.GrayFg;
    public const string BrownFg = Bukit.Notion.Rendering.NotionColorPalette.BrownFg;
    public const string OrangeFg = Bukit.Notion.Rendering.NotionColorPalette.OrangeFg;
    public const string YellowFg = Bukit.Notion.Rendering.NotionColorPalette.YellowFg;
    public const string GreenFg = Bukit.Notion.Rendering.NotionColorPalette.GreenFg;
    public const string BlueFg = Bukit.Notion.Rendering.NotionColorPalette.BlueFg;
    public const string PurpleFg = Bukit.Notion.Rendering.NotionColorPalette.PurpleFg;
    public const string PinkFg = Bukit.Notion.Rendering.NotionColorPalette.PinkFg;
    public const string RedFg = Bukit.Notion.Rendering.NotionColorPalette.RedFg;
    public const string GrayBg = Bukit.Notion.Rendering.NotionColorPalette.GrayBg;
    public const string BrownBg = Bukit.Notion.Rendering.NotionColorPalette.BrownBg;
    public const string OrangeBg = Bukit.Notion.Rendering.NotionColorPalette.OrangeBg;
    public const string YellowBg = Bukit.Notion.Rendering.NotionColorPalette.YellowBg;
    public const string GreenBg = Bukit.Notion.Rendering.NotionColorPalette.GreenBg;
    public const string BlueBg = Bukit.Notion.Rendering.NotionColorPalette.BlueBg;
    public const string PurpleBg = Bukit.Notion.Rendering.NotionColorPalette.PurpleBg;
    public const string PinkBg = Bukit.Notion.Rendering.NotionColorPalette.PinkBg;
    public const string RedBg = Bukit.Notion.Rendering.NotionColorPalette.RedBg;
    public const string DefaultBg = Bukit.Notion.Rendering.NotionColorPalette.DefaultBg;

    public static string ToForeground(string notionColor)
        => Bukit.Notion.Rendering.NotionColorPalette.ToForeground(notionColor);

    public static string ToBackground(string notionColor)
        => Bukit.Notion.Rendering.NotionColorPalette.ToBackground(notionColor);
}
