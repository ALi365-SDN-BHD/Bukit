using System.Text.RegularExpressions;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static partial class PublicContentProjectionPolicy
{
    internal static string ResolvePublicId(ContentRecord record, string routeUrl)
    {
        var candidate = record.Identity.CanonicalUrlKey?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return routeUrl;
        }

        if ((IsNotionRecord(record) &&
             (ContainsNotionIdentifier(candidate) ||
              string.Equals(candidate, record.Identity.Id, StringComparison.OrdinalIgnoreCase))) ||
            (IsCloudBaseRecord(record) && ContainsCloudBaseIdentifier(candidate)))
        {
            return routeUrl;
        }

        return candidate;
    }

    internal static IReadOnlyList<EntityRecord> SanitizeEntities(ContentRecord record)
    {
        if (IsCloudBaseRecord(record))
        {
            return
            [
                .. record.Entities
                    .Where(entity => !ContainsCloudBaseIdentifier(entity.Name) &&
                                     !ContainsCloudBaseIdentifier(entity.Url))
                    .Select(entity => entity with
                    {
                        Id = SanitizeCloudBaseValue(entity.Id),
                        Url = SanitizeCloudBaseValue(entity.Url),
                        SameAs = entity.SameAs is null
                            ? null
                            : [.. entity.SameAs.Where(value => !ContainsCloudBaseIdentifier(value))]
                    })
            ];
        }

        if (!IsNotionRecord(record))
        {
            return record.Entities;
        }

        return record.Entities
            .Where(entity => !ContainsNotionIdentifier(entity.Name))
            .Select(entity => entity with
            {
                Id = SanitizeOptionalValue(entity.Id),
                Url = SanitizeOptionalValue(entity.Url),
                SameAs = entity.SameAs?
                    .Where(value => !ContainsNotionIdentifier(value))
                    .ToArray()
            })
            .ToArray();
    }

    internal static IReadOnlyList<ContentRelation> SanitizeRelations(ContentRecord record)
    {
        if (IsCloudBaseRecord(record))
        {
            return
            [
                .. record.Relations
                    .Where(relation => !ContainsCloudBaseIdentifier(relation.Target))
                    .Select(relation => relation with { TargetId = SanitizeCloudBaseValue(relation.TargetId) })
            ];
        }

        if (!IsNotionRecord(record))
        {
            return record.Relations;
        }

        return record.Relations
            .Where(relation => !ContainsNotionIdentifier(relation.Target))
            .Select(relation => relation with
            {
                TargetId = SanitizeOptionalValue(relation.TargetId)
            })
            .ToArray();
    }

    internal static bool ContainsNotionIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value) && NotionIdentifierRegex().IsMatch(value);

    private static bool IsNotionRecord(ContentRecord record)
        => string.Equals(record.Provenance.Source, "notion", StringComparison.OrdinalIgnoreCase);

    private static bool IsCloudBaseRecord(ContentRecord record)
        => string.Equals(record.Provenance.Source, "cloudbase", StringComparison.OrdinalIgnoreCase);

    internal static bool ContainsCloudBaseIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           (value.Contains("cloud://", StringComparison.OrdinalIgnoreCase) ||
            value.Contains(".api.tcloudbasegateway.com", StringComparison.OrdinalIgnoreCase) ||
            value.Contains(".api.intl.tcloudbasegateway.com", StringComparison.OrdinalIgnoreCase) ||
            value.Contains(".storage.tcloudbase.com", StringComparison.OrdinalIgnoreCase));

    private static string? SanitizeCloudBaseValue(string? value)
        => ContainsCloudBaseIdentifier(value) ? null : value;

    private static string? SanitizeOptionalValue(string? value)
        => ContainsNotionIdentifier(value) ? null : value;

    [GeneratedRegex("(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex NotionIdentifierRegex();
}
