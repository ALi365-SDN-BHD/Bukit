using Bukit.Config;

namespace Bukit.Engine;

internal static class CollectionIndexabilityPolicy
{
    internal static bool ShouldNoIndex(
        AppConfig config,
        ListRouteKind kind,
        string? collectionKey,
        int totalItems)
    {
        if (kind is not (ListRouteKind.CollectionList or ListRouteKind.CollectionPage or ListRouteKind.FilteredListPage))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(collectionKey))
        {
            return false;
        }

        if (config.Site.Collections is not { Count: > 0 } collections ||
            !collections.TryGetValue(collectionKey, out var collection))
        {
            return false;
        }

        var policy = collection.NoindexWhenEmpty
            ? new CollectionIndexPolicyConfig
            {
                MinimumItems = 1,
                BelowMinimum = "noindex-follow"
            }
            : collection.IndexPolicy;

        if (!string.Equals(policy.BelowMinimum, "noindex-follow", StringComparison.Ordinal))
        {
            return false;
        }

        return totalItems < policy.MinimumItems;
    }
}
