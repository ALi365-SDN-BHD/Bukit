using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

internal static class SeoModelBuilder
{
    internal static SeoModel BuildForDocument(
        AppConfig config,
        string baseUrl,
        ContentDocument document,
        RouteInfo route,
        IReadOnlyList<SeoAlternateModel>? alternates = null)
    {
        var record = document.Record;
        var title = FirstTextField(document.CustomFields, "seo_title") ?? FirstTextField(document.CustomFields, "seotitle") ?? record.Presentation.Title;
        var description = FirstTextField(document.CustomFields, "seo_desc") ?? FirstTextField(document.CustomFields, "seodesc") ?? record.Presentation.Summary ?? config.Site.Description;
        var canonical = FirstTextField(document.CustomFields, "canonical") ?? BuildAbsoluteUrl(config.Site.Url, baseUrl, route.Url);
        var robots = BuildRobots(document.Publish, FirstTextField(document.CustomFields, "robots"));
        var image = FirstTextField(document.CustomFields, "og_image")
            ?? FirstTextField(document.CustomFields, "cover")
            ?? FirstTextField(document.CustomFields, "image")
            ?? record.Media.FirstOrDefault(media => string.Equals(media.Kind, "image", StringComparison.OrdinalIgnoreCase))?.Url
            ?? config.Site.Seo.DefaultImage;
        image = BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, image);

        var geo = SeoGeoMetaParser.ParseGeoMeta(document.CustomFields);
        var schemaType = CleanText(geo.SchemaType)
            ?? FirstTextField(document.CustomFields, "schema_type")
            ?? FirstTextField(document.CustomFields, "seo_schema_type");
        var isArticle = IsArticleSchemaType(schemaType)
                        || document.CustomFields.TryGetValue("seo_article", out var seoArticle) && IsTruthyValue(seoArticle.Value)
                        || string.Equals(record.Identity.ContentType, "post", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(record.Classification.Type, "post", StringComparison.OrdinalIgnoreCase);
        schemaType = isArticle && string.IsNullOrWhiteSpace(schemaType) ? "BlogPosting" : schemaType;
        var isStructuredContent = isArticle || IsStructuredContentSchemaType(schemaType);
        var isCollectionPage = !isStructuredContent && IsCollectionLikePage(document.CustomFields);
        var tags = record.Classification.Tags.Count > 0 ? record.Classification.Tags : Array.Empty<string>();
        var jsonLd = SeoJsonLdBuilder.BuildJsonLd(
            config,
            baseUrl,
            title,
            description,
            canonical,
            image,
            route.Url,
            item: null,
            itemListFields: document.CustomFields,
            isStructuredContent,
            isCollectionPage,
            geo,
            schemaType,
            record);

        return new SeoModel
        {
            Title = title,
            Description = description,
            Canonical = canonical,
            Robots = robots,
            Og = new SeoOpenGraphModel
            {
                Title = title,
                Description = description,
                Url = canonical,
                Image = image,
                Type = isArticle ? "article" : "website",
                SiteName = config.Site.Title,
                Locale = record.Presentation.Language
            },
            Twitter = new SeoTwitterModel
            {
                Card = string.IsNullOrWhiteSpace(image) ? "summary" : "summary_large_image",
                Title = title,
                Description = description,
                Image = image,
                Site = config.Site.Seo.TwitterSite,
                Creator = FirstTextField(document.CustomFields, "twitter_creator")
            },
            Article = new SeoArticleModel
            {
                PublishedTime = isArticle ? record.Lifecycle.PublishedAt : null,
                ModifiedTime = isArticle ? record.Lifecycle.UpdatedAt : null,
                Author = isArticle ? record.Ownership.Author : null,
                Tags = isArticle ? tags : Array.Empty<string>()
            },
            Alternates = alternates ?? Array.Empty<SeoAlternateModel>(),
            JsonLd = jsonLd,
            SchemaType = schemaType,
            FaqItems = geo.FaqItems,
            HowToSteps = geo.HowToSteps,
            Citations = geo.Citations,
            GeoAuthor = geo.GeoAuthor,
            SpeakableXPath = geo.SpeakableXPath,
            SameAs = geo.SameAs
        };
    }

    internal static SeoModel BuildForContent(
        AppConfig config,
        string baseUrl,
        ContentItem item,
        RouteInfo route,
        IReadOnlyList<SeoAlternateModel>? alternates = null)
    {
        return BuildForDocument(config, baseUrl, ToDocument(item), route, alternates);
    }

    internal static SeoModel BuildForList(
        AppConfig config,
        string baseUrl,
        PageInfo page,
        IReadOnlyList<SeoAlternateModel>? alternates = null)
    {
        var title = page.Title;
        var description = page.Summary ?? config.Site.Description;
        var canonical = BuildAbsoluteUrl(config.Site.Url, baseUrl, page.Url);
        var image = BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, config.Site.Seo.DefaultImage);

        return new SeoModel
        {
            Title = title,
            Description = description,
            Canonical = canonical,
            Og = new SeoOpenGraphModel
            {
                Title = title,
                Description = description,
                Url = canonical,
                Image = image,
                Type = "website",
                SiteName = config.Site.Title,
                Locale = config.Site.Language
            },
            Twitter = new SeoTwitterModel
            {
                Card = string.IsNullOrWhiteSpace(image) ? "summary" : "summary_large_image",
                Title = title,
                Description = description,
                Image = image,
                Site = config.Site.Seo.TwitterSite
            },
            Alternates = alternates ?? Array.Empty<SeoAlternateModel>(),
            JsonLd = SeoJsonLdBuilder.BuildJsonLd(config, baseUrl, title, description, canonical, image, page.Url, item: null, itemListFields: page.Fields, isPost: false, isCollectionPage: page.Url != "/", geo: SeoGeoMetaParser.ParsedGeoMeta.Empty, schemaType: null, record: null)
        };
    }

    internal static string BuildAbsoluteUrl(string? siteUrl, string baseUrl, string url)
    {
        var u = url.StartsWith('/') ? url : "/" + url;
        var b = NormalizeBaseUrl(baseUrl);
        var path = b == "/" ? u : $"{b}{u}";

        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return path;
        }

        return siteUrl.Trim().TrimEnd('/') + path;
    }

    internal static string BuildAlternateKey(ContentItem item, RouteInfo route)
    {
        var i18nKey = FirstTextField(item.Fields, "i18nKey") ?? FirstTextField(item.Fields, "i18n_key");
        if (!string.IsNullOrWhiteSpace(i18nKey))
        {
            return $"i18n:{i18nKey}";
        }

        return $"route:{route.Url}";
    }

    internal static string BuildListAlternateKey(RouteInfo route) => $"route:{route.Url}";

    internal static string BuildDocumentAlternateKey(ContentDocument document, RouteInfo route)
    {
        var i18nKey = FirstTextField(document.CustomFields, "i18nKey") ?? FirstTextField(document.CustomFields, "i18n_key");
        if (!string.IsNullOrWhiteSpace(i18nKey))
        {
            return $"i18n:{i18nKey}";
        }

        return !string.IsNullOrWhiteSpace(document.Record.Identity.CanonicalUrlKey)
            ? $"content:{document.Record.Identity.CanonicalUrlKey}"
            : $"route:{route.Url}";
    }

    internal static bool IsIndexable(string? robots)
    {
        if (string.IsNullOrWhiteSpace(robots))
        {
            return true;
        }

        var tokens = robots.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return !tokens.Any(t =>
            string.Equals(t, "noindex", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "none", StringComparison.OrdinalIgnoreCase));
    }

    internal static string? FirstTextField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || !fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        var value = field.Value is string text ? text : field.Value.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static string? FirstTextField(ContentItem item, string key)
    {
        return FirstTextField(item.Fields, key);
    }

    internal static string? CleanText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static string? ResolveSchemaType(ContentItem item, SeoGeoMetaParser.ParsedGeoMeta geo)
        => CleanText(geo.SchemaType)
           ?? FirstTextField(item.Fields, "schema_type")
           ?? FirstTextField(item.Fields, "seo_schema_type");

    internal static bool IsArticleSchemaType(string? schemaType)
        => schemaType is not null &&
           (schemaType.EndsWith("Article", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, "BlogPosting", StringComparison.OrdinalIgnoreCase));

    internal static bool IsStructuredContentSchemaType(string? schemaType)
        => schemaType is not null &&
           (string.Equals(schemaType, "FAQPage", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, "HowTo", StringComparison.OrdinalIgnoreCase));

    internal static bool IsCollectionLikePage(ContentItem item)
        => item.Fields is not null &&
           (item.Fields.ContainsKey("items") || item.Fields.ContainsKey("terms"));

    internal static bool IsCollectionLikePage(IReadOnlyDictionary<string, ContentField>? fields)
        => fields is not null &&
           (fields.ContainsKey("items") || fields.ContainsKey("terms"));

    internal static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || !fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        if (field.Value is string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (field.Value is IEnumerable<object> values)
        {
            var list = values
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
            return list.Count == 0 ? null : list;
        }

        return null;
    }

    private static ContentDocument ToDocument(ContentItem item)
    {
        var fields = item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        var type = FirstTextField(fields, "type") ?? "post";
        var collection = FirstTextField(fields, "collection") ?? type;
        var summary = FirstTextField(fields, "summary");
        var language = FirstTextField(fields, "language") ?? "und";
        var tags = GetStringList(fields, "tags") ?? Array.Empty<string>();
        var categories = GetStringList(fields, "categories") ?? Array.Empty<string>();
        var record = new ContentRecord(
            new ContentIdentity(item.Id, item.Slug, item.Id, type, "published"),
            new ContentPresentation(item.Title, summary, item.ContentHtml, language, Array.Empty<string>()),
            new ContentClassification(type, collection, categories, tags),
            new ContentOwnership(FirstTextField(fields, "author"), null, null, null),
            new ContentLifecycle(item.PublishAt, TryGetUpdatedAt(fields), null, null),
            new ProvenanceRecord(FirstTextField(fields, "source"), FirstTextField(fields, "original_url"), Array.Empty<string>(), Array.Empty<string>(), null),
            new TrustMetadata(null, FirstTextField(fields, "review_status") ?? "unreviewed", Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef(item.ContentHtml, null, null, null),
            new ContentRoutePolicy(null, null, null, null, collection),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            fields,
            Array.Empty<ContentDiagnostic>());
    }

    internal static string? BuildMaybeAbsoluteUrl(string? siteUrl, string baseUrl, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return BuildAbsoluteUrl(siteUrl, baseUrl, trimmed);
    }

    internal static bool TryGetUpdateTime(ContentItem item, out DateTimeOffset updated)
    {
        updated = default;
        var value = FirstTextField(item.Fields, "update_time");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(value, out updated);
    }

    private static DateTimeOffset? TryGetUpdatedAt(IReadOnlyDictionary<string, ContentField> fields)
    {
        return DateTimeOffset.TryParse(FirstTextField(fields, "update_time"), out var updated)
            ? updated
            : null;
    }

    private static string? BuildRobots(ContentPublishPolicy publish, string? explicitRobots)
    {
        if (!string.IsNullOrWhiteSpace(explicitRobots))
        {
            return explicitRobots;
        }

        var tokens = new List<string>();
        if (publish.NoIndex)
        {
            tokens.Add("noindex");
        }

        if (publish.NoFollow)
        {
            tokens.Add("nofollow");
        }

        return tokens.Count == 0 ? null : string.Join(',', tokens);
    }

    private static bool IsTruthyValue(object? value)
    {
        return value switch
        {
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("1", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    internal static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl) ? "/" : baseUrl.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    internal static string ToTitle(string segment)
    {
        var text = segment.Replace('-', ' ');
        return string.IsNullOrWhiteSpace(text) ? segment : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
