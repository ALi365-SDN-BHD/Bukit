using System.Globalization;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Primitives;

/// <summary>The field and direction that order entries sharing a publish time.</summary>
internal sealed record ContentTieBreak(string Field, bool Descending)
{
    /// <summary>1 ascending; -1 descending.</summary>
    internal int Direction => Descending ? -1 : 1;

    /// <summary>
    /// True for title, which compares like the built-in term-page title order (case-insensitive text),
    /// so title asc matches that order and title desc is its exact reverse.
    /// </summary>
    internal bool IsTitle => Field.Equals("title", StringComparison.OrdinalIgnoreCase);

    /// <summary>Compares two tie-break values in this direction; missing values come last either way.</summary>
    internal int Compare(string? a, string? b)
        => IsTitle ? ContentOrdering.CompareTitles(a, b, Direction) : ContentOrdering.CompareValues(a, b, Direction);

    internal IComparer<string?> Comparer => Comparer<string?>.Create(Compare);
}

/// <summary>
/// Same-publish-time ordering shared by every content list (home, collection lists, archives,
/// feeds, llms.txt and term pages), so one site never shows the same entries in two orders.
/// </summary>
internal static class ContentOrdering
{
    /// <summary>The site-wide tie-break from content.ordering, or null when unset.</summary>
    internal static ContentTieBreak? Resolve(AppConfig? config) => Resolve(config?.Content);

    internal static ContentTieBreak? Resolve(ContentConfig? content) => Create(content?.OrderingTieBreakField, content?.OrderingTieBreakOrder);

    internal static ContentTieBreak? Create(string? field, string? order)
    {
        var trimmed = field?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return new ContentTieBreak(trimmed, string.Equals(order?.Trim(), "desc", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the value an entry is ordered by: its route URL, its title, or a content field.</summary>
    internal static string? ReadValue(ContentDocument document, string url, string field)
    {
        if (field.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // The document title, the same value term pages fall back to, not a custom field named title.
        if (field.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            return FormatValue(document.Title);
        }

        return ContentFieldReader.TryGetField(document.CustomFields, field, out var contentField) && contentField.Value is not null
            ? FormatValue(contentField.Value)
            : null;
    }

    /// <summary>Adds the tie-break after an existing publish-time order; a null tie-break leaves the order unchanged.</summary>
    internal static IOrderedEnumerable<T> ThenByTieBreak<T>(
        this IOrderedEnumerable<T> source,
        ContentTieBreak? tieBreak,
        Func<T, ContentDocument> document,
        Func<T, string> url)
        => tieBreak is null
            ? source
            : source.ThenBy(x => ReadValue(document(x), url(x), tieBreak.Field), tieBreak.Comparer);

    /// <summary>Case-insensitive title order (the built-in term-page order); missing titles come last.</summary>
    internal static int CompareTitles(string? a, string? b, int direction)
    {
        if (a is null || b is null)
        {
            return a is null ? (b is null ? 0 : 1) : -1;
        }

        var cmp = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return direction < 0 ? -cmp : cmp;
    }

    // Dates use ISO 8601 so text order is chronological; lists use their first value.
    private static string? FormatValue(object? value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            DateTimeOffset dto => dto.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            DateTime dt => (dt.Kind == DateTimeKind.Unspecified ? dt : dt.ToUniversalTime()).ToString("o", CultureInfo.InvariantCulture),
            System.Collections.IEnumerable list => list.Cast<object?>().Select(FormatValue).FirstOrDefault(x => x is not null),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    // Missing values always come last and numbers always before text, whatever the direction
    // (separate groups keep the order transitive); numbers compare by value, text by CompareNatural.
    internal static int CompareValues(string? a, string? b, int direction)
    {
        if (a is null || b is null)
        {
            return a is null ? (b is null ? 0 : 1) : -1;
        }

        var aIsNumber = TryParseNumber(a, out var x);
        var bIsNumber = TryParseNumber(b, out var y);
        if (aIsNumber != bIsNumber)
        {
            return aIsNumber ? -1 : 1;
        }

        var cmp = !aIsNumber ? CompareNatural(a, b)
            : TryCompareIntegers(a.Trim(), b.Trim(), out var exact) ? exact
            : x.CompareTo(y);
        return direction < 0 ? -cmp : cmp;
    }

    private static bool TryParseNumber(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    // Whole numbers compare by their digits, so IDs past 2^53 (where doubles lose precision) stay distinct.
    private static bool TryCompareIntegers(string a, string b, out int result)
    {
        result = 0;
        if (!IsInteger(a, out var aNegative, out var aDigits) || !IsInteger(b, out var bNegative, out var bDigits))
        {
            return false;
        }

        if (aNegative != bNegative)
        {
            result = aNegative ? -1 : 1;
            return true;
        }

        var magnitude = aDigits.Length != bDigits.Length
            ? aDigits.Length.CompareTo(bDigits.Length)
            : aDigits.SequenceCompareTo(bDigits);
        result = aNegative ? -magnitude : magnitude;
        return true;
    }

    // Digits without sign or leading zeros; "-0" and "0" both give empty digits.
    private static bool IsInteger(string text, out bool negative, out ReadOnlySpan<char> digits)
    {
        var span = text.AsSpan();
        negative = span.Length > 0 && span[0] == '-';
        if (span.Length > 0 && (span[0] == '-' || span[0] == '+'))
        {
            span = span[1..];
        }

        digits = span.TrimStart('0');
        if (span.Length == 0)
        {
            return false;
        }

        foreach (var c in span)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        negative &= digits.Length > 0;
        return true;
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
}
