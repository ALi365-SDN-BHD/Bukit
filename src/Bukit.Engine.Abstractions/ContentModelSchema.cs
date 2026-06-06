namespace Bukit.Engine.Abstractions.Content;

public sealed record ContentModelSchema(
    IReadOnlyList<string>? ContentTypes = null,
    IReadOnlyList<string>? Statuses = null,
    IReadOnlyList<string>? ReviewStatuses = null,
    IReadOnlyList<string>? SyncStatuses = null,
    bool RequireSummary = false,
    bool RequireAuthor = false,
    bool RequireOrganization = false,
    bool RequireUpdatedAt = false,
    bool RequireProvenance = false,
    bool RequireReviewedAt = false,
    bool RequireMediaAlt = true,
    bool RequireMediaDescription = false,
    bool RequireMediaLicense = false,
    bool RequireEntityIds = false,
    bool RequireRelationTargets = true);
