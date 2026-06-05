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
        ContentItem item,
        RouteInfo route,
        IReadOnlyList<SeoAlternateModel>? alternates = null)
    {
        var title = FirstTextOrMeta(item, "seo_title") ?? FirstTextOrMeta(item, "seotitle") ?? item.Title;
        var description = FirstTextOrMeta(item, "seo_desc") ?? FirstTextOrMeta(item, "seodesc") ?? MetaHelpers.GetString(item.Meta, "summary") ?? config.Site.Description;
        var canonical = FirstTextOrMeta(item, "canonical") ?? BuildAbsoluteUrl(config.Site.Url, baseUrl, route.Url);
        var robots = FirstTextOrMeta(item, "robots");
        var image = FirstTextOrMeta(item, "og_image")
            ?? FirstTextOrMeta(item, "cover")
            ?? FirstTextOrMeta(item, "image")
            ?? config.Site.Seo.DefaultImage;
        image = BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, image);

        var geo = SeoGeoMetaParser.ParseGeoMeta(item);
        var schemaType = ResolveSchemaType(item, geo);
        var isArticle = IsArticleSchemaType(schemaType) || IsTruthyMeta(item, "seo_article");
        schemaType = isArticle && string.IsNullOrWhiteSpace(schemaType) ? "BlogPosting" : schemaType;
        var isStructuredContent = isArticle || IsStructuredContentSchemaType(schemaType);
        var isCollectionPage = !isStructuredContent && IsCollectionLikePage(item);
        TryGetUpdateTime(item, out var updated);
        var author = FirstTextOrMeta(item, "author");
        var tags = GetStringList(item.Meta, "tags") ?? Array.Empty<string>();
        var jsonLd = SeoJsonLdBuilder.BuildJsonLd(config, baseUrl, title, description, canonical, image, route.Url, item, item.Fields, isStructuredContent, isCollectionPage, geo, schemaType);

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
                Creator = FirstTextOrMeta(item, "twitter_creator")
            },
            Article = new SeoArticleModel
            {
                PublishedTime = isArticle ? item.PublishAt : null,
                ModifiedTime = isArticle && updated != default ? updated : null,
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
            JsonLd = SeoJsonLdBuilder.BuildJsonLd(config, baseUrl, title, description, canonical, image, page.Url, item: null, itemListFields: page.Fields, isPost: false, isCollectionPage: page.Url != "/", geo: SeoGeoMetaParser.ParsedGeoMeta.Empty, schemaType: null)
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
        return MetaHelpers.TryGetI18nKey(item.Meta, out var key) ? $"i18n:{key}" : $"route:{route.Url}";
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
        var value = MetaHelpers.TryGetTextField(fields, key);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static string? FirstTextOrMeta(ContentItem item, string key)
    {
        return FirstTextField(item.Fields, key) ?? CleanText(MetaHelpers.GetString(item.Meta, key));
    }

    internal static string? CleanText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static string? ResolveSchemaType(ContentItem item, SeoGeoMetaParser.ParsedGeoMeta geo)
        => CleanText(geo.SchemaType)
           ?? FirstTextOrMeta(item, "schema_type")
           ?? FirstTextOrMeta(item, "seo_schema_type");

    internal static bool IsArticleSchemaType(string? schemaType)
        => schemaType is not null &&
           (schemaType.EndsWith("Article", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, "BlogPosting", StringComparison.OrdinalIgnoreCase));

    internal static bool IsStructuredContentSchemaType(string? schemaType)
        => schemaType is not null &&
           (string.Equals(schemaType, "FAQPage", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, "HowTo", StringComparison.OrdinalIgnoreCase));

    private static bool IsTruthyMeta(ContentItem item, string key)
    {
        if (!item.Meta.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("1", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    internal static bool IsCollectionLikePage(ContentItem item)
        => item.Fields is not null &&
           (item.Fields.ContainsKey("items") || item.Fields.ContainsKey("terms"));

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

    internal static bool TryGetUpdateTime(ContentItem item, out DateTimeOffset updated)
    {
        updated = default;
        var value = FirstTextOrMeta(item, "update_time");
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
