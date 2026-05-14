using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Rendering;
using Bukit.Routing;

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
        var title = FirstTextOrMeta(item, "seo_title") ?? item.Title;
        var description = FirstTextOrMeta(item, "seo_desc") ?? MetaHelpers.GetString(item.Meta, "summary") ?? config.Site.Description;
        var canonical = FirstTextOrMeta(item, "canonical") ?? BuildAbsoluteUrl(config.Site.Url, baseUrl, route.Url);
        var robots = FirstTextOrMeta(item, "robots");
        var image = FirstTextOrMeta(item, "og_image")
            ?? FirstTextOrMeta(item, "cover")
            ?? FirstTextOrMeta(item, "image")
            ?? config.Site.Seo.DefaultImage;
        image = BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, image);

        var isPost = IsPost(item);
        var jsonLd = BuildJsonLd(config, title, description, canonical, image, route.Url, item, isPost);

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
                Type = isPost ? "article" : "website"
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
            JsonLd = jsonLd
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
                Type = "website"
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
            JsonLd = BuildJsonLd(config, title, description, canonical, image, page.Url, item: null, isPost: false)
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

    private static IReadOnlyList<string> BuildJsonLd(
        AppConfig config,
        string title,
        string? description,
        string canonical,
        string? image,
        string routeUrl,
        ContentItem? item,
        bool isPost)
    {
        var result = new List<string>();
        var siteHome = BuildAbsoluteUrl(config.Site.Url, config.Site.BaseUrl, "/");

        result.Add(ToJson(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = config.Site.Title,
            ["url"] = siteHome
        }));

        if (config.Site.Seo.Organization is { } org &&
            (!string.IsNullOrWhiteSpace(org.Name) || !string.IsNullOrWhiteSpace(org.Url) || !string.IsNullOrWhiteSpace(org.Logo)))
        {
            var organization = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Organization",
                ["name"] = string.IsNullOrWhiteSpace(org.Name) ? config.Site.Title : org.Name,
                ["url"] = string.IsNullOrWhiteSpace(org.Url) ? siteHome : org.Url
            };
            if (!string.IsNullOrWhiteSpace(org.Logo))
            {
                organization["logo"] = org.Logo;
            }

            result.Add(ToJson(organization));
        }

        if (routeUrl.Trim('/') is { Length: > 0 } trimmed)
        {
            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var items = new List<Dictionary<string, object?>>();
            var current = string.Empty;
            for (var i = 0; i < segments.Length; i++)
            {
                current += "/" + segments[i];
                items.Add(new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = i + 1,
                    ["name"] = i == segments.Length - 1 ? title : ToTitle(segments[i]),
                    ["item"] = BuildAbsoluteUrl(config.Site.Url, config.Site.BaseUrl, current + "/")
                });
            }

            result.Add(ToJson(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = items
            }));
        }

        if (isPost && item is not null)
        {
            var article = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BlogPosting",
                ["headline"] = title,
                ["description"] = description,
                ["url"] = canonical,
                ["datePublished"] = item.PublishAt.ToString("O")
            };
            if (!string.IsNullOrWhiteSpace(image))
            {
                article["image"] = image;
            }

            if (TryGetUpdateTime(item, out var updated))
            {
                article["dateModified"] = updated.ToString("O");
            }

            var author = FirstTextOrMeta(item, "author");
            if (!string.IsNullOrWhiteSpace(author))
            {
                article["author"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Person",
                    ["name"] = author
                };
            }

            result.Add(ToJson(article));
        }

        return result;
    }

    private static string? FirstTextField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        var value = MetaHelpers.TryGetTextField(fields, key);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstTextOrMeta(ContentItem item, string key)
    {
        return FirstTextField(item.Fields, key) ?? CleanText(MetaHelpers.GetString(item.Meta, key));
    }

    private static string? CleanText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsPost(ContentItem item)
    {
        var collection = MetaHelpers.GetString(item.Meta, "collection") ?? MetaHelpers.GetString(item.Meta, "type");
        return string.Equals(collection, "post", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildMaybeAbsoluteUrl(string? siteUrl, string baseUrl, string? value)
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

    private static bool TryGetUpdateTime(ContentItem item, out DateTimeOffset updated)
    {
        updated = default;
        var value = FirstTextOrMeta(item, "update_time");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(value, out updated);
    }

    private static string NormalizeBaseUrl(string baseUrl)
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

    private static string ToTitle(string segment)
    {
        var text = segment.Replace('-', ' ');
        return string.IsNullOrWhiteSpace(text) ? segment : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string ToJson(IReadOnlyDictionary<string, object?> value)
    {
        return JsonSerializer.Serialize(value.Where(x => x.Value is not null).ToDictionary(x => x.Key, x => x.Value));
    }
}
