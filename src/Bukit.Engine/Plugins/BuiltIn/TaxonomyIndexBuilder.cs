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
            terms = BuildIndexCore(context.Routed, context.ContentGraph, key, itemFields, context.Config.Taxonomy);
            cache[indexKey] = terms;
        }

        return terms;
    }

    internal static Dictionary<string, TaxonomyTerm> BuildIndexCore(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        CanonicalContentGraph contentGraph,
        string key,
        IReadOnlyList<string> itemFields,
        TaxonomyConfig config)
    {
        TaxonomyPlugin.BuildIndexCountForTestsScope.Value++;
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);
        var recordsById = contentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (item, route) in routed)
        {
            recordsById.TryGetValue(item.Id, out var record);
            var values = ResolveTaxonomyValues(item, record, key);
            if (values is null || values.Count == 0)
            {
                continue;
            }

            var summary = record?.Presentation.Summary ?? item.GetSummary();
            var extra = ExtractExtraFields(item, itemFields);
            var sourceKey = GetSourceKey(item.Meta);
            var pinField = ResolvePinField(config, sourceKey);
            var pinOrderField = ResolvePinOrderField(config, sourceKey);
            var isPinned = TaxonomySortHelper.TryGetPinned(item, pinField);
            var pinOrder = TaxonomySortHelper.TryGetPinOrder(item, pinOrderField);
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

                term.Pages.Add(new TaxonomyPage(item.Title, route.Url, item.PublishAt, summary, extra, isPinned, pinOrder));
            }
        }

        foreach (var term in terms.Values)
        {
            term.Pages.Sort(TaxonomySortHelper.ComparePages);
        }

        return terms;
    }

    private static IReadOnlyList<string>? ResolveTaxonomyValues(ContentItem item, ContentRecord? record, string key)
    {
        if (key.Equals("tags", StringComparison.OrdinalIgnoreCase) &&
            record?.Classification.Tags is { Count: > 0 } tags)
        {
            return tags;
        }

        if (key.Equals("categories", StringComparison.OrdinalIgnoreCase) &&
            record?.Classification.Sections is { Count: > 0 } categories)
        {
            return categories;
        }

        return GetStringList(item, key);
    }

    internal static IReadOnlyList<string>? GetStringList(ContentItem item, string key)
    {
        if (item.Fields is not null)
        {
            if (item.Fields.TryGetValue(key, out var field) && field.Value is not null)
            {
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
            }
        }

        if (!item.Meta.TryGetValue(key, out var v) || v is null)
        {
            return null;
        }

        if (v is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (v is IEnumerable<object> seq)
        {
            var list = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return list.Count == 0 ? null : list;
        }

        return null;
    }

    internal static IReadOnlyDictionary<string, object>? ExtractExtraFields(ContentItem item, IReadOnlyList<string> itemFields)
    {
        if (itemFields.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in itemFields)
        {
            if (TryGetItemValue(item, key, out var value))
            {
                dict[key] = value!;
                continue;
            }

            if (key.Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                dict["date"] = item.PublishAt.UtcDateTime.ToString("yyyy-MM-dd");
            }
        }

        return dict.Count == 0 ? null : dict;
    }

    internal static bool TryGetItemValue(ContentItem item, string key, out object? value)
    {
        value = null;

        if (item.Fields is not null && item.Fields.TryGetValue(key, out var field) && field.Value is not null)
        {
            value = field.Value;
            return true;
        }

        if (item.Meta.TryGetValue(key, out var metaValue) && metaValue is not null)
        {
            if (metaValue is string s)
            {
                var trimmed = s.Trim();
                if (trimmed.Length == 0)
                {
                    return false;
                }

                value = trimmed;
                return true;
            }

            value = metaValue;
            return true;
        }

        return false;
    }

    internal static string? GetSourceKey(IReadOnlyDictionary<string, object> meta)
    {
        if (!meta.TryGetValue("sourceKey", out var obj) || obj is null)
        {
            return null;
        }

        var text = obj.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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
