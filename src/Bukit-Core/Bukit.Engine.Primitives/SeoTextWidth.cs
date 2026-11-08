namespace Bukit.Engine.Primitives;

/// <summary>
/// Approximate display width of SEO text: East Asian wide and fullwidth characters count 2,
/// everything else 1. Search results truncate by pixels, so a char count undercounts CJK text.
/// </summary>
internal static class SeoTextWidth
{
    internal const int TitleMax = 60;
    internal const int DescriptionMax = 160;

    internal static int Measure(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += IsWide(rune.Value) ? 2 : 1;
        }

        return width;
    }

    /// <summary>Cuts text to at most <paramref name="maxWidth"/> columns, ending with "…" when cut.</summary>
    internal static string Truncate(string text, int maxWidth)
    {
        if (Measure(text) <= maxWidth)
        {
            return text;
        }

        var budget = maxWidth - 1; // room for the ellipsis
        var width = 0;
        var end = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var w = IsWide(rune.Value) ? 2 : 1;
            if (width + w > budget)
            {
                break;
            }

            width += w;
            end += rune.Utf16SequenceLength;
        }

        return text[..end].TrimEnd() + "…";
    }

    private static bool IsWide(int cp) =>
        cp is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0x303E
            or >= 0x3041 and <= 0x33FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xA000 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1F64F
            or >= 0x1F900 and <= 0x1F9FF
            or >= 0x20000 and <= 0x3FFFD;
}
