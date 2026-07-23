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
    private sealed class TaxonomyIndexCacheEntry
    {
        internal required TaxonomyConfig Config { get; init; }
        internal required Dictionary<string, Dictionary<string, TaxonomyTerm>> Indexes { get; init; }
    }

    internal static Dictionary<string, TaxonomyTerm> GetOrBuildIndex(
        BuildContext context,
        string key,
        IReadOnlyList<string> itemFields,
        TaxonomyConfig config)
    {
        Dictionary<string, Dictionary<string, TaxonomyTerm>> cache;
        if (context.Data.TryGetValue(TaxonomyPlugin.IndexCacheKey, out var cacheObj)
            && cacheObj is TaxonomyIndexCacheEntry existingCache
            && ReferenceEquals(existingCache.Config, config))
        {
            cache = existingCache.Indexes;
        }
        else
        {
            cache = new Dictionary<string, Dictionary<string, TaxonomyTerm>>(StringComparer.OrdinalIgnoreCase);
            context.Data[TaxonomyPlugin.IndexCacheKey] = new TaxonomyIndexCacheEntry
            {
                Config = config,
                Indexes = cache
            };
        }

        var indexKey = $"{key}|{string.Join(",", itemFields)}";
        if (!cache.TryGetValue(indexKey, out var terms))
        {
            terms = BuildIndexCore(context.RoutedDocuments, context.ContentGraph, key, itemFields, config);
            cache[indexKey] = terms;
        }

        return terms;
    }

    internal static Dictionary<string, TaxonomyTerm> BuildIndexCore(
        IReadOnlyList<RoutedContentDocument> routed,
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

        foreach (var routedDocument in routed)
        {
            var item = routedDocument.Document;
            var route = routedDocument.Route;
            recordsById.TryGetValue(item.Id, out var record);
            var values = ResolveTaxonomyValues(item, record, key);
            if (values is null || values.Count == 0)
            {
                continue;
            }

            var summary = record?.Presentation.Summary ?? ContentFieldReader.GetSummary(item);
            var extra = ExtractExtraFields(item, itemFields, summary);
            var sourceKey = GetSourceKey(item);
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

                term.Pages.Add(new TaxonomyPage(item.Id, item.Title, route.Url, item.PublishAt, summary, extra, isPinned, pinOrder));
            }
        }

        foreach (var term in terms.Values)
        {
            term.Pages.Sort(TaxonomySortHelper.ComparePages);
        }

        return terms;
    }

    private static IReadOnlyList<string>? ResolveTaxonomyValues(ContentDocument item, ContentRecord? record, string key)
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

    internal static IReadOnlyList<string>? GetStringList(ContentDocument item, string key)
    {
        return ContentFieldReader.GetTextList(item.CustomFields, key);
    }

    internal static IReadOnlyDictionary<string, object>? ExtractExtraFields(ContentDocument item, IReadOnlyList<string> itemFields, string? summary)
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
            else if (key.Equals("summary", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(summary))
            {
                dict["summary"] = summary;
            }
        }

        return dict.Count == 0 ? null : dict;
    }

    internal static bool TryGetItemValue(ContentDocument item, string key, out object? value)
    {
        value = null;

        if (ContentFieldReader.TryGetField(item.CustomFields, key, out var field) && field.Value is not null)
        {
            value = field.Value;
            return true;
        }

        return false;
    }

    internal static string? GetSourceKey(ContentDocument item)
    {
        return ContentFieldReader.GetText(item.CustomFields, "sourceKey");
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
