using System.Globalization;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
internal static class TaxonomySortHelper
{
    internal sealed record TieBreak(string Field, bool Descending);

    /// <summary>Returns the configured same-time ordering, or null for the default (title).</summary>
    internal static TieBreak? ResolveTieBreak(TaxonomyConfig config)
    {
        var field = config.TieBreakField?.Trim();
        if (string.IsNullOrEmpty(field) || field.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var descending = string.Equals(config.TieBreakOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return new TieBreak(field, descending);
    }

    internal static string? ReadTieBreakValue(ContentDocument item, string url, string field)
    {
        if (field.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (!TaxonomyIndexBuilder.TryGetItemValue(item, field, out var value) || value is null)
        {
            return null;
        }

        var text = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    internal static int ComparePages(TaxonomyPage a, TaxonomyPage b) => ComparePages(a, b, 0);

    /// <param name="tieBreakDirection">0 ignores TieBreakValue; 1 sorts it ascending; -1 descending.</param>
    internal static int ComparePages(TaxonomyPage a, TaxonomyPage b, int tieBreakDirection)
    {
        if (a.IsPinned && !b.IsPinned)
        {
            return -1;
        }

        if (!a.IsPinned && b.IsPinned)
        {
            return 1;
        }

        if (a.IsPinned && b.IsPinned)
        {
            var cmp = ComparePinOrder(a.PinOrder, b.PinOrder);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        var publishAtCmp = b.PublishAt.CompareTo(a.PublishAt);
        if (publishAtCmp != 0)
        {
            return publishAtCmp;
        }

        if (tieBreakDirection != 0)
        {
            var tieCmp = CompareTieBreakValues(a.TieBreakValue, b.TieBreakValue, tieBreakDirection);
            if (tieCmp != 0)
            {
                return tieCmp;
            }
        }

        var titleCmp = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        if (titleCmp != 0)
        {
            return titleCmp;
        }

        return string.Compare(a.Url, b.Url, StringComparison.OrdinalIgnoreCase);
    }

    // Missing values always come last; numbers compare by value, text by CompareNatural.
    internal static int CompareTieBreakValues(string? a, string? b, int direction)
    {
        if (a is null || b is null)
        {
            return a is null ? (b is null ? 0 : 1) : -1;
        }

        var cmp = double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                  double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            ? x.CompareTo(y)
            : CompareNatural(a, b);
        return direction < 0 ? -cmp : cmp;
    }

    // Compares strings with digit runs ordered by numeric value, so /999/ sorts before /1000/.
    internal static int CompareNatural(string? a, string? b)
    {
        a ??= string.Empty;
        b ??= string.Empty;
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                var startA = i;
                var startB = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                var numA = a.AsSpan(startA, i - startA).TrimStart('0');
                var numB = b.AsSpan(startB, j - startB).TrimStart('0');
                var cmp = numA.Length != numB.Length ? numA.Length.CompareTo(numB.Length) : numA.SequenceCompareTo(numB);
                if (cmp != 0)
                {
                    return cmp;
                }

                continue;
            }

            var charCmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
            if (charCmp != 0)
            {
                return charCmp;
            }

            i++;
            j++;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    internal static int ComparePinOrder(int? a, int? b)
    {
        if (a.HasValue && !b.HasValue)
        {
            return -1;
        }

        if (!a.HasValue && b.HasValue)
        {
            return 1;
        }

        if (a.HasValue && b.HasValue)
        {
            return a.Value.CompareTo(b.Value);
        }

        return 0;
    }

    internal static bool TryGetPinned(ContentDocument item, string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return false;
        }

        if (!TaxonomyIndexBuilder.TryGetItemValue(item, field, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            decimal m => m != 0,
            string s => ParseBoolLike(s),
            _ => ParseBoolLike(value.ToString() ?? string.Empty)
        };
    }

    internal static int? TryGetPinOrder(ContentDocument item, string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        if (!TaxonomyIndexBuilder.TryGetItemValue(item, field, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => l is > int.MaxValue or < int.MinValue ? null : (int)l,
            double d => double.IsNaN(d) || double.IsInfinity(d) ? null : (int)Math.Round(d),
            decimal m => m is > int.MaxValue or < int.MinValue ? null : (int)m,
            string s => int.TryParse(s.Trim(), out var i) ? i : null,
            _ => int.TryParse(value.ToString(), out var i) ? i : null
        };
    }

    internal static bool ParseBoolLike(string raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return false;
        }

        if (bool.TryParse(s, out var b))
        {
            return b;
        }

        if (s.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (s.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }
}
