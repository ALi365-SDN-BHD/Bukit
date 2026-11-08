using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Shared;

/// <summary>
/// Cleans a raw taxonomy term value before it becomes a display name and slug.
/// A value stored percent-encoded (e.g. "%e4%b8%80") would otherwise slugify to hex
/// ("e4b880"), so it is decoded when it decodes to valid UTF-8.
/// Shared by taxonomy page generation and the <c>util.term_slug</c> template function,
/// so links built in templates point at the pages taxonomy actually writes.
/// </summary>
internal static partial class TermValueNormalizer
{
    public static string Normalize(string? raw) => Normalize(raw, out _);

    public static string Normalize(string? raw, out bool decoded)
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

    /// <summary>The slug taxonomy uses for a raw term value.</summary>
    public static string ToSlug(string? raw) => SlugHelper.Slugify(Normalize(raw));

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
