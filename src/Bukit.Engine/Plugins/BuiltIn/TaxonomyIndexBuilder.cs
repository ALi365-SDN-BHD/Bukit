using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyIndexBuilder
{
    internal static Dictionary<string, TaxonomyTerm> GetOrBuildIndex(
        BuildContext context,
        string key,
        IReadOnlyList<string> itemFields)
    {
        Dictionary<string, Dictionary<string, TaxonomyTerm>> cache;
        if (context.Data.TryGetValue(TaxonomyPlugin.IndexCacheKey, out var cacheObj)
            && cacheObj is Dictionary<string, Dictionary<string, TaxonomyTerm>> existingCache)
        {
            cache = existingCache;
        }
        else
        {
            cache = new Dictionary<string, Dictionary<string, TaxonomyTerm>>(StringComparer.OrdinalIgnoreCase);
            context.Data[TaxonomyPlugin.IndexCacheKey] = cache;
        }

        var indexKey = $"{key}|{string.Join(",", itemFields)}";
        if (!cache.TryGetValue(indexKey, out var terms))
        {
            terms = BuildIndexCore(context.RoutedDocuments, key, itemFields, context.Config.Taxonomy);
            cache[indexKey] = terms;
        }

        return terms;
    }

    internal static Dictionary<string, TaxonomyTerm> BuildIndexCore(
        IReadOnlyList<(ContentDocument Document, RouteInfo Route)> routed,
        string key,
        IReadOnlyList<string> itemFields,
        TaxonomyConfig config)
    {
        TaxonomyPlugin.BuildIndexCountForTestsScope.Value++;
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);

        foreach (var (document, route) in routed)
        {
            var record = document.Record;
            var values = ResolveTaxonomyValues(document, key);
            if (values is null || values.Count == 0)
            {
                continue;
            }

            var summary = record.Presentation.Summary;
            var extra = ExtractExtraFields(document, itemFields);
            var sourceKey = record.Provenance.Source;
            var pinField = ResolvePinField(config, sourceKey);
            var pinOrderField = ResolvePinOrderField(config, sourceKey);
            var isPinned = TaxonomySortHelper.TryGetPinned(document.CustomFields, pinField);
            var pinOrder = TaxonomySortHelper.TryGetPinOrder(document.CustomFields, pinOrderField);
            if (pinOrder.HasValue)
            {
                isPinned = true;
            }

            foreach (var raw in values)
            {
                var display = (raw ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(display))
                {
                    continue;
                }

                var slug = SlugHelper.Slugify(display);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                if (!terms.TryGetValue(slug, out var term))
                {
                    term = new TaxonomyTerm(display, slug);
                    terms[slug] = term;
                }

                term.Pages.Add(new TaxonomyPage(
                    record.Presentation.Title,
                    route.Url,
                    record.Lifecycle.PublishedAt,
                    summary,
                    extra,
                    isPinned,
                    pinOrder));
            }
        }

        foreach (var term in terms.Values)
        {
            term.Pages.Sort(TaxonomySortHelper.ComparePages);
        }

        return terms;
    }

    private static IReadOnlyList<string>? ResolveTaxonomyValues(ContentDocument document, string key)
    {
        var record = document.Record;
        if (key.Equals("tags", StringComparison.OrdinalIgnoreCase) &&
            record.Classification.Tags is { Count: > 0 } tags)
        {
            return tags;
        }

        if (key.Equals("categories", StringComparison.OrdinalIgnoreCase) &&
            record.Classification.Sections is { Count: > 0 } categories)
        {
            return categories;
        }

        return GetStringList(document.CustomFields, key);
    }

    internal static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null)
        {
            return null;
        }

        if (!fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        if (field.Value is string fieldText)
        {
            var fieldParts = fieldText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return fieldParts.Length == 0 ? null : fieldParts;
        }

        if (field.Value is IEnumerable<object> fieldSeq)
        {
            var fieldList = fieldSeq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            return fieldList.Count == 0 ? null : fieldList;
        }

        return null;
    }

    internal static IReadOnlyDictionary<string, object>? ExtractExtraFields(ContentDocument document, IReadOnlyList<string> itemFields)
    {
        if (itemFields.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in itemFields)
        {
            if (TryGetItemValue(document, key, out var value))
            {
                dict[key] = value!;
                continue;
            }

            if (key.Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                dict["date"] = document.Record.Lifecycle.PublishedAt.UtcDateTime.ToString("yyyy-MM-dd");
            }
        }

        return dict.Count == 0 ? null : dict;
    }

    internal static bool TryGetItemValue(ContentDocument document, string key, out object? value)
    {
        return TryGetItemValue(document.CustomFields, key, out value);
    }

    internal static bool TryGetItemValue(IReadOnlyDictionary<string, ContentField> fields, string key, out object? value)
    {
        value = null;

        if (fields.TryGetValue(key, out var field) && field.Value is not null)
        {
            value = field.Value;
            return true;
        }

        return false;
    }

    internal static string ResolvePinField(TaxonomyConfig config, string? sourceKey)
    {
        if (!string.IsNullOrWhiteSpace(sourceKey) &&
            config.PinFieldBySource is not null &&
            config.PinFieldBySource.TryGetValue(sourceKey, out var field) &&
            !string.IsNullOrWhiteSpace(field))
        {
            return field.Trim();
        }

        return string.IsNullOrWhiteSpace(config.PinField) ? "pinned" : config.PinField.Trim();
    }

    internal static string? ResolvePinOrderField(TaxonomyConfig config, string? sourceKey)
    {
        if (!string.IsNullOrWhiteSpace(sourceKey) &&
            config.PinOrderFieldBySource is not null &&
            config.PinOrderFieldBySource.TryGetValue(sourceKey, out var field) &&
            !string.IsNullOrWhiteSpace(field))
        {
            return field.Trim();
        }

        return string.IsNullOrWhiteSpace(config.PinOrderField) ? null : config.PinOrderField.Trim();
    }

    internal static void MergeEnsureTerms(BuildContext context, string kind, Dictionary<string, TaxonomyTerm> terms)
    {
        if (!context.Data.TryGetValue("taxonomy_ensure_terms", out var obj) || obj is null)
        {
            return;
        }

        if (obj is not Dictionary<string, List<Dictionary<string, object>>> map)
        {
            return;
        }

        if (!map.TryGetValue(kind, out var list) || list is null || list.Count == 0)
        {
            return;
        }

        foreach (var termObj in list)
        {
            if (termObj is null)
            {
                continue;
            }

            var title = termObj.TryGetValue("title", out var t) && t is not null ? (t.ToString() ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var slug = termObj.TryGetValue("slug", out var s) && s is not null ? (s.ToString() ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = SlugHelper.Slugify(title);
            }

            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            if (slug.Contains("..") || Path.IsPathRooted(slug) || slug.Contains('/') || slug.Contains('\\'))
            {
                slug = SlugHelper.Slugify(slug);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }
            }

            if (!terms.ContainsKey(slug))
            {
                terms[slug] = new TaxonomyTerm(title, slug);
            }
        }
    }
}
