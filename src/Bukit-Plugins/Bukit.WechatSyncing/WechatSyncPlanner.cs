using System.Globalization;

namespace Bukit.WechatSyncing;

using static WechatSyncHelpers;

internal static class WechatSyncPlanner
{
    private static readonly HashSet<string> DefaultDraftReviewStatuses =
        new(["reviewed", "verified", "approved"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DefaultPublishReviewStatuses =
        new(["verified", "approved"], StringComparer.OrdinalIgnoreCase);

    internal static WechatSyncPlan Create(
        WechatSyncContext context,
        WechatSyncOptions options,
        DateTimeOffset now)
    {
        var candidates = new List<WechatSyncCandidate>();
        var exclusions = new List<WechatSyncPlanExclusion>();
        if (!TryResolveReviewPolicy(options, out var draftReviewStatuses, out var publishReviewStatuses, out var policyError))
        {
            exclusions.Add(new WechatSyncPlanExclusion(
                "plugin.wechat-sync.invalidReviewPolicy",
                "error",
                $"wechat-sync review policy is invalid: {policyError}.",
                null));
            return new WechatSyncPlan(candidates, exclusions);
        }

        foreach (var (item, route) in context.Routed)
        {
            if (ReadMetaString(item.Metadata, "sourceMode").Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                exclusions.Add(Exclude(item, route, "sourceModeDenied", "info", "source mode is data"));
                continue;
            }

            var sourceKey = ReadMetaString(item.Metadata, "sourceKey");
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                sourceKey = ReadMetaString(item.Metadata, "source");
            }

            if (options.SourceNames.Count > 0 && !options.SourceNames.Contains(sourceKey))
            {
                exclusions.Add(Exclude(item, route, "sourceDenied", "info", $"source '{sourceKey}' is not selected"));
                continue;
            }

            var fieldType = ReadFieldType(item.Fields);
            if (!MatchesType(fieldType, options.ContentTypes, options.DefaultTypesWhenMissing))
            {
                exclusions.Add(Exclude(item, route, "contentTypeDenied", "info", $"content type '{fieldType ?? "(missing)"}' is not selected"));
                continue;
            }

            var manifestReviewStatus = ReadMetaString(item.Metadata, "manifestReviewStatus").Trim();
            var contentReviewStatus = ReadMetaString(item.Metadata, "reviewStatus").Trim();
            if (manifestReviewStatus.Length == 0 || contentReviewStatus.Length == 0)
            {
                exclusions.Add(Exclude(item, route, "reviewStatusMissing", "warning", "manifest and content review status are required"));
                continue;
            }

            if (!manifestReviewStatus.Equals(contentReviewStatus, StringComparison.OrdinalIgnoreCase))
            {
                exclusions.Add(Exclude(item, route, "reviewStatusMismatch", "warning", "manifest and content review status do not match"));
                continue;
            }

            if (!TryReadExpiry(item.Metadata, out var expiresAt))
            {
                exclusions.Add(Exclude(item, route, "contentExpiryInvalid", "warning", "content expiry is invalid"));
                continue;
            }

            if (expiresAt is { } expiry && expiry <= now)
            {
                exclusions.Add(Exclude(item, route, "contentExpired", "warning", $"content expired at {expiry:O}"));
                continue;
            }

            var allowedReviewStatuses = options.Target.Equals("publish", StringComparison.OrdinalIgnoreCase)
                ? publishReviewStatuses
                : draftReviewStatuses;
            if (!allowedReviewStatuses.Contains(contentReviewStatus))
            {
                exclusions.Add(Exclude(
                    item,
                    route,
                    "reviewStatusDenied",
                    "warning",
                    $"review status '{contentReviewStatus}' is not allowed for target '{options.Target}'"));
                continue;
            }

            var sourceId = ReadMetaString(item.Metadata, "sourceId");
            var syncKey = !string.IsNullOrWhiteSpace(sourceKey) && !string.IsNullOrWhiteSpace(sourceId)
                ? $"{sourceKey}:{sourceId}"
                : item.Id;
            candidates.Add(new WechatSyncCandidate(syncKey, sourceKey, sourceId, item, route, expiresAt));
        }

        var hasErrors = exclusions.Any(exclusion =>
            exclusion.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        return new WechatSyncPlan(hasErrors ? [] : candidates, exclusions);
    }

    private static bool TryResolveReviewPolicy(
        WechatSyncOptions options,
        out HashSet<string> draftReviewStatuses,
        out HashSet<string> publishReviewStatuses,
        out string error)
    {
        draftReviewStatuses = NormalizeReviewStatuses(options.DraftReviewStatuses ?? DefaultDraftReviewStatuses);
        publishReviewStatuses = NormalizeReviewStatuses(options.PublishReviewStatuses ?? DefaultPublishReviewStatuses);

        if (draftReviewStatuses.Count == 0 || publishReviewStatuses.Count == 0)
        {
            error = "draft and publish review status allowlists must not be empty";
            return false;
        }

        if (draftReviewStatuses.Contains("*") || publishReviewStatuses.Contains("*"))
        {
            error = "wildcard review statuses are not allowed";
            return false;
        }

        if (!publishReviewStatuses.IsSubsetOf(draftReviewStatuses))
        {
            error = "publish review statuses must be a subset of draft review statuses";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static HashSet<string> NormalizeReviewStatuses(IEnumerable<string> statuses)
        => new(
            statuses
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Select(status => status.Trim()),
            StringComparer.OrdinalIgnoreCase);

    private static WechatSyncPlanExclusion Exclude(
        WechatSyncItem item,
        WechatSyncRoute route,
        string reason,
        string severity,
        string message)
        => new(
            $"plugin.wechat-sync.{reason}",
            severity,
            $"wechat-sync item '{item.Id}' excluded: {message}.",
            route.OutputPath);

    private static bool TryReadExpiry(
        IReadOnlyDictionary<string, object> metadata,
        out DateTimeOffset? expiresAt)
    {
        expiresAt = null;
        if (!metadata.TryGetValue("expiresAt", out var value))
        {
            return true;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            expiresAt = dateTimeOffset;
            return true;
        }

        if (value is string text &&
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dateTimeOffset))
        {
            expiresAt = dateTimeOffset;
            return true;
        }

        return false;
    }

    private static bool MatchesType(
        string? type,
        HashSet<string> contentTypes,
        HashSet<string> defaultTypesWhenMissing)
    {
        if (contentTypes.Count == 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            return contentTypes.Contains(type);
        }

        return defaultTypesWhenMissing.Any(contentTypes.Contains);
    }
}
