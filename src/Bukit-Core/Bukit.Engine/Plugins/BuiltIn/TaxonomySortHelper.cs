using System.Globalization;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
internal static class TaxonomySortHelper
{
    /// <summary>
    /// Taxonomy settings with the term-page tie-break resolved: taxonomy.tieBreakField wins; when it is unset
    /// the field comes from content.ordering, else title. taxonomy.tieBreakOrder wins whenever it is set,
    /// so writing only tieBreakOrder: desc reverses the inherited (or title) order instead of being ignored.
    /// </summary>
    internal static TaxonomyConfig WithContentOrdering(AppConfig config)
    {
        var taxonomy = config.Taxonomy;
        if (!string.IsNullOrWhiteSpace(taxonomy.TieBreakField))
        {
            return taxonomy;
        }

        var order = string.IsNullOrWhiteSpace(taxonomy.TieBreakOrder) ? null : taxonomy.TieBreakOrder;
        if (ContentOrdering.Resolve(config) is { } siteWide)
        {
            return taxonomy with { TieBreakField = siteWide.Field, TieBreakOrder = order ?? (siteWide.Descending ? "desc" : "asc") };
        }

        return order is null ? taxonomy : taxonomy with { TieBreakField = "title" };
    }

    /// <summary>
    /// Returns the configured same-time ordering, or null for the built-in title-ascending order.
    /// title asc is that built-in order, so writing it gives the same output and cache as leaving it unset;
    /// title desc is its exact reverse.
    /// </summary>
    internal static ContentTieBreak? ResolveTieBreak(TaxonomyConfig config)
        => ContentOrdering.Create(config.TieBreakField, config.TieBreakOrder) is { } tieBreak &&
           !(tieBreak.IsTitle && !tieBreak.Descending)
            ? tieBreak
            : null;

    internal static string? ReadTieBreakValue(ContentDocument item, string url, string field)
        => ContentOrdering.ReadValue(item, url, field);

    internal static int ComparePages(TaxonomyPage a, TaxonomyPage b) => ComparePages(a, b, tieBreak: null);

    /// <param name="tieBreakDirection">0 ignores TieBreakValue; 1 sorts it ascending; -1 descending.</param>
    internal static int ComparePages(TaxonomyPage a, TaxonomyPage b, int tieBreakDirection)
        => ComparePages(a, b, tieBreakDirection == 0 ? null : new ContentTieBreak("value", tieBreakDirection < 0));

    /// <summary>Term-page order: pinned entries first, then newest, then <see cref="CompareSameTime"/>.</summary>
    internal static int ComparePages(TaxonomyPage a, TaxonomyPage b, ContentTieBreak? tieBreak)
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
        return publishAtCmp != 0 ? publishAtCmp : CompareSameTime(a, b, tieBreak);
    }

    /// <summary>
    /// Orders entries with the same publish time: the tie-break (title compares the page titles), then
    /// title, then URL. Term pages and term feeds both use this, so they always agree.
    /// </summary>
    internal static int CompareSameTime(TaxonomyPage a, TaxonomyPage b, ContentTieBreak? tieBreak)
    {
        if (tieBreak is not null)
        {
            var tieCmp = tieBreak.IsTitle
                ? ContentOrdering.CompareTitles(a.Title, b.Title, tieBreak.Direction)
                : tieBreak.Compare(a.TieBreakValue, b.TieBreakValue);
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

    internal static int CompareTieBreakValues(string? a, string? b, int direction)
        => ContentOrdering.CompareValues(a, b, direction);

    internal static int CompareNatural(string? a, string? b) => ContentOrdering.CompareNatural(a, b);

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
