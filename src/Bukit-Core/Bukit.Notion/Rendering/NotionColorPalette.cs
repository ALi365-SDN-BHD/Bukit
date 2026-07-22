namespace Bukit.Notion.Rendering;

/// <summary>
/// Shared Notion color palette constants. All foreground and background hex values
/// are aligned with the official Notion rendering (light theme) so that
/// <see cref="NotionRichTextRenderer"/>, block renderers, CSS, and the WeChat
/// <c>ContentProcessor</c> produce identical colours.
/// </summary>
public static class NotionColorPalette
{
    // ── Foreground ──────────────────────────────────────────────────────

    public const string GrayFg = "#787774";
    public const string BrownFg = "#64473A";
    public const string OrangeFg = "#D9730D";
    public const string YellowFg = "#DFAB01";
    public const string GreenFg = "#0F7B6C";
    public const string BlueFg = "#0B6E99";
    public const string PurpleFg = "#6940A5";
    public const string PinkFg = "#AD1A72";
    public const string RedFg = "#E03E3E";

    // ── Background ─────────────────────────────────────────────────────

    public const string GrayBg = "#F1F1EF";
    public const string BrownBg = "#F4EEEE";
    public const string OrangeBg = "#FBECDD";
    public const string YellowBg = "#FBF3DB";
    public const string GreenBg = "#EDF3EC";
    public const string BlueBg = "#E7F3F8";
    public const string PurpleBg = "#F6F3F9";
    public const string PinkBg = "#F9F0F5";
    public const string RedBg = "#FDEBEC";

    public const string DefaultBg = "#F7F6F3";

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a Notion color name (e.g. "blue") to its foreground CSS hex value.
    /// Returns "inherit" for unknown names.
    /// </summary>
    public static string ToForeground(string notionColor) =>
        notionColor.ToLowerInvariant() switch
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

    /// <summary>
    /// Maps a Notion color name or background variant (e.g. "blue_background" or "blue")
    /// to its background CSS hex value.
    /// Returns <see cref="DefaultBg"/> for unknown names.
    /// </summary>
    public static string ToBackground(string notionColor) =>
        notionColor.ToLowerInvariant() switch
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
