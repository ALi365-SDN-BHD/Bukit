using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

internal static class SeoModelBuilder
{
    internal static SeoModel BuildForContent(
        AppConfig config,
        string baseUrl,
        ContentDocument document,
        RouteInfo route,
        IReadOnlyList<SeoAlternateModel>? alternates = null)
    {
        var record = document.Record;
        var fields = document.CustomFields;
        var title = FirstTextField(fields, "seo_title") ?? FirstTextField(fields, "seotitle") ?? document.Title;
        var description = FirstTextField(fields, "seo_desc") ?? FirstTextField(fields, "seodesc") ?? record.Presentation.Summary ?? config.Site.Description;
        var canonical = FirstTextField(fields, "canonical") ?? BuildAbsoluteUrl(config.Site.Url, baseUrl, route.Url);
        var robots = FirstTextField(fields, "robots");
        var image = FirstTextField(fields, "og_image")
            ?? FirstTextField(fields, "cover")
            ?? FirstTextField(fields, "image")
            ?? record.Media.FirstOrDefault(media => string.Equals(media.Kind, "image", StringComparison.OrdinalIgnoreCase))?.Url
            ?? config.Site.Seo.DefaultImage;
        image = BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, image);

        var geo = SeoGeoMetaParser.ParseGeoMeta(document);
        var schemaType = ResolveSchemaType(fields, geo);
        var isArticle = IsArticleSchemaType(schemaType)
                        || IsTruthyField(fields, "seo_article")
                        || string.Equals(record.Identity.ContentType, "post", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(record.Classification.Type, "post", StringComparison.OrdinalIgnoreCase);
        schemaType = isArticle && string.IsNullOrWhiteSpace(schemaType) ? "BlogPosting" : schemaType;
        var isStructuredContent = isArticle || IsStructuredContentSchemaType(schemaType);
        var isCollectionPage = !isStructuredContent && IsCollectionLikePage(fields);
        var updated = record.Lifecycle.UpdatedAt;
        var author = record.Ownership.Author ?? FirstTextField(fields, "author");
        var tags = record.Classification.Tags.Count > 0 ? record.Classification.Tags : ContentFieldReader.GetTextList(fields, "tags") ?? Array.Empty<string>();
        var jsonLd = SeoJsonLdBuilder.BuildJsonLd(config, baseUrl, title, description, canonical, image, route.Url, document, fields, isStructuredContent, isCollectionPage, geo, schemaType, record);

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
                Locale = config.Site.Language
            },
            Twitter = new SeoTwitterModel
            {
                Card = string.IsNullOrWhiteSpace(image) ? "summary" : "summary_large_image",
                Title = title,
                Description = description,
                Image = image,
                Site = config.Site.Seo.TwitterSite,
                Creator = FirstTextField(fields, "twitter_creator")
            },
            Article = new SeoArticleModel
            {
                PublishedTime = isArticle ? record.Lifecycle.PublishedAt : null,
                ModifiedTime = isArticle ? updated : null,
                Author = isArticle ? author : null,
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

    internal static SeoModel BuildForList(
        AppConfig config,
        string baseUrl,
        PageInfo page,
        IReadOnlyList<SeoAlternateModel>? alternates = null)
        => BuildForListCore(config, baseUrl, page, page.Url, prevUrl: null, nextUrl: null, alternates);

    internal static SeoModel BuildForList(
        AppConfig config,
        string baseUrl,
        PageInfo page,
        ListRoutePlan route,
        IReadOnlyList<SeoAlternateModel>? alternates = null)
        => BuildForListCore(config, baseUrl, page, route.CanonicalUrl, route.PrevUrl, route.NextUrl, alternates);

    private static SeoModel BuildForListCore(
        AppConfig config,
        string baseUrl,
        PageInfo page,
        string canonicalUrl,
        string? prevUrl,
        string? nextUrl,
        IReadOnlyList<SeoAlternateModel>? alternates)
    {
        var title = page.Title;
        var description = page.Summary ?? config.Site.Description;
        var canonical = BuildAbsoluteUrl(config.Site.Url, baseUrl, canonicalUrl);
        var prev = string.IsNullOrWhiteSpace(prevUrl) ? null : BuildAbsoluteUrl(config.Site.Url, baseUrl, prevUrl);
        var next = string.IsNullOrWhiteSpace(nextUrl) ? null : BuildAbsoluteUrl(config.Site.Url, baseUrl, nextUrl);
        var image = BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, config.Site.Seo.DefaultImage);

        return new SeoModel
        {
            Title = title,
            Description = description,
            Canonical = canonical,
            Prev = prev,
            Next = next,
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
            JsonLd = SeoJsonLdBuilder.BuildJsonLd(config, baseUrl, title, description, canonical, image, canonicalUrl, document: null, itemListFields: page.Fields, isPost: false, isCollectionPage: page.Url != "/", geo: SeoGeoMetaParser.ParsedGeoMeta.Empty, schemaType: null, record: null)
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

    internal static string BuildAlternateKey(ContentDocument document, RouteInfo route)
    {
        return ContentFieldReader.TryGetI18nKey(document.CustomFields, out var key) ? $"i18n:{key}" : $"route:{route.Url}";
    }

    internal static string BuildListAlternateKey(RouteInfo route) => $"route:{route.Url}";

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
        var value = ContentFieldReader.GetText(fields, key);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static string? CleanText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static string? ResolveSchemaType(IReadOnlyDictionary<string, ContentField>? fields, SeoGeoMetaParser.ParsedGeoMeta geo)
        => CleanText(geo.SchemaType)
           ?? FirstTextField(fields, "schema_type")
           ?? FirstTextField(fields, "seo_schema_type");

    internal static bool IsArticleSchemaType(string? schemaType)
        => schemaType is not null &&
           (schemaType.EndsWith("Article", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, "BlogPosting", StringComparison.OrdinalIgnoreCase));

    internal static bool IsStructuredContentSchemaType(string? schemaType)
        => schemaType is not null &&
           (string.Equals(schemaType, "FAQPage", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, "HowTo", StringComparison.OrdinalIgnoreCase));

    private static bool IsTruthyField(IReadOnlyDictionary<string, ContentField>? fields, string key)
        => ContentFieldReader.GetBool(fields, key) is true;

    internal static bool IsCollectionLikePage(IReadOnlyDictionary<string, ContentField>? fields)
        => fields is not null &&
           (fields.ContainsKey("items") || fields.ContainsKey("terms"));

    internal static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (value is IEnumerable<object> values)
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

    internal static bool TryGetUpdateTime(ContentDocument document, out DateTimeOffset updated)
        => TryGetUpdateTime(document.CustomFields, out updated);

    internal static bool TryGetUpdateTime(IReadOnlyDictionary<string, ContentField>? fields, out DateTimeOffset updated)
    {
        updated = default;
        var value = FirstTextField(fields, "update_time");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(value, out updated);
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
