using System.Globalization;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

/// <summary>The field and direction that order entries sharing a publish time.</summary>
internal sealed record ContentTieBreak(string Field, bool Descending)
{
    /// <summary>1 ascending; -1 descending.</summary>
    internal int Direction => Descending ? -1 : 1;
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

        if (ContentFieldReader.TryGetField(document.CustomFields, field, out var contentField) && contentField.Value is not null)
        {
            return FormatValue(contentField.Value);
        }

        return field.Equals("title", StringComparison.OrdinalIgnoreCase) ? FormatValue(document.Title) : null;
    }

    /// <summary>Adds the tie-break after an existing publish-time order; a null tie-break leaves the order unchanged.</summary>
    internal static IOrderedEnumerable<T> ThenByTieBreak<T>(
        this IOrderedEnumerable<T> source,
        ContentTieBreak? tieBreak,
        Func<T, ContentDocument> document,
        Func<T, string> url)
        => tieBreak is null
            ? source
            : source.ThenBy(x => ReadValue(document(x), url(x), tieBreak.Field), Comparer(tieBreak.Direction));

    internal static IComparer<string?> Comparer(int direction)
        => Comparer<string?>.Create((a, b) => CompareValues(a, b, direction));

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

        var cmp = aIsNumber ? x.CompareTo(y) : CompareNatural(a, b);
        return direction < 0 ? -cmp : cmp;
    }

    private static bool TryParseNumber(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

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
