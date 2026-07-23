using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class RelatedContentPlugin : IBukitPlugin, IDerivePagesPlugin
{
    public string Name => "related-content";
    public string Version => "1.0.0";

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
    {
        var relatedConfig = context.Config.Site.Related;
        if (!relatedConfig.Enabled)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var indices = relatedConfig.Indices;
        if (indices is null || indices.Count == 0)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var items = context.RoutedDocuments
            .Where(x => !x.Document.Id.StartsWith("blog-archive-", StringComparison.OrdinalIgnoreCase)
                        && !x.Document.Id.StartsWith("blog-page-", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (items.Count < 2)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var threshold = relatedConfig.Threshold;
        var limit = relatedConfig.Limit > 0 ? relatedConfig.Limit : 5;
        var recordsById = context.ContentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < items.Count; i++)
        {
            var currentItem = items[i].Document;
            var related = new List<object>();
            recordsById.TryGetValue(currentItem.Id, out var currentRecord);

            for (var j = 0; j < items.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var otherItem = items[j].Document;
                var otherRoute = items[j].Route;
                recordsById.TryGetValue(otherItem.Id, out var otherRecord);
                var score = CalculateScore(currentItem, otherItem, currentRecord, otherRecord, indices);
                if (score >= threshold)
                {
                    related.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["title"] = otherItem.Title,
                        ["url"] = otherRoute.Url,
                        ["score"] = score
                    });
                }
            }

            if (related.Count > 0)
            {
                related.Sort((a, b) =>
                {
                    var sa = (int)((Dictionary<string, object>)a)["score"];
                    var sb = (int)((Dictionary<string, object>)b)["score"];
                    return sb.CompareTo(sa);
                });

                var topN = related.Take(limit).ToList();

                lock (context.Data)
                {
                    if (!context.Data.TryGetValue("__related_pages", out var dictObj) || dictObj is not Dictionary<string, List<object>> dict)
                    {
                        dict = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
                        context.Data["__related_pages"] = dict;
                    }

                    dict[currentItem.Id] = topN;
                }
            }
        }

        return Array.Empty<RoutedContentDocument>();
    }

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
