using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Notion;

internal sealed record NotionRelationResolveCandidate(
    IReadOnlyList<string> RelationKeys,
    IReadOnlyDictionary<string, ContentField> Fields);

internal static class NotionRelationResolvePlan
{
    internal static IReadOnlyList<string> BuildMissingIds(
        IEnumerable<NotionRelationResolveCandidate> candidates,
        IReadOnlyDictionary<string, RelationTargetInfo> existingIndex,
        int maxResolve)
    {
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            AddIdsIfPresent(candidate, "tags");
            AddIdsIfPresent(candidate, "categories");

            if (maxResolve > 0 && missing.Count >= maxResolve)
            {
                break;
            }
        }

        return missing;

        void AddIdsIfPresent(NotionRelationResolveCandidate candidate, string key)
        {
            if (!HasRelationKey(candidate.RelationKeys, key))
            {
                return;
            }

            if (!candidate.Fields.TryGetValue(key, out var field))
            {
                return;
            }

            if (!IsRelationListField(field))
            {
                return;
            }

            var ids = ContentFieldReader.ToTextList(field.Value);
            if (ids is null || ids.Count == 0)
            {
                return;
            }

            foreach (var raw in ids)
            {
                var id = (raw ?? string.Empty).Trim();
                if (id.Length == 0 || existingIndex.ContainsKey(id) || !seen.Add(id))
                {
                    continue;
                }

                missing.Add(id);
                if (maxResolve > 0 && missing.Count >= maxResolve)
                {
                    return;
                }
            }
        }
    }

    private static bool IsRelationListField(ContentField field)
        => field.Type is "list" or "multi_select" or "relation";

    private static bool HasRelationKey(IReadOnlyList<string> relationKeys, string key)
    {
        for (var i = 0; i < relationKeys.Count; i++)
        {
            if (string.Equals(relationKeys[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
