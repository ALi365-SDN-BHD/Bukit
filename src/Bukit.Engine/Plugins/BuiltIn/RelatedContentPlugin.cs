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

        var items = context.Routed
            .Where(x => !x.Item.Id.StartsWith("blog-archive-", StringComparison.OrdinalIgnoreCase)
                        && !x.Item.Id.StartsWith("blog-page-", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (items.Count < 2)
        {
            return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
        }

        var threshold = relatedConfig.Threshold;
        var limit = relatedConfig.Limit > 0 ? relatedConfig.Limit : 5;

        for (var i = 0; i < items.Count; i++)
        {
            var (currentItem, currentRoute) = items[i];
            var related = new List<object>();

            for (var j = 0; j < items.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var (otherItem, otherRoute) = items[j];
                var score = CalculateScore(currentItem, otherItem, indices);
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

        return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
    }

    private static int CalculateScore(ContentItem a, ContentItem b, IReadOnlyList<RelatedIndexConfig> indices)
    {
        var total = 0;

        foreach (var idx in indices)
        {
            switch (idx.Name.ToLowerInvariant())
            {
                case "tags":
                    total += idx.Weight * CountShared(MetaHelpers.GetStringList(a.Meta, "tags"), MetaHelpers.GetStringList(b.Meta, "tags"));
                    break;
                case "categories":
                    total += idx.Weight * CountShared(MetaHelpers.GetStringList(a.Meta, "categories"), MetaHelpers.GetStringList(b.Meta, "categories"));
                    break;
                case "keywords":
                    total += idx.Weight * CountShared(MetaHelpers.GetStringList(a.Meta, "keywords"), MetaHelpers.GetStringList(b.Meta, "keywords"));
                    break;
                case "collection":
                    var ta = MetaHelpers.GetString(a.Meta, "collection");
                    var tb = MetaHelpers.GetString(b.Meta, "collection");
                    if (string.Equals(ta, tb, StringComparison.OrdinalIgnoreCase))
                    {
                        total += idx.Weight;
                    }

                    break;
                case "type":
                    var taType = MetaHelpers.GetString(a.Meta, "type");
                    var tbType = MetaHelpers.GetString(b.Meta, "type");
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
