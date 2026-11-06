namespace Bukit.Engine;

/// <summary>
/// Raw keys that the engine itself reads, injects, or normalizes. They are not user
/// schema fields, so strict unknown-key validation and doctor diagnostics must not
/// ask content authors to declare them.
/// </summary>
internal static class ContentFieldReservations
{
    internal static readonly IReadOnlySet<string> EngineManaged = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "title", "slug", "type", "collection", "publishat", "published", "language", "tags", "categories",
        "summary", "description", "excerpt", "status", "route", "url", "outputpath", "template", "source",
        "sourcepath", "sourcekey", "sourcemode", "sourceid", "bodyfingerprint", "tableofcontents", "draft",
        "i18nkey", "i18n_key", "locale", "notionpageid", "last_edited_time",
        "updatedat", "updated", "modified", "update_time", "expires_at", "expires", "reviewed_at", "evergreen",
        "author", "authors", "organization", "org", "company", "owner", "owners", "reviewer", "reviewers",
        "authortype", "original_url", "source_url", "citations", "references", "sync_status",
        "credibility_score", "trust_score", "review_status", "quality_flags", "translations", "sections",
        "permalink", "listgroup", "noindex", "nofollow", "feedexclude", "excludefromfeed",
        "searchexclude", "excludefromsearch", "sitemapexclude", "excludefromsitemap",
        "image", "cover", "video", "attachment", "alt", "caption", "license",
        "image_alt", "cover_alt", "video_alt", "attachment_alt",
        "image_caption", "cover_caption", "video_caption", "attachment_caption",
        "image_description", "cover_description", "video_description", "attachment_description",
        "image_license", "cover_license", "video_license", "attachment_license",
        "seo_title", "seotitle", "seo_desc", "seodesc", "canonical", "robots", "twitter_creator",
        "schema_type", "seo_schema_type", "seo_article",
        "entities", "products", "services", "places", "people", "companies", "related_to"
    };

    internal static bool IsEngineManaged(string key) => EngineManaged.Contains(key);
}
