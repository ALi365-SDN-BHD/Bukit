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
        var isCollectionPage = !isPost && IsCollectionLikePage(item);
        TryGetUpdateTime(item, out var updated);
        var author = FirstTextOrMeta(item, "author");
        var tags = GetStringList(item.Meta, "tags") ?? Array.Empty<string>();
        var jsonLd = BuildJsonLd(config, baseUrl, title, description, canonical, image, route.Url, item, item.Fields, isPost, isCollectionPage);

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
                Type = isPost ? "article" : "website",
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
                PublishedTime = isPost ? item.PublishAt : null,
                ModifiedTime = isPost && updated != default ? updated : null,
                Author = isPost ? author : null,
                Tags = isPost ? tags : Array.Empty<string>()
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
            JsonLd = BuildJsonLd(config, baseUrl, title, description, canonical, image, page.Url, item: null, itemListFields: page.Fields, isPost: false, isCollectionPage: page.Url != "/")
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

    private static IReadOnlyList<string> BuildJsonLd(
        AppConfig config,
        string baseUrl,
        string title,
        string? description,
        string canonical,
        string? image,
        string routeUrl,
        ContentItem? item,
        IReadOnlyDictionary<string, ContentField>? itemListFields,
        bool isPost,
        bool isCollectionPage)
    {
        var result = new List<string>();
        var siteHome = BuildAbsoluteUrl(config.Site.Url, baseUrl, "/");
        var website = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = config.Site.Title,
            ["url"] = siteHome
        };

        if (config.Site.Seo.Schema.SearchAction)
        {
            website["potentialAction"] = new Dictionary<string, object?>
            {
                ["@type"] = "SearchAction",
                ["target"] = BuildAbsoluteUrl(config.Site.Url, baseUrl, "/search/?q={search_term_string}"),
                ["query-input"] = "required name=search_term_string"
            };
        }

        result.Add(ToJson(website));

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

        if (config.Site.Seo.Schema.WebPage)
        {
            result.Add(ToJson(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = isCollectionPage && config.Site.Seo.Schema.CollectionPage ? "CollectionPage" : "WebPage",
                ["name"] = title,
                ["description"] = description,
                ["url"] = canonical,
                ["isPartOf"] = new Dictionary<string, object?>
                {
                    ["@type"] = "WebSite",
                    ["name"] = config.Site.Title,
                    ["url"] = siteHome
                }
            }));
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
                    ["item"] = BuildAbsoluteUrl(config.Site.Url, baseUrl, current + "/")
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

            var tags = GetStringList(item.Meta, "tags");
            if (tags is { Count: > 0 })
            {
                article["keywords"] = tags;
            }

            result.Add(ToJson(article));
        }

        var itemList = BuildItemList(config, baseUrl, itemListFields);
        if (isCollectionPage && itemList is not null)
        {
            result.Add(ToJson(itemList));
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

    private static bool IsCollectionLikePage(ContentItem item)
        => item.Fields is not null &&
           (item.Fields.ContainsKey("items") || item.Fields.ContainsKey("terms"));

    private static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object> meta, string key)
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

    private static IReadOnlyDictionary<string, object?>? BuildItemList(
        AppConfig config,
        string baseUrl,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return null;
        }

        if (!TryGetListField(fields, "items", out var values) &&
            !TryGetListField(fields, "terms", out values))
        {
            return null;
        }

        var elements = new List<Dictionary<string, object?>>();
        for (var i = 0; i < values.Count; i++)
        {
            if (!TryReadListEntry(values[i], out var name, out var url))
            {
                continue;
            }

            var entry = new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = elements.Count + 1,
                ["name"] = name
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                entry["url"] = BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, url);
            }

            elements.Add(entry);
        }

        if (elements.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "ItemList",
            ["itemListElement"] = elements
        };
    }

    private static bool TryGetListField(IReadOnlyDictionary<string, ContentField> fields, string key, out IReadOnlyList<object> values)
    {
        values = Array.Empty<object>();
        if (!fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return false;
        }

        if (field.Value is IReadOnlyList<object> readOnlyObjects)
        {
            values = readOnlyObjects;
            return values.Count > 0;
        }

        if (field.Value is IEnumerable<object> objects)
        {
            values = objects.ToList();
            return values.Count > 0;
        }

        return false;
    }

    private static bool TryReadListEntry(object value, out string? name, out string? url)
    {
        name = null;
        url = null;

        if (value is IReadOnlyDictionary<string, object> readOnly)
        {
            name = ReadMapString(readOnly, "title") ?? ReadMapString(readOnly, "name") ?? ReadMapString(readOnly, "term");
            url = ReadMapString(readOnly, "url") ?? ReadMapString(readOnly, "href");
            return !string.IsNullOrWhiteSpace(name);
        }

        if (value is IDictionary<string, object> dict)
        {
            name = ReadMapString(dict, "title") ?? ReadMapString(dict, "name") ?? ReadMapString(dict, "term");
            url = ReadMapString(dict, "url") ?? ReadMapString(dict, "href");
            return !string.IsNullOrWhiteSpace(name);
        }

        name = value.ToString();
        return !string.IsNullOrWhiteSpace(name);
    }

    private static string? ReadMapString(IReadOnlyDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static string? ReadMapString(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

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
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteJsonValue(writer, value);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case int integer:
                writer.WriteNumberValue(integer);
                break;
            case long integer:
                writer.WriteNumberValue(integer);
                break;
            case DateTimeOffset timestamp:
                writer.WriteStringValue(timestamp);
                break;
            case IReadOnlyDictionary<string, object?> map:
                writer.WriteStartObject();
                foreach (var (key, child) in map)
                {
                    if (child is null)
                    {
                        continue;
                    }

                    writer.WritePropertyName(key);
                    WriteJsonValue(writer, child);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable<object?> values:
                writer.WriteStartArray();
                foreach (var item in values)
                {
                    WriteJsonValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
