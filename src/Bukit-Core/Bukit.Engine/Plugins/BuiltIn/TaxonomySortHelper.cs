using System.Globalization;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
internal static class TaxonomySortHelper
{
    internal sealed record TieBreak(string Field, bool Descending);

    /// <summary>
    /// Taxonomy settings with the site-wide content.ordering tie-break filled in when
    /// taxonomy.tieBreakField is unset, so term pages order same-time entries like every other list.
    /// </summary>
    internal static TaxonomyConfig WithContentOrdering(AppConfig config)
    {
        var taxonomy = config.Taxonomy;
        if (!string.IsNullOrWhiteSpace(taxonomy.TieBreakField) || ContentOrdering.Resolve(config) is not { } siteWide)
        {
            return taxonomy;
        }

        return taxonomy with { TieBreakField = siteWide.Field, TieBreakOrder = siteWide.Descending ? "desc" : "asc" };
    }

    /// <summary>Returns the configured same-time ordering, or null for the default (title).</summary>
    internal static TieBreak? ResolveTieBreak(TaxonomyConfig config)
    {
        if (ContentOrdering.Create(config.TieBreakField, config.TieBreakOrder) is not { } tieBreak ||
            tieBreak.Field.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new TieBreak(tieBreak.Field, tieBreak.Descending);
    }

    /// <summary>0 when no tie-break is configured; 1 ascending; -1 descending.</summary>
    internal static int ResolveDirection(TaxonomyConfig config)
        => ResolveTieBreak(config) is { } tieBreak ? (tieBreak.Descending ? -1 : 1) : 0;

    internal static string? ReadTieBreakValue(ContentDocument item, string url, string field)
        => ContentOrdering.ReadValue(item, url, field);

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
