using System.Collections;
using System.Globalization;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.ContentLinks;
using Bukit.Engine.Primitives;

namespace Bukit.Engine.Queries;

/// <summary>
/// Computes <c>site.queries</c>: each query takes one collection's entries of the current language
/// (pages and entries without a page), in collection list order, then filters, sorts, and keeps
/// entries, distinct values or groups. Entries have the shape of a link entry (key, title, slug,
/// url, collection and the selected fields); url is null for an entry without a page.
/// </summary>
internal static class SiteQueryResolver
{
    private sealed record Entry(ContentDocument Document, string? Url, int Order);

    internal static IReadOnlyDictionary<string, object>? Resolve(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> pages,
        IReadOnlyList<ContentDocument> withoutPage)
    {
        if (config.Site.Queries.Count == 0)
        {
            return null;
        }

        var entries = Order(pages, withoutPage, ContentOrdering.Resolve(config));
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in config.Site.Queries)
        {
            result[query.Name] = Run(query, entries);
        }

        return result;
    }

    /// <summary>The collection list order: newest first, then content.ordering, then URL and id.</summary>
    private static Entry[] Order(
        IReadOnlyList<RoutedContentDocument> pages,
        IReadOnlyList<ContentDocument> withoutPage,
        ContentTieBreak? tieBreak)
        => [.. pages.Select(page => (page.Document, Url: (string?)page.Route.Url))
            .Concat(withoutPage.Select(document => (Document: document, Url: (string?)null)))
            .OrderByDescending(item => item.Document.PublishAt)
            .ThenByTieBreak(tieBreak, item => item.Document, item => item.Url ?? string.Empty)
            .ThenBy(item => item.Url ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.Document.Id, StringComparer.Ordinal)
            .Select((item, index) => new Entry(item.Document, item.Url, index))];

    private static List<object?> Run(SiteQueryConfig query, Entry[] entries)
    {
        var matches = entries
            .Where(entry => string.Equals(ContentFieldReader.GetCollection(entry.Document), query.From, StringComparison.OrdinalIgnoreCase))
            .Where(entry => query.Where.All(filter => Matches(entry.Document.CustomFields, filter)))
            .ToList();
        if (query.Sort.Count > 0)
        {
            matches.Sort((x, y) => Compare(x, y, query.Sort));
        }

        var limit = query.Limit ?? int.MaxValue;
        if (query.Distinct is { } distinct)
        {
            return DistinctValues(matches, distinct, limit);
        }

        if (query.Group is { } group)
        {
            return [.. group.Values.Select(value => (object?)new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = value,
                ["items"] = ToItems(matches.Where(entry => FilteredListMatcher.Matches(entry.Document.CustomFields, group.Field, "equals", [value])), query, limit)
            })];
        }

        return ToItems(matches, query, limit);
    }

    private static List<object?> ToItems(IEnumerable<Entry> entries, SiteQueryConfig query, int limit)
        => [.. entries.Take(limit).Select(entry => (object?)ContentLinkResolver.Entry(entry.Document, entry.Document.Slug, entry.Url, query.Select))];

    private static bool Matches(IReadOnlyDictionary<string, ContentField>? fields, SiteQueryFilterConfig filter)
        => filter.HasBackref is { } backref
            ? ContentFieldReader.TryGetField(fields, backref + "_count", out var count) && count.Value is long total && total > 0
            : FilteredListMatcher.Matches(fields, filter.Field!, "in", filter.Values);

    /// <summary>The field's values in entry order, list items flattened, each once (case-insensitive text).</summary>
    private static List<object?> DistinctValues(List<Entry> entries, string field, int limit)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new List<object?>();
        foreach (var entry in entries)
        {
            if (!ContentFieldReader.TryGetField(entry.Document.CustomFields, field, out var value))
            {
                continue;
            }

            foreach (var item in Scalars(value.Value))
            {
                if (values.Count < limit && Text(item) is { Length: > 0 } text && seen.Add(text))
                {
                    values.Add(item);
                }
            }
        }

        return values;
    }

    /// <summary>Applies the sort keys in order; equal entries keep the collection list order.</summary>
    private static int Compare(Entry x, Entry y, IReadOnlyList<SiteQuerySortConfig> sort)
    {
        foreach (var key in sort)
        {
            var xValue = SortValue(x, key);
            var yValue = SortValue(y, key);
            if (xValue is null || yValue is null)
            {
                // A missing value sorts last in both directions.
                if (xValue is null != yValue is null)
                {
                    return xValue is null ? 1 : -1;
                }

                continue;
            }

            var compared = CompareValues(xValue, yValue);
            if (compared != 0)
            {
                return key.Direction == "desc" ? -compared : compared;
            }
        }

        return x.Order.CompareTo(y.Order);
    }

    /// <summary>The first scalar of the field, or its position in <c>values</c>; null when missing, empty or not listed.</summary>
    private static object? SortValue(Entry entry, SiteQuerySortConfig key)
    {
        var value = ContentFieldReader.TryGetField(entry.Document.CustomFields, key.Field, out var field)
            ? Scalars(field.Value).FirstOrDefault()
            : null;
        if (Text(value) is not { Length: > 0 } text)
        {
            return null;
        }

        if (key.Values.Count == 0)
        {
            return value;
        }

        for (var i = 0; i < key.Values.Count; i++)
        {
            if (string.Equals(key.Values[i], text, StringComparison.OrdinalIgnoreCase))
            {
                return (long)i;
            }
        }

        return null;
    }

    private static int CompareValues(object x, object y)
    {
        if (Number(x) is { } xn && Number(y) is { } yn)
        {
            return xn.CompareTo(yn);
        }

        return (x, y) switch
        {
            (DateTimeOffset xd, DateTimeOffset yd) => xd.CompareTo(yd),
            (bool xb, bool yb) => xb.CompareTo(yb),
            _ => StringComparer.OrdinalIgnoreCase.Compare(Text(x), Text(y))
        };
    }

    private static double? Number(object? value) => value switch
    {
        long or int or short or byte or ulong or uint or ushort or sbyte or double or float or decimal
            => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        _ => null
    };

    private static IEnumerable<object?> Scalars(object? value) => value switch
    {
        null => [],
        string => [value],
        IDictionary => [],
        IEnumerable items => items.Cast<object?>().Where(item => item is not (null or IDictionary or IEnumerable) || item is string),
        _ => [value]
    };

    private static string? Text(object? value) => value switch
    {
        null => null,
        string text => text.Trim(),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
