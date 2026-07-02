using System.Globalization;
using System.Text;

namespace Bukit.Shared;

public static class SlugHelper
{
    public static string Slugify(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var normalized = NormalizeLatin(trimmed.ToLowerInvariant());

        var sb = new StringBuilder(normalized.Length);
        var dash = false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                dash = false;
                continue;
            }

            if (ch is ' ' or '-' or '_' or '.')
            {
                if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }
        }

        return sb.ToString().Trim('-');
    }

    private static string NormalizeLatin(string text)
    {
        var sb = new StringBuilder(text.Length);

        foreach (var ch in text.Normalize(NormalizationForm.FormD))
        {
            var cat = char.GetUnicodeCategory(ch);
            if (cat is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch <= 127)
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append(TransliterateLatin(ch));
            }
        }

        return sb.ToString();
    }

    private static string TransliterateLatin(char ch)
    {
        return ch switch
        {
            '\u00df' => "ss",
            '\u00e6' or '\u00c6' => "ae",
            '\u0153' or '\u0152' => "oe",
            '\u00f8' or '\u00d8' => "o",
            _ => ch.ToString()
        };
    }
}
