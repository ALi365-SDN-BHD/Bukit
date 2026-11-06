namespace Bukit.Engine;

/// <summary>
/// Raw keys that strict unknown-key validation and doctor diagnostics must not ask
/// content authors to declare. Two groups, kept apart so each is easy to review:
/// <list type="bullet">
/// <item><see cref="EngineInjected"/>: written by providers or the engine itself.</item>
/// <item><see cref="CanonicalFields"/>: front matter / Notion keys the engine reads.</item>
/// </list>
/// <c>ContentFieldReservationsTests</c> scans the source tree for literal field reads
/// and fails when a key the engine reads is missing here.
/// </summary>
internal static class ContentFieldReservations
{
    internal static readonly IReadOnlySet<string> EngineInjected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "collection", "source", "sourcepath", "sourcekey", "sourcemode", "sourceid",
        "bodyfingerprint", "tableofcontents", "outputpath", "route", "url",
        "notionpageid", "last_edited_time", "sync_status"
    };

    internal static readonly IReadOnlySet<string> CanonicalFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Identity and routing
        "title", "slug", "type", "template", "permalink", "listgroup", "aliases", "collections",
        "language", "locale", "i18nkey", "i18n_key", "translations", "sections",
        // Dates and lifecycle
        "publishat", "published", "draft", "status", "updatedat", "updated", "modified", "update_time",
        "expires_at", "expires", "reviewed_at", "evergreen",
        // Summary and taxonomy
        "summary", "description", "excerpt", "tags", "categories", "keywords",
        // Data modules
        "enabled", "order",
        // Ownership and trust
        "author", "authors", "organization", "org", "company", "owner", "owners", "reviewer", "reviewers",
        "authortype", "original_url", "source_url", "citations", "references",
        "credibility_score", "trust_score", "review_status", "quality_flags",
        // Publish policy
        "noindex", "nofollow", "feedexclude", "excludefromfeed",
        "searchexclude", "excludefromsearch", "sitemapexclude", "excludefromsitemap",
        // Media
        "image", "cover", "video", "attachment", "alt", "caption", "license",
        "image_alt", "cover_alt", "video_alt", "attachment_alt",
        "image_caption", "cover_caption", "video_caption", "attachment_caption",
        "image_description", "cover_description", "video_description", "attachment_description",
        "image_license", "cover_license", "video_license", "attachment_license",
        // SEO / GEO
        "seo_title", "seotitle", "seo_desc", "seodesc", "seo_image", "og_image", "canonical", "robots",
        "twitter_creator", "schema_type", "seo_schema_type", "seo_article", "seo_inject", "geo",
        // Entities and relations
        "entities", "products", "services", "places", "people", "companies", "related_to",
        "sameas", "same_as"
    };

    internal static readonly IReadOnlySet<string> EngineManaged =
        new HashSet<string>(EngineInjected.Concat(CanonicalFields), StringComparer.OrdinalIgnoreCase);

    internal static bool IsEngineManaged(string key) => EngineManaged.Contains(key);
}
