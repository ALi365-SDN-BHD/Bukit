using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Engine.Plugins.BuiltIn;

/// <summary>
/// Cleans a raw taxonomy term value before it becomes a display name and slug.
/// A value stored percent-encoded (e.g. "%e4%b8%80") would otherwise slugify to hex
/// ("e4b880"), so it is decoded when it decodes to valid UTF-8.
/// </summary>
internal static partial class TaxonomyTermValueNormalizer
{
    internal static string Normalize(string? raw, out bool decoded)
    {
        decoded = false;
        var value = CollapseWhitespace(raw ?? string.Empty);
        if (value.Length == 0 || !PercentEscape().IsMatch(value))
        {
            return value;
        }

        string unescaped;
        try
        {
            unescaped = Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }

        // Invalid UTF-8 either stays escaped or yields U+FFFD; keep the original then.
        if (unescaped == value || unescaped.Contains('�') || PercentEscape().IsMatch(unescaped))
        {
            return value;
        }

        decoded = true;
        return CollapseWhitespace(unescaped);
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    [GeneratedRegex("%[0-9A-Fa-f]{2}", RegexOptions.CultureInvariant)]
    private static partial Regex PercentEscape();
}
