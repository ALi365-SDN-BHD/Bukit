using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class RelatedContentPlugin : IBukitPlugin, IDerivePagesPlugin
{
    public string Name => "related-content";
    public string Version => "1.0.0";

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var relatedConfig = context.Config.Site.Related;
        if (!relatedConfig.Enabled)
        {
            return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
        }

        var indices = relatedConfig.Indices;
        if (indices is null || indices.Count == 0)
        {
            return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
        }

        var documents = GetDocuments(context)
            .Where(x => !x.Document.Record.Identity.Id.StartsWith("blog-archive-", StringComparison.OrdinalIgnoreCase)
                        && !x.Document.Record.Identity.Id.StartsWith("blog-page-", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (documents.Count < 2)
        {
            return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
        }

        var threshold = relatedConfig.Threshold;
        var limit = relatedConfig.Limit > 0 ? relatedConfig.Limit : 5;
        for (var i = 0; i < documents.Count; i++)
        {
            var (currentDocument, _) = documents[i];
            var related = new List<object>();

            for (var j = 0; j < documents.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var (otherDocument, otherRoute) = documents[j];
                var score = CalculateScore(currentDocument, otherDocument, indices);
                if (score >= threshold)
                {
                    related.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["title"] = otherDocument.Record.Presentation.Title,
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

                    dict[currentDocument.Record.Identity.Id] = topN;
                }
            }
        }

        return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
    }

    private static int CalculateScore(ContentDocument a, ContentDocument b, IReadOnlyList<RelatedIndexConfig> indices)
    {
        var total = 0;

        foreach (var idx in indices)
        {
            switch (idx.Name.ToLowerInvariant())
            {
                case "tags":
                    total += idx.Weight * CountShared(ResolveTags(a), ResolveTags(b));
                    break;
                case "categories":
                    total += idx.Weight * CountShared(ResolveCategories(a), ResolveCategories(b));
                    break;
                case "keywords":
                    total += idx.Weight * CountShared(GetStringList(a.CustomFields, "keywords"), GetStringList(b.CustomFields, "keywords"));
                    break;
                case "collection":
                    var ta = a.Record.Classification.Collection;
                    var tb = b.Record.Classification.Collection;
                    if (string.Equals(ta, tb, StringComparison.OrdinalIgnoreCase))
                    {
                        total += idx.Weight;
                    }

                    break;
                case "type":
                    var taType = a.Record.Classification.Type;
                    var tbType = b.Record.Classification.Type;
                    if (string.Equals(taType, tbType, StringComparison.OrdinalIgnoreCase))
                    {
                        total += idx.Weight;
                    }

                    break;
                case "date":
                    var dayDiff = Math.Abs((a.Record.Lifecycle.PublishedAt - b.Record.Lifecycle.PublishedAt).Days);
                    if (dayDiff <= 90)
                    {
                        total += idx.Weight * (90 - dayDiff) / 90;
                    }

                    break;
            }
        }

        return total;
    }

    private static IReadOnlyList<string>? ResolveTags(ContentDocument document)
        => document.Record.Classification.Tags is { Count: > 0 } tags
            ? tags
            : GetStringList(document.CustomFields, "tags");

    private static IReadOnlyList<string>? ResolveCategories(ContentDocument document)
        => document.Record.Classification.Sections is { Count: > 0 } categories
            ? categories
            : GetStringList(document.CustomFields, "categories");

    private static IReadOnlyList<(ContentDocument Document, RouteInfo Route)> GetDocuments(BuildContext context)
    {
        if (context.RoutedDocuments.Count > 0)
        {
            return context.RoutedDocuments;
        }

        var recordsById = context.ContentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        return context.Routed
            .Select(x =>
            {
                var record = recordsById.TryGetValue(x.Item.Id, out var existing)
                    ? existing
                    : CanonicalContentGraphBuilder.ToRecord(x.Item);
                var document = new ContentDocument(
                    record,
                    new ContentBodyRef(x.Item.ContentHtml, x.Item.BodyKey, null, null),
                    new ContentRoutePolicy(null, null, null, null, record.Classification.Collection),
                    new ContentPublishPolicy(false, false, false, false, false, false, false),
                    x.Item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
                    []);
                return (document, x.Route);
            })
            .ToList();
    }

    private static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || !fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        return field.Value switch
        {
            string text => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            IEnumerable<string> strings => strings.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            IEnumerable<object> objects => objects.Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            _ => null
        };
    }

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
