using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Notion;

internal sealed record RelationTargetInfo(
    string PageId,
    string Title,
    string Slug,
    string Type,
    string? Url);

internal static class NotionRelationLinkBuilder
{
    public static IReadOnlyDictionary<string, RelationTargetInfo> BuildIndex(IEnumerable<RelationTargetInfo> targets)
    {
        var map = new Dictionary<string, RelationTargetInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in targets)
        {
            if (string.IsNullOrWhiteSpace(t.PageId))
            {
                continue;
            }

            map[t.PageId] = t;
        }

        return map;
    }

    public static IReadOnlyDictionary<string, ContentField> EnrichFields(
        IReadOnlyDictionary<string, ContentField> fields,
        IReadOnlyList<string> relationKeys,
        IReadOnlyDictionary<string, RelationTargetInfo> index)
    {
        if (relationKeys.Count == 0 || index.Count == 0)
        {
            return fields;
        }

        var mutated = false;
        Dictionary<string, ContentField>? dict = null;

        for (var i = 0; i < relationKeys.Count; i++)
        {
            var key = relationKeys[i];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!fields.TryGetValue(key, out var field))
            {
                continue;
            }

            if (field.Value is not IEnumerable<string> ids)
            {
                continue;
            }

            var linkKey = $"{key}_links";
            if (fields.ContainsKey(linkKey))
            {
                continue;
            }

            var links = new List<Dictionary<string, object?>>();
            foreach (var rawId in ids)
            {
                var id = (rawId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (index.TryGetValue(id, out var t))
                {
                    links.Add(new Dictionary<string, object?>
                    {
                        ["id"] = t.PageId,
                        ["title"] = t.Title,
                        ["url"] = t.Url,
                        ["slug"] = t.Slug,
                        ["type"] = t.Type
                    });
                }
                else
                {
                    links.Add(new Dictionary<string, object?>
                    {
                        ["id"] = id,
                        ["title"] = null,
                        ["url"] = null,
                        ["slug"] = null,
                        ["type"] = null
                    });
                }
            }

            if (links.Count == 0)
            {
                continue;
            }

            dict ??= new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);
            dict[linkKey] = new ContentField("list", links);
            mutated = true;
        }

        return mutated ? dict! : fields;
    }
}

