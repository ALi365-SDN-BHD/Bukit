namespace Bukit.WechatSyncing;

internal static class NotionColorPalette
{
    internal const string GrayFg = "#787774";
    internal const string BrownFg = "#64473A";
    internal const string OrangeFg = "#D9730D";
    internal const string YellowFg = "#DFAB01";
    internal const string GreenFg = "#0F7B6C";
    internal const string BlueFg = "#0B6E99";
    internal const string PurpleFg = "#6940A5";
    internal const string PinkFg = "#AD1A72";
    internal const string RedFg = "#E03E3E";

    internal const string GrayBg = "#F1F1EF";
    internal const string BrownBg = "#F4EEEE";
    internal const string OrangeBg = "#FBECDD";
    internal const string YellowBg = "#FBF3DB";
    internal const string GreenBg = "#EDF3EC";
    internal const string BlueBg = "#E7F3F8";
    internal const string PurpleBg = "#F6F3F9";
    internal const string PinkBg = "#F9F0F5";
    internal const string RedBg = "#FDEBEC";
    internal const string DefaultBg = "#F7F6F3";

    internal static string ToForeground(string notionColor)
        => notionColor.ToLowerInvariant() switch
        {
            "gray" => GrayFg,
            "brown" => BrownFg,
            "orange" => OrangeFg,
            "yellow" => YellowFg,
            "green" => GreenFg,
            "blue" => BlueFg,
            "purple" => PurpleFg,
            "pink" => PinkFg,
            "red" => RedFg,
            _ => "inherit"
        };

    internal static string ToBackground(string notionColor)
        => notionColor.ToLowerInvariant() switch
        {
            "gray_background" or "gray" => GrayBg,
            "brown_background" or "brown" => BrownBg,
            "orange_background" or "orange" => OrangeBg,
            "yellow_background" or "yellow" => YellowBg,
            "green_background" or "green" => GreenBg,
            "blue_background" or "blue" => BlueBg,
            "purple_background" or "purple" => PurpleBg,
            "pink_background" or "pink" => PinkBg,
            "red_background" or "red" => RedBg,
            _ => DefaultBg
        };
}
