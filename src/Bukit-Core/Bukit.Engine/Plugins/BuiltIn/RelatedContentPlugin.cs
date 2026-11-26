using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Primitives;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class RelatedContentPlugin : IBukitPlugin, IDerivePagesPlugin
{
    private readonly AppConfig _config;

    internal RelatedContentPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "related-content";
    public string Version => "1.0.0";

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
    {
        var relatedConfig = _config.Site.Related;
        if (!relatedConfig.Enabled)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var siteRules = relatedConfig.Indices is { Count: > 0 }
            ? new Rules(relatedConfig.Threshold, relatedConfig.Limit > 0 ? relatedConfig.Limit : 5, relatedConfig.Indices, SameCollection: false)
            : null;
        var collectionRules = relatedConfig.Collections.ToDictionary(
            rule => rule.Collection,
            rule => new Rules(rule.Threshold, rule.Limit, rule.Indices, SameCollection: true),
            StringComparer.OrdinalIgnoreCase);
        if (siteRules is null && collectionRules.Count == 0)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        // Candidates in collection list order, so equal scores keep that order.
        var items = CollectionRouteIndex.OrderEntries(
                context.RoutedDocuments.Where(x => !x.Document.Id.StartsWith("blog-archive-", StringComparison.OrdinalIgnoreCase)
                                                   && !x.Document.Id.StartsWith("blog-page-", StringComparison.OrdinalIgnoreCase)),
                ContentOrdering.Resolve(_config))
            .ToList();

        if (items.Count < 2)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var recordsById = context.ContentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < items.Count; i++)
        {
            var currentItem = items[i].Document;
            recordsById.TryGetValue(currentItem.Id, out var currentRecord);
            var collection = ResolveCollection(currentItem, currentRecord);
            var rules = collection is not null && collectionRules.TryGetValue(collection, out var own) ? own : siteRules;
            if (rules is null)
            {
                continue;
            }

            var related = new List<(int Score, int Order, RoutedContentDocument Item)>();
            for (var j = 0; j < items.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var otherItem = items[j].Document;
                recordsById.TryGetValue(otherItem.Id, out var otherRecord);
                if (rules.SameCollection &&
                    !string.Equals(collection, ResolveCollection(otherItem, otherRecord), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var score = CalculateScore(currentItem, otherItem, currentRecord, otherRecord, rules.Indices);
                if (score >= rules.Threshold)
                {
                    related.Add((score, j, items[j]));
                }
            }

            if (related.Count > 0)
            {
                var topN = related
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Order)
                    .Take(rules.Limit)
                    .Select(x => (object)new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["id"] = x.Item.Document.Id,
                        ["slug"] = x.Item.Document.Slug,
                        ["title"] = x.Item.Document.Title,
                        ["url"] = x.Item.Route.Url,
                        ["score"] = x.Score
                    })
                    .ToList();

                var dict = context.GetOrAdd(
                    BuildDataKeys.RelatedPages,
                    static () => new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase));
                lock (dict)
                {
                    dict[currentItem.Id] = topN;
                }
            }
        }

        return Array.Empty<RoutedContentDocument>();
    }

    /// <summary>The scoring rules for one page: the site rules, or its collection's rules (same-collection candidates only).</summary>
    private sealed record Rules(int Threshold, int Limit, IReadOnlyList<RelatedIndexConfig> Indices, bool SameCollection);

    private static int CalculateScore(
        ContentDocument a,
        ContentDocument b,
        ContentRecord? aRecord,
        ContentRecord? bRecord,
        IReadOnlyList<RelatedIndexConfig> indices)
    {
        var total = 0;

        foreach (var idx in indices)
        {
            if (idx.Field is { } field)
            {
                total += idx.Weight * CountShared(ContentFieldReader.GetTextList(a.CustomFields, field), ContentFieldReader.GetTextList(b.CustomFields, field));
                continue;
            }

            switch (idx.Name.ToLowerInvariant())
            {
                case "tags":
                    total += idx.Weight * CountShared(ResolveTags(a, aRecord), ResolveTags(b, bRecord));
                    break;
                case "categories":
                    total += idx.Weight * CountShared(ResolveCategories(a, aRecord), ResolveCategories(b, bRecord));
                    break;
                case "keywords":
                    total += idx.Weight * CountShared(ContentFieldReader.GetTextList(a.CustomFields, "keywords"), ContentFieldReader.GetTextList(b.CustomFields, "keywords"));
                    break;
                case "collection":
                    var ta = ResolveCollection(a, aRecord);
                    var tb = ResolveCollection(b, bRecord);
                    if (string.Equals(ta, tb, StringComparison.OrdinalIgnoreCase))
                    {
                        total += idx.Weight;
                    }

                    break;
                case "type":
                    var taType = ResolveType(a, aRecord);
                    var tbType = ResolveType(b, bRecord);
                    if (string.Equals(taType, tbType, StringComparison.OrdinalIgnoreCase))
                    {
                        total += idx.Weight;
                    }

                    break;
                case "date":
                    var dayDiff = Math.Abs((a.PublishAt - b.PublishAt).Days);
                    if (dayDiff <= 90)
                    {
                        total += idx.Weight * (90 - dayDiff) / 90;
                    }

                    break;
            }
        }

        return total;
    }

    private static IReadOnlyList<string>? ResolveTags(ContentDocument item, ContentRecord? record)
        => record?.Classification.Tags is { Count: > 0 } tags
            ? tags
            : ContentFieldReader.GetTextList(item.CustomFields, "tags");

    private static IReadOnlyList<string>? ResolveCategories(ContentDocument item, ContentRecord? record)
        => record?.Classification.Sections is { Count: > 0 } categories
            ? categories
            : ContentFieldReader.GetTextList(item.CustomFields, "categories");

    private static string? ResolveCollection(ContentDocument item, ContentRecord? record)
        => string.IsNullOrWhiteSpace(record?.Classification.Collection)
            ? ContentFieldReader.GetText(item.CustomFields, "collection")
            : record.Classification.Collection;

    private static string? ResolveType(ContentDocument item, ContentRecord? record)
        => string.IsNullOrWhiteSpace(record?.Classification.Type)
            ? ContentFieldReader.GetText(item.CustomFields, "type")
            : record.Classification.Type;

    private static int CountShared(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        if (a is null || b is null || a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var set = new HashSet<string>(b, StringComparer.OrdinalIgnoreCase);
        return a.Count(x => set.Contains(x));
    }
}
