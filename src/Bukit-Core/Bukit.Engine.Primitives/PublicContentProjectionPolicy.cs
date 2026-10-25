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

        if (IsNotionRecord(record) &&
            (ContainsNotionIdentifier(candidate) ||
             string.Equals(candidate, record.Identity.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return routeUrl;
        }

        return candidate;
    }

    internal static IReadOnlyList<EntityRecord> SanitizeEntities(ContentRecord record)
    {
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

    private static string? SanitizeOptionalValue(string? value)
        => ContainsNotionIdentifier(value) ? null : value;

    [GeneratedRegex("(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex NotionIdentifierRegex();
}
