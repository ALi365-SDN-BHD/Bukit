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
        string contentTitle,
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
        ContentRecord? record,
        ResolvedSeoAuthors? resolvedAuthors = null,
        SearchActionDescriptor? searchAction = null,
        BreadcrumbDescriptor? breadcrumb = null)
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

        if (searchAction is not null)
        {
            website["potentialAction"] = new Dictionary<string, object?>
            {
                ["@type"] = "SearchAction",
                ["target"] = searchAction.Target,
                ["query-input"] = searchAction.QueryInput
            };
        }

        result.Add(ToJson(website));

        var organizationNode = BuildOrganizationNode(config, baseUrl);
        if (organizationNode is not null)
        {
            var standaloneOrganization = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org"
            };
            foreach (var property in organizationNode)
            {
                standaloneOrganization[property.Key] = property.Value;
            }

            result.Add(ToJson(standaloneOrganization));
        }

        if (config.Site.Seo.Schema.WebPage)
        {
            result.Add(ToJson(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = isCollectionPage && config.Site.Seo.Schema.CollectionPage ? "CollectionPage" : "WebPage",
                ["name"] = contentTitle,
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

        if (breadcrumb is not null)
        {
            var items = breadcrumb.Items
                .Select((item, index) => new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = index + 1,
                    ["name"] = item.Name,
                    ["item"] = item.Item
                })
                .ToList();

            result.Add(ToJson(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = items
            }));
        }

        var geoAuthorMergedIntoArticle = false;
        if (isPost && document is not null)
        {
            var effectiveType = schemaType ?? "BlogPosting";
            var effectiveAuthors = resolvedAuthors
                ?? SeoAuthorResolver.Resolve(record, document.CustomFields, geo.GeoAuthor);
            if (string.Equals(effectiveType, "FAQPage", StringComparison.OrdinalIgnoreCase) && geo.FaqItems is { Count: > 0 })
            {
                BuildFaqPageJsonLd(result, contentTitle, description, canonical, image, document, geo.FaqItems);
                geoAuthorMergedIntoArticle = effectiveAuthors.UsesAuthorRelation;
            }
            else if (string.Equals(effectiveType, "HowTo", StringComparison.OrdinalIgnoreCase) && geo.HowToSteps is { Count: > 0 })
            {
                BuildHowToJsonLd(result, contentTitle, description, canonical, image, document, geo.HowToSteps);
                geoAuthorMergedIntoArticle = effectiveAuthors.UsesAuthorRelation;
            }
            else
            {
                geoAuthorMergedIntoArticle = BuildArticleJsonLd(
                    result,
                    effectiveType,
                    contentTitle,
                    description,
                    canonical,
                    image,
                    document,
                    geo,
                    record,
                    config.Site.Language,
                    organizationNode,
                    effectiveAuthors,
                    config.Site.Url,
                    baseUrl);
            }
        }

        if (!isPost && record is not null && IsCompanyContent(record, schemaType))
        {
            var entity = BuildCompanyEntityNode(record, schemaType, config.Site.Url, baseUrl);
            if (entity is not null && !MatchesOrganization(organizationNode, entity))
            {
                result.Add(ToJson(entity));
            }
        }

        if (geo.GeoAuthor is not null && !geoAuthorMergedIntoArticle)
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

    private static bool BuildArticleJsonLd(
        List<string> result,
        string schemaType,
        string title,
        string? description,
        string canonical,
        string? image,
        ContentDocument document,
        SeoGeoMetaParser.ParsedGeoMeta geo,
        ContentRecord? record,
        string? language,
        IReadOnlyDictionary<string, object?>? organizationNode,
        ResolvedSeoAuthors resolvedAuthors,
        string? siteUrl,
        string baseUrl)
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

        if (organizationNode is not null && IsArticleFamilyType(schemaType))
        {
            article["publisher"] = organizationNode;
        }

        article["mainEntityOfPage"] = new Dictionary<string, object?>
        {
            ["@type"] = "WebPage",
            ["@id"] = canonical
        };

        if (geo.Citations is { Count: > 0 })
        {
            var citationNodes = geo.Citations
                .Select(BuildCitationNode)
                .ToArray();
            article["citation"] = citationNodes;

            var basedOnNodes = geo.Citations
                .Where(citation => string.Equals(citation.Relation, "based-on", StringComparison.Ordinal))
                .Select(BuildCitationNode)
                .ToArray();
            if (basedOnNodes.Length > 0)
            {
                article["isBasedOn"] = basedOnNodes;
            }
        }

        if (resolvedAuthors.Authors.Count > 0)
        {
            var authorNodes = resolvedAuthors.Authors
                .Select(author => BuildAuthorNode(author, siteUrl, baseUrl))
                .ToArray();
            article["author"] = authorNodes.Length == 1
                ? authorNodes[0]
                : authorNodes;
        }

        if (geo.SameAs is { Count: > 0 })
        {
            article["sameAs"] = geo.SameAs;
        }

        var tags = record?.Classification.Tags.Count > 0
            ? record.Classification.Tags
            : ContentFieldReader.GetTextList(document.CustomFields, "tags");
        if (tags is { Count: > 0 })
        {
            article["keywords"] = tags;
        }

        result.Add(ToJson(article));
        return resolvedAuthors.SuppressStandaloneGeoAuthor;
    }

    private static bool IsArticleFamilyType(string schemaType)
        => string.Equals(schemaType, "Article", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(schemaType, "BlogPosting", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(schemaType, "NewsArticle", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object?> BuildAuthorNode(
        ResolvedSeoAuthor author,
        string? siteUrl,
        string baseUrl)
    {
        var node = new Dictionary<string, object?>
        {
            ["@type"] = author.SchemaType,
            ["name"] = author.Name
        };
        if (!string.IsNullOrWhiteSpace(author.Url))
        {
            node["url"] = SeoModelBuilder.BuildMaybeAbsoluteUrl(siteUrl, baseUrl, author.Url);
        }

        if (!string.IsNullOrWhiteSpace(author.Image))
        {
            node["image"] = SeoModelBuilder.BuildMaybeAbsoluteUrl(siteUrl, baseUrl, author.Image);
        }

        if (author.SameAs.Count > 0)
        {
            node["sameAs"] = author.SameAs;
        }

        return node;
    }

    private static IReadOnlyDictionary<string, object?>? BuildOrganizationNode(
        AppConfig config,
        string baseUrl)
    {
        if (config.Site.Seo.Organization is not { } organization ||
            (string.IsNullOrWhiteSpace(organization.Name) &&
             string.IsNullOrWhiteSpace(organization.Url) &&
             string.IsNullOrWhiteSpace(organization.Logo) &&
             organization.SameAs.Count == 0))
        {
            return null;
        }

        var node = new Dictionary<string, object?>
        {
            ["@type"] = organization.Type is "Organization" or "NewsMediaOrganization"
                ? organization.Type
                : "Organization",
            ["name"] = string.IsNullOrWhiteSpace(organization.Name)
                ? config.Site.Title
                : organization.Name.Trim()
        };

        var url = BuildAbsoluteHttpUrl(config.Site.Url, baseUrl, organization.Url);
        if (url is not null)
        {
            node["url"] = url;
        }

        var logo = BuildAbsoluteHttpUrl(config.Site.Url, baseUrl, organization.Logo);
        if (logo is not null)
        {
            node["logo"] = logo;
        }

        var sameAs = organization.SameAs
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();
        if (sameAs.Length > 0)
        {
            node["sameAs"] = sameAs;
        }

        return node;
    }

    private static bool IsCompanyContent(ContentRecord record, string? schemaType)
        => string.Equals(record.Identity.ContentType, "company", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(record.Classification.Type, "company", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(record.Classification.Collection, "companies", StringComparison.OrdinalIgnoreCase) ||
           record.Entities.Any(entity => string.Equals(entity.Type, "company", StringComparison.OrdinalIgnoreCase)) ||
           string.Equals(schemaType, "Organization", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(schemaType, "LocalBusiness", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object?>? BuildCompanyEntityNode(
        ContentRecord record,
        string? schemaType,
        string? siteUrl,
        string baseUrl)
    {
        var entity = PublicContentProjectionPolicy.SanitizeEntities(record)
            .FirstOrDefault(item => string.Equals(item.Type, "company", StringComparison.OrdinalIgnoreCase));
        if (entity is null || string.IsNullOrWhiteSpace(entity.Name))
        {
            return null;
        }

        var profile = entity.LocalBusinessProfile;
        var useLocalBusiness = string.Equals(schemaType, "LocalBusiness", StringComparison.OrdinalIgnoreCase) &&
                               profile?.HasCompleteVerifiedLocalOperations == true;
        var node = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = useLocalBusiness ? "LocalBusiness" : "Organization",
            ["name"] = entity.Name.Trim()
        };

        var description = useLocalBusiness
            ? profile!.LocalOperationsDescription
            : entity.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description.Trim();
        }

        var url = BuildAbsoluteHttpUrl(siteUrl, baseUrl, entity.Url);
        if (url is not null)
        {
            node["url"] = url;
        }

        var sameAs = (entity.SameAs ?? Array.Empty<string>())
            .Select(value => BuildAbsoluteHttpUrl(siteUrl, baseUrl, value))
            .Where(static value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (sameAs.Length > 0)
        {
            node["sameAs"] = sameAs;
        }

        if (useLocalBusiness)
        {
            node["address"] = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = profile!.StreetAddress!.Trim(),
                ["addressLocality"] = profile.AddressLocality!.Trim(),
                ["addressRegion"] = profile.AddressRegion!.Trim(),
                ["postalCode"] = profile.PostalCode!.Trim(),
                ["addressCountry"] = profile.AddressCountry!.Trim()
            };
        }

        return node;
    }

    private static bool MatchesOrganization(
        IReadOnlyDictionary<string, object?>? siteOrganization,
        IReadOnlyDictionary<string, object?> companyOrganization)
    {
        if (siteOrganization is null ||
            !TryGetText(siteOrganization, "@type", out var siteType) ||
            !TryGetText(companyOrganization, "@type", out var companyType) ||
            !string.Equals(siteType, companyType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasSiteUrl = TryGetText(siteOrganization, "url", out var siteUrl);
        var hasCompanyUrl = TryGetText(companyOrganization, "url", out var companyUrl);
        if (hasSiteUrl && hasCompanyUrl)
        {
            return string.Equals(siteUrl, companyUrl, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool TryGetText(IReadOnlyDictionary<string, object?> node, string key, out string value)
    {
        if (node.TryGetValue(key, out var raw) && raw is string text && !string.IsNullOrWhiteSpace(text))
        {
            value = text.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? BuildAbsoluteHttpUrl(string? siteUrl, string baseUrl, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var schemeSeparator = trimmed.IndexOf(':');
        if (schemeSeparator > 0 &&
            char.IsLetter(trimmed[0]) &&
            trimmed[..schemeSeparator].All(static character =>
                char.IsLetterOrDigit(character) || character is '+' or '-' or '.'))
        {
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var configuredUri) &&
                   configuredUri.Scheme is "http" or "https"
                ? trimmed
                : null;
        }

        var candidate = SeoModelBuilder.BuildMaybeAbsoluteUrl(siteUrl, baseUrl, trimmed);
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https"
            ? candidate
            : null;
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
            mentionList.Add(BuildCitationNode(citation));
        }

        result.Add(ToJson(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebPage",
            ["url"] = canonical,
            ["mentions"] = mentionList
        }));
    }

    private static Dictionary<string, object?> BuildCitationNode(GeoCitationModel citation)
        => new()
        {
            ["@type"] = "WebPage",
            ["name"] = citation.Title,
            ["url"] = citation.Url
        };

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
