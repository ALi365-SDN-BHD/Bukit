using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
internal static class TaxonomySortHelper
{
    internal static int ComparePages(TaxonomyPage a, TaxonomyPage b)
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

        var titleCmp = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        if (titleCmp != 0)
        {
            return titleCmp;
        }

        return string.Compare(a.Url, b.Url, StringComparison.OrdinalIgnoreCase);
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
