using System.Text.Json.Nodes;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Plugins.Protocol;

internal static class ProtocolContentJsonBuilder
{
    internal static JsonObject Build(ContentRecord record)
        => new()
        {
            ["id"] = record.Identity.Id,
            ["slug"] = record.Identity.Slug,
            ["canonicalUrlKey"] = record.Identity.CanonicalUrlKey,
            ["type"] = record.Identity.ContentType,
            ["collection"] = record.Classification.Collection,
            ["status"] = record.Identity.Status,
            ["title"] = record.Presentation.Title,
            ["summary"] = record.Presentation.Summary,
            ["language"] = record.Presentation.Language,
            ["translations"] = ToJsonArray(record.Presentation.Translations),
            ["author"] = record.Ownership.Author,
            ["organization"] = record.Ownership.Organization,
            ["owner"] = record.Ownership.Owner,
            ["reviewer"] = record.Ownership.Reviewer,
            ["publishedAt"] = record.Lifecycle.PublishedAt,
            ["updatedAt"] = record.Lifecycle.UpdatedAt,
            ["expiresAt"] = record.Lifecycle.ExpiresAt,
            ["reviewedAt"] = record.Lifecycle.ReviewedAt,
            ["source"] = record.Provenance.Source,
            ["originalSource"] = record.Provenance.OriginalSource,
            ["citations"] = ToJsonArray(record.Provenance.Citations),
            ["references"] = ToJsonArray(record.Provenance.References),
            ["syncStatus"] = record.Provenance.SyncStatus,
            ["reviewStatus"] = record.Trust.ReviewStatus,
            ["credibilityScore"] = record.Trust.CredibilityScore,
            ["qualityFlags"] = ToJsonArray(record.Trust.QualityFlags),
            ["entities"] = new JsonArray(record.Entities.Select(x => (JsonNode)new JsonObject
            {
                ["type"] = x.Type,
                ["name"] = x.Name,
                ["description"] = x.Description,
                ["id"] = x.Id,
                ["url"] = x.Url,
                ["sameAs"] = x.SameAs is null ? null : ToJsonArray(x.SameAs)
            }).ToArray()),
            ["relations"] = new JsonArray(record.Relations.Select(x => (JsonNode)new JsonObject
            {
                ["type"] = x.Type,
                ["target"] = x.Target,
                ["targetType"] = x.TargetType,
                ["targetId"] = x.TargetId
            }).ToArray()),
            ["media"] = new JsonArray(record.Media.Select(x => (JsonNode)new JsonObject
            {
                ["kind"] = x.Kind,
                ["url"] = x.Url,
                ["alt"] = x.Alt,
                ["caption"] = x.Caption,
                ["description"] = x.Description,
                ["license"] = x.License
            }).ToArray())
        };

    private static JsonArray ToJsonArray(IEnumerable<string> values)
        => new(values.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray());
}
