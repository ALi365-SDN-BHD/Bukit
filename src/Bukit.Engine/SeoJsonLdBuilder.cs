using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static class SeoJsonLdBuilder
{
    internal static IReadOnlyList<string> BuildJsonLd(
        AppConfig config,
        string baseUrl,
        string title,
        string? description,
        string canonical,
        string? image,
        string routeUrl,
        ContentDocument? document,
        IReadOnlyDictionary<string, ContentField>? itemListFields,
        bool isPost,
        bool isCollectionPage,
        SeoGeoMetaParser.ParsedGeoMeta geo,
        string? schemaType,
        ContentRecord? record)
    {
        var result = new List<string>();
        var siteHome = SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, "/");
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
                ["target"] = SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, "/search/?q={search_term_string}"),
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
                    ["name"] = i == segments.Length - 1 ? title : SeoModelBuilder.ToTitle(segments[i]),
                    ["item"] = SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, baseUrl, current + "/")
                });
            }

            result.Add(ToJson(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = items
            }));
        }

        if (isPost && document is not null)
        {
            var effectiveType = schemaType ?? "BlogPosting";
            if (string.Equals(effectiveType, "FAQPage", StringComparison.OrdinalIgnoreCase) && geo.FaqItems is { Count: > 0 })
            {
                BuildFaqPageJsonLd(result, title, description, canonical, image, document, geo.FaqItems);
            }
            else if (string.Equals(effectiveType, "HowTo", StringComparison.OrdinalIgnoreCase) && geo.HowToSteps is { Count: > 0 })
            {
                BuildHowToJsonLd(result, title, description, canonical, image, document, geo.HowToSteps);
            }
            else
            {
                BuildArticleJsonLd(result, effectiveType, title, description, canonical, image, document, geo, record, config.Site.Language);
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
        ContentDocument document,
        SeoGeoMetaParser.ParsedGeoMeta geo,
        ContentRecord? record,
        string? language)
    {
        var article = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = schemaType,
            ["headline"] = title,
            ["description"] = description,
            ["url"] = canonical,
            ["datePublished"] = document.Record.Lifecycle.PublishedAt.ToString("O")
        };
        if (!string.IsNullOrWhiteSpace(image))
        {
            article["image"] = image;
        }

        if (document.Record.Lifecycle.UpdatedAt is { } updated ||
            SeoModelBuilder.TryGetUpdateTime(document, out updated))
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

        var contentLanguage = string.IsNullOrWhiteSpace(record?.Presentation.Language) ||
                              string.Equals(record?.Presentation.Language, "und", StringComparison.OrdinalIgnoreCase)
            ? language
            : record!.Presentation.Language;
        if (!string.IsNullOrWhiteSpace(contentLanguage))
        {
            article["inLanguage"] = contentLanguage;
        }

        var author = geo.GeoAuthor?.Name ?? record?.Ownership.Author ?? SeoModelBuilder.FirstTextField(document.Fields, "author");
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

        var tags = record?.Classification.Tags.Count > 0
            ? record.Classification.Tags
            : ContentFieldReader.GetTextList(document.Fields, "tags");
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
        ContentDocument document,
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
            ["datePublished"] = document.Record.Lifecycle.PublishedAt.ToString("O"),
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
        ContentDocument document,
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
            ["datePublished"] = document.Record.Lifecycle.PublishedAt.ToString("O"),
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

    internal static IReadOnlyDictionary<string, object?>? BuildItemList(
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
                entry["url"] = SeoModelBuilder.BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, url);
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
