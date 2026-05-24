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
        var geo = ParseGeoMeta(item);
        var schemaType = geo.SchemaType ?? (isPost ? "BlogPosting" : null);
        var jsonLd = BuildJsonLd(config, baseUrl, title, description, canonical, image, route.Url, item, item.Fields, isPost, isCollectionPage, geo, schemaType);

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
            JsonLd = BuildJsonLd(config, baseUrl, title, description, canonical, image, page.Url, item: null, itemListFields: page.Fields, isPost: false, isCollectionPage: page.Url != "/", geo: ParsedGeoMeta.Empty, schemaType: null)
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
        bool isCollectionPage,
        ParsedGeoMeta geo,
        string? schemaType)
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
            var effectiveType = schemaType ?? "BlogPosting";
            if (string.Equals(effectiveType, "FAQPage", StringComparison.OrdinalIgnoreCase) && geo.FaqItems is { Count: > 0 })
            {
                BuildFaqPageJsonLd(result, title, description, canonical, image, item, geo.FaqItems);
            }
            else if (string.Equals(effectiveType, "HowTo", StringComparison.OrdinalIgnoreCase) && geo.HowToSteps is { Count: > 0 })
            {
                BuildHowToJsonLd(result, title, description, canonical, image, item, geo.HowToSteps);
            }
            else
            {
                BuildArticleJsonLd(result, effectiveType, title, description, canonical, image, item, geo, config.Site.Language);
            }
        }

        if (geo.GeoAuthor is not null)
        {
            BuildPersonJsonLd(result, geo.GeoAuthor);
        }

        if (geo.Citations is { Count: > 0 })
        {
            BuildCitationsJsonLd(result, canonical, geo.Citations);
        }

        if (!string.IsNullOrWhiteSpace(geo.SpeakableXPath))
        {
            BuildSpeakableJsonLd(result, canonical, geo.SpeakableXPath);
        }

        var itemList = BuildItemList(config, baseUrl, itemListFields);
        if (isCollectionPage && itemList is not null)
        {
            result.Add(ToJson(itemList));
        }

        return result;
    }

    private static void BuildArticleJsonLd(
        List<string> result,
        string schemaType,
        string title,
        string? description,
        string canonical,
        string? image,
        ContentItem item,
        ParsedGeoMeta geo,
        string? language)
    {
        var article = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = schemaType,
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

        if (geo.DateReviewed.HasValue)
        {
            article["dateReviewed"] = geo.DateReviewed.Value.ToString("O");
        }

        if (!string.IsNullOrWhiteSpace(geo.About))
        {
            article["about"] = geo.About;
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            article["inLanguage"] = language;
        }

        var author = geo.GeoAuthor?.Name ?? FirstTextOrMeta(item, "author");
        if (!string.IsNullOrWhiteSpace(author))
        {
            var person = new Dictionary<string, object?>
            {
                ["@type"] = "Person",
                ["name"] = author
            };
            if (geo.GeoAuthor?.Url is { } authorUrl)
            {
                person["url"] = authorUrl;
            }

            if (geo.GeoAuthor?.SameAs is { Count: > 0 })
            {
                person["sameAs"] = geo.GeoAuthor.SameAs;
            }

            article["author"] = person;
        }

        if (geo.SameAs is { Count: > 0 })
        {
            article["sameAs"] = geo.SameAs;
        }

        var tags = GetStringList(item.Meta, "tags");
        if (tags is { Count: > 0 })
        {
            article["keywords"] = tags;
        }

        result.Add(ToJson(article));
    }

    private static void BuildFaqPageJsonLd(
        List<string> result,
        string title,
        string? description,
        string canonical,
        string? image,
        ContentItem item,
        IReadOnlyList<GeoFaqModel> faqItems)
    {
        var mainEntity = new List<Dictionary<string, object?>>();
        foreach (var faq in faqItems)
        {
            mainEntity.Add(new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = faq.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer",
                    ["text"] = faq.Answer
                }
            });
        }

        var faqPage = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["headline"] = title,
            ["description"] = description,
            ["url"] = canonical,
            ["datePublished"] = item.PublishAt.ToString("O"),
            ["mainEntity"] = mainEntity
        };

        if (!string.IsNullOrWhiteSpace(image))
        {
            faqPage["image"] = image;
        }

        result.Add(ToJson(faqPage));
    }

    private static void BuildHowToJsonLd(
        List<string> result,
        string title,
        string? description,
        string canonical,
        string? image,
        ContentItem item,
        IReadOnlyList<GeoHowToStepModel> steps)
    {
        var stepList = new List<Dictionary<string, object?>>();
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var stepEntry = new Dictionary<string, object?>
            {
                ["@type"] = "HowToStep",
                ["position"] = i + 1,
                ["name"] = step.Name,
                ["text"] = step.Text
            };

            if (!string.IsNullOrWhiteSpace(step.Image))
            {
                stepEntry["image"] = step.Image;
            }

            if (!string.IsNullOrWhiteSpace(step.Url))
            {
                stepEntry["url"] = step.Url;
            }

            stepList.Add(stepEntry);
        }

        var howTo = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "HowTo",
            ["name"] = title,
            ["description"] = description ?? title,
            ["url"] = canonical,
            ["datePublished"] = item.PublishAt.ToString("O"),
            ["step"] = stepList
        };

        if (!string.IsNullOrWhiteSpace(image))
        {
            howTo["image"] = image;
        }

        result.Add(ToJson(howTo));
    }

    private static void BuildPersonJsonLd(List<string> result, GeoAuthorModel author)
    {
        var person = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Person",
            ["name"] = author.Name
        };

        if (!string.IsNullOrWhiteSpace(author.Url))
        {
            person["url"] = author.Url;
        }

        if (author.SameAs is { Count: > 0 })
        {
            person["sameAs"] = author.SameAs;
        }

        result.Add(ToJson(person));
    }

    private static void BuildCitationsJsonLd(List<string> result, string canonical, IReadOnlyList<GeoCitationModel> citations)
    {
        var mentionList = new List<Dictionary<string, object?>>();
        foreach (var citation in citations)
        {
            mentionList.Add(new Dictionary<string, object?>
            {
                ["@type"] = "WebPage",
                ["name"] = citation.Title,
                ["url"] = citation.Url
            });
        }

        result.Add(ToJson(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebPage",
            ["url"] = canonical,
            ["mentions"] = mentionList
        }));
    }

    private static void BuildSpeakableJsonLd(List<string> result, string canonical, string xpath)
    {
        result.Add(ToJson(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebPage",
            ["url"] = canonical,
            ["speakable"] = new Dictionary<string, object?>
            {
                ["@type"] = "SpeakableSpecification",
                ["xpath"] = xpath
            }
        }));
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
        var type = MetaHelpers.GetString(item.Meta, "type");
        if (!string.IsNullOrWhiteSpace(type))
        {
            return string.Equals(type, "post", StringComparison.OrdinalIgnoreCase);
        }

        var collection = MetaHelpers.GetString(item.Meta, "collection");
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

    private sealed record ParsedGeoMeta(
        string? SchemaType,
        IReadOnlyList<GeoFaqModel>? FaqItems,
        IReadOnlyList<GeoHowToStepModel>? HowToSteps,
        IReadOnlyList<GeoCitationModel>? Citations,
        GeoAuthorModel? GeoAuthor,
        string? SpeakableXPath,
        IReadOnlyList<string>? SameAs,
        string? About,
        DateTimeOffset? DateReviewed)
    {
        public static readonly ParsedGeoMeta Empty = new(null, null, null, null, null, null, null, null, null);
    }

    private static ParsedGeoMeta ParseGeoMeta(ContentItem item)
    {
        if (!item.Meta.TryGetValue("geo", out var geoValue) || geoValue is not IReadOnlyDictionary<string, object> geo)
        {
            return ParsedGeoMeta.Empty;
        }

        var schemaType = ReadGeoString(geo, "schema_type");
        var speakableXPath = ReadGeoString(geo, "speakable_xpath")
            ?? (geo.TryGetValue("speakable", out var sp) && sp is IReadOnlyDictionary<string, object> spMap
                ? ReadGeoString(spMap, "xpath")
                : null);

        var sameAs = ReadGeoStringList(geo, "same_as");
        var citations = ReadGeoCitations(geo);
        var faqItems = ReadGeoFaqItems(geo);
        var howToSteps = ReadGeoHowToSteps(geo);
        var geoAuthor = ReadGeoAuthor(geo);
        var about = ReadGeoString(geo, "about");
        var dateReviewed = ReadGeoDateTime(geo, "date_reviewed");

        return new ParsedGeoMeta(schemaType, faqItems, howToSteps, citations, geoAuthor, speakableXPath, sameAs, about, dateReviewed);
    }

    private static DateTimeOffset? ReadGeoDateTime(IReadOnlyDictionary<string, object> map, string key)
    {
        var value = ReadGeoString(map, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var dt) ? dt : null;
    }

    private static string? ReadGeoString(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var s = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static IReadOnlyList<string>? ReadGeoStringList(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is IEnumerable<object> seq)
        {
            var list = seq
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
            return list.Count == 0 ? null : list;
        }

        if (value is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        return null;
    }

    private static IReadOnlyList<GeoFaqModel>? ReadGeoFaqItems(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("faq", out var value) || value is not IEnumerable<object> items)
        {
            return null;
        }

        var result = new List<GeoFaqModel>();
        foreach (var item in items)
        {
            if (item is IReadOnlyDictionary<string, object> entry)
            {
                var question = ReadGeoString(entry, "question");
                var answer = ReadGeoString(entry, "answer");
                if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                {
                    result.Add(new GeoFaqModel { Question = question, Answer = answer });
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<GeoHowToStepModel>? ReadGeoHowToSteps(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("steps", out var value) || value is not IEnumerable<object> items)
        {
            return null;
        }

        var result = new List<GeoHowToStepModel>();
        foreach (var item in items)
        {
            if (item is IReadOnlyDictionary<string, object> entry)
            {
                var name = ReadGeoString(entry, "name");
                var text = ReadGeoString(entry, "text");
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new GeoHowToStepModel
                    {
                        Name = name,
                        Text = text,
                        Image = ReadGeoString(entry, "image"),
                        Url = ReadGeoString(entry, "url")
                    });
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<GeoCitationModel>? ReadGeoCitations(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("citations", out var value) || value is not IEnumerable<object> items)
        {
            return null;
        }

        var result = new List<GeoCitationModel>();
        foreach (var item in items)
        {
            if (item is IReadOnlyDictionary<string, object> entry)
            {
                var title = ReadGeoString(entry, "title");
                var url = ReadGeoString(entry, "url");
                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(url))
                {
                    result.Add(new GeoCitationModel { Title = title, Url = url });
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static GeoAuthorModel? ReadGeoAuthor(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("author", out var value) || value is not IReadOnlyDictionary<string, object> author)
        {
            return null;
        }

        var name = ReadGeoString(author, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new GeoAuthorModel
        {
            Name = name,
            Url = ReadGeoString(author, "url"),
            SameAs = ReadGeoStringList(author, "same_as") ?? Array.Empty<string>()
        };
    }
}
