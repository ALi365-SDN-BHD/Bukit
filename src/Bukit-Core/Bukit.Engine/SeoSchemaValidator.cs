using System.Text.Json;

namespace Bukit.Engine;

internal static class SeoSchemaValidator
{
    internal static bool IsSupportedArticleAuthorType(string? type)
        => type is "Person" or "Organization";

    internal static bool IsSupportedProfileAuthorType(string? type)
        => string.Equals(type, "Person", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(type, "Organization", StringComparison.OrdinalIgnoreCase);

    internal static IReadOnlyList<string> ExtractSchemaTypes(
        IReadOnlyList<string> jsonLd,
        string routeUrl,
        List<SeoAuditIssue> issues,
        bool searchActionExpected = false)
    {
        var types = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var json in jsonLd)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                ExtractSchemaTypes(doc.RootElement, types);
                ValidateSchemaObject(doc.RootElement, routeUrl, issues, searchActionExpected);
                if (types.Count == 0)
                {
                    issues.Add(Warning("seo.json_ld_type_missing", routeUrl, "JSON-LD does not declare @type."));
                }
            }
            catch (JsonException ex)
            {
                issues.Add(Error("seo.json_ld_invalid", routeUrl, $"JSON-LD is not valid JSON: {ex.Message}"));
            }
        }

        return types.ToArray();
    }

    private static void ExtractSchemaTypes(JsonElement element, ISet<string> types)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@type", out var type))
            {
                if (type.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(type.GetString()))
                {
                    types.Add(type.GetString()!);
                }
                else if (type.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in type.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String))
                    {
                        types.Add(item.GetString()!);
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                ExtractSchemaTypes(property.Value, types);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ExtractSchemaTypes(item, types);
            }
        }
    }

    internal static void ValidateSchemaObject(
        JsonElement element,
        string routeUrl,
        List<SeoAuditIssue> issues,
        bool searchActionExpected = false)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            ValidateSchemaNode(element, routeUrl, issues, searchActionExpected);
            foreach (var property in element.EnumerateObject())
            {
                ValidateSchemaObject(property.Value, routeUrl, issues, searchActionExpected);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateSchemaObject(item, routeUrl, issues, searchActionExpected);
            }
        }
    }

    private static void ValidateSchemaNode(
        JsonElement node,
        string routeUrl,
        List<SeoAuditIssue> issues,
        bool searchActionExpected)
    {
        foreach (var type in ReadTypes(node))
        {
            switch (type)
            {
                case "WebSite":
                    if (node.TryGetProperty("@context", out _) ||
                        node.TryGetProperty("potentialAction", out _))
                    {
                        ValidateWebSite(node, routeUrl, issues, searchActionExpected);
                    }
                    break;
                case "BlogPosting":
                case "Article":
                case "NewsArticle":
                    ValidateArticle(node, type, routeUrl, issues);
                    break;
                case "ItemList":
                    ValidateItemList(node, routeUrl, issues);
                    break;
                case "BreadcrumbList":
                    ValidateBreadcrumbList(node, routeUrl, issues);
                    break;
            }
        }
    }

    private static IReadOnlyList<string> ReadTypes(JsonElement node)
    {
        if (!node.TryGetProperty("@type", out var type))
        {
            return Array.Empty<string>();
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            var value = type.GetString();
            return string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value! };
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            return type.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                .Select(x => x.GetString()!)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static void ValidateWebSite(
        JsonElement node,
        string routeUrl,
        List<SeoAuditIssue> issues,
        bool searchActionExpected)
    {
        if (!HasNonEmptyString(node, "name"))
        {
            issues.Add(Warning("seo.schema_website_name_missing", routeUrl, "WebSite JSON-LD should include a non-empty name."));
        }

        if (!HasAbsoluteUrl(node, "url"))
        {
            issues.Add(Warning("seo.schema_website_url_invalid", routeUrl, "WebSite JSON-LD should include an absolute url."));
        }

        if (!node.TryGetProperty("@context", out _) &&
            !node.TryGetProperty("potentialAction", out _))
        {
            return;
        }

        if (!node.TryGetProperty("potentialAction", out var action))
        {
            if (searchActionExpected)
            {
                issues.Add(Warning("seo.schema_website_searchaction_missing", routeUrl, "WebSite JSON-LD should include potentialAction SearchAction when site search is enabled."));
            }

            return;
        }

        ValidateSearchAction(action, routeUrl, issues);
    }

    private static void ValidateSearchAction(JsonElement action, string routeUrl, List<SeoAuditIssue> issues)
    {
        if (action.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in action.EnumerateArray())
            {
                if (IsSchemaType(item, "SearchAction"))
                {
                    ValidateSearchAction(item, routeUrl, issues);
                    return;
                }
            }

            issues.Add(Warning("seo.schema_searchaction_missing", routeUrl, "WebSite potentialAction does not contain a SearchAction."));
            return;
        }

        if (action.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Warning("seo.schema_searchaction_invalid", routeUrl, "WebSite potentialAction must be an object or array."));
            return;
        }

        if (!IsSchemaType(action, "SearchAction"))
        {
            issues.Add(Warning("seo.schema_searchaction_type_missing", routeUrl, "WebSite potentialAction should declare @type SearchAction."));
        }

        if (!HasNonEmptyString(action, "target"))
        {
            issues.Add(Warning("seo.schema_searchaction_target_missing", routeUrl, "SearchAction should include a non-empty target."));
        }
        else if (!HasAbsoluteUrl(action, "target"))
        {
            issues.Add(Warning("seo.schema_searchaction_target_not_absolute", routeUrl, "SearchAction target should be an absolute URL."));
        }

        if (!HasNonEmptyString(action, "query-input"))
        {
            issues.Add(Warning("seo.schema_searchaction_query_input_missing", routeUrl, "SearchAction should include query-input."));
        }
    }

    private static void ValidateArticle(JsonElement node, string type, string routeUrl, List<SeoAuditIssue> issues)
    {
        var prefix = type switch
        {
            "BlogPosting" => "seo.schema_blogposting",
            "NewsArticle" => "seo.schema_newsarticle",
            _ => "seo.schema_article"
        };

        if (!HasNonEmptyString(node, "headline"))
        {
            issues.Add(Error($"{prefix}_headline_missing", routeUrl, $"{type} JSON-LD must include headline."));
        }

        if (!HasNonEmptyString(node, "datePublished"))
        {
            issues.Add(Error($"{prefix}_date_published_missing", routeUrl, $"{type} JSON-LD must include datePublished."));
        }

        if (!node.TryGetProperty("author", out var author) || IsEmptySchemaValue(author))
        {
            issues.Add(Warning($"{prefix}_author_missing", routeUrl, $"{type} JSON-LD should include author."));
        }
        else
        {
            ValidateArticleAuthor(author, prefix, routeUrl, issues);
        }

        if (!node.TryGetProperty("image", out var image) || IsEmptySchemaValue(image))
        {
            issues.Add(Warning($"{prefix}_image_missing", routeUrl, $"{type} JSON-LD should include image."));
        }

        if (!node.TryGetProperty("publisher", out var publisher) || IsEmptySchemaValue(publisher))
        {
            issues.Add(Warning($"{prefix}_publisher_missing", routeUrl,
                $"{type} JSON-LD should include publisher when publisher identity is available."));
        }
        else if (publisher.ValueKind != JsonValueKind.Object ||
                 !HasSupportedPublisherType(publisher) ||
                 !HasNonEmptyString(publisher, "name"))
        {
            issues.Add(Warning($"{prefix}_publisher_type_invalid", routeUrl,
                $"{type} publisher should be an Organization or NewsMediaOrganization with a non-empty name."));
        }
    }

    private static bool HasSupportedPublisherType(JsonElement publisher)
        => ReadTypes(publisher).Any(publisherType =>
            publisherType is "Organization" or "NewsMediaOrganization");

    private static void ValidateArticleAuthor(
        JsonElement author,
        string prefix,
        string routeUrl,
        List<SeoAuditIssue> issues)
    {
        if (author.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in author.EnumerateArray())
            {
                ValidateArticleAuthor(item, prefix, routeUrl, issues);
            }

            return;
        }

        if (author.ValueKind == JsonValueKind.String)
        {
            if (string.IsNullOrWhiteSpace(author.GetString()))
            {
                issues.Add(Warning($"{prefix}_author_name_missing", routeUrl, "Article author should include a non-empty name."));
            }

            issues.Add(Warning($"{prefix}_author_type_missing", routeUrl, "Article author should declare @type Person or Organization."));
            return;
        }

        if (author.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Error($"{prefix}_author_type_invalid", routeUrl, "Article author must be a Person or Organization object."));
            return;
        }

        var types = ReadTypes(author);
        if (types.Count == 0)
        {
            issues.Add(Warning($"{prefix}_author_type_missing", routeUrl, "Article author should declare @type Person or Organization."));
        }
        else if (types.Any(authorType => !IsSupportedArticleAuthorType(authorType)))
        {
            issues.Add(Error($"{prefix}_author_type_invalid", routeUrl, "Article author @type must be Person or Organization."));
        }

        if (!HasNonEmptyString(author, "name"))
        {
            issues.Add(Warning($"{prefix}_author_name_missing", routeUrl, "Article author should include a non-empty name."));
        }
    }

    private static void ValidateItemList(JsonElement node, string routeUrl, List<SeoAuditIssue> issues)
    {
        if (!node.TryGetProperty("itemListElement", out var elements) ||
            elements.ValueKind != JsonValueKind.Array ||
            elements.GetArrayLength() == 0)
        {
            issues.Add(Error("seo.schema_itemlist_elements_missing", routeUrl, "ItemList JSON-LD must include a non-empty itemListElement array."));
            return;
        }

        var index = 0;
        foreach (var item in elements.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("seo.schema_itemlist_item_invalid", routeUrl, $"ItemList item #{index} must be an object."));
                continue;
            }

            if (!item.TryGetProperty("position", out var position) || position.ValueKind != JsonValueKind.Number)
            {
                issues.Add(Error("seo.schema_itemlist_position_missing", routeUrl, $"ItemList item #{index} must include numeric position."));
            }

            if (!HasNonEmptyString(item, "name"))
            {
                issues.Add(Error("seo.schema_itemlist_name_missing", routeUrl, $"ItemList item #{index} must include name."));
            }

            if (!HasAbsoluteUrl(item, "url") && !HasAbsoluteUrl(item, "item"))
            {
                issues.Add(Warning("seo.schema_itemlist_url_missing", routeUrl, $"ItemList item #{index} should include an absolute url or item."));
            }
        }
    }

    private static void ValidateBreadcrumbList(JsonElement node, string routeUrl, List<SeoAuditIssue> issues)
    {
        if (!node.TryGetProperty("itemListElement", out var elements) ||
            elements.ValueKind != JsonValueKind.Array ||
            elements.GetArrayLength() == 0)
        {
            issues.Add(Error(
                "seo.schema_breadcrumb_elements_missing",
                routeUrl,
                "BreadcrumbList JSON-LD must include a non-empty itemListElement array."));
            return;
        }

        var expectedPosition = 0;
        foreach (var item in elements.EnumerateArray())
        {
            expectedPosition++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error(
                    "seo.schema_breadcrumb_item_invalid",
                    routeUrl,
                    $"BreadcrumbList item #{expectedPosition} must be an object."));
                continue;
            }

            if (!IsSchemaType(item, "ListItem"))
            {
                issues.Add(Error(
                    "seo.schema_breadcrumb_item_type_invalid",
                    routeUrl,
                    $"BreadcrumbList item #{expectedPosition} must declare @type ListItem."));
            }

            if (!item.TryGetProperty("position", out var position) ||
                position.ValueKind != JsonValueKind.Number ||
                !position.TryGetInt32(out var positionValue) ||
                positionValue != expectedPosition)
            {
                issues.Add(Error(
                    "seo.schema_breadcrumb_position_invalid",
                    routeUrl,
                    $"BreadcrumbList item #{expectedPosition} must use consecutive position {expectedPosition}."));
            }

            if (!HasNonEmptyString(item, "name"))
            {
                issues.Add(Error(
                    "seo.schema_breadcrumb_name_missing",
                    routeUrl,
                    $"BreadcrumbList item #{expectedPosition} must include a non-empty name."));
            }

            ValidateBreadcrumbItemUrl(item, expectedPosition, routeUrl, issues);
        }
    }

    private static void ValidateBreadcrumbItemUrl(
        JsonElement item,
        int position,
        string routeUrl,
        List<SeoAuditIssue> issues)
    {
        if (!HasNonEmptyString(item, "item"))
        {
            issues.Add(Error(
                "seo.schema_breadcrumb_item_url_missing",
                routeUrl,
                $"BreadcrumbList item #{position} must include a non-empty item URL."));
            return;
        }

        var value = item.GetProperty("item").GetString()!;
        if (SeoAuditReportWriter.IsAbsoluteHttpUrl(value))
        {
            return;
        }

        if (IsValidInternalRelativeUrl(value))
        {
            issues.Add(Warning(
                "seo.schema_breadcrumb_item_url_not_absolute",
                routeUrl,
                $"BreadcrumbList item #{position} uses a relative item URL because site.url is unavailable."));
            return;
        }

        issues.Add(Error(
            "seo.schema_breadcrumb_item_url_invalid",
            routeUrl,
            $"BreadcrumbList item #{position} must include an absolute HTTP(S) URL or an internal relative path."));
    }

    private static bool IsValidInternalRelativeUrl(string value)
        => value.StartsWith("/", StringComparison.Ordinal) &&
           !value.StartsWith("//", StringComparison.Ordinal) &&
           !value.Any(char.IsControl) &&
           !value.Contains('\\') &&
           Uri.TryCreate(value, UriKind.Relative, out _);

    private static bool HasNonEmptyString(JsonElement node, string property)
        => node.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasAbsoluteUrl(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return SeoAuditReportWriter.IsAbsoluteHttpUrl(value.GetString() ?? string.Empty);
        }

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("@id", out var id) &&
            id.ValueKind == JsonValueKind.String)
        {
            return SeoAuditReportWriter.IsAbsoluteHttpUrl(id.GetString() ?? string.Empty);
        }

        return false;
    }

    private static bool IsSchemaType(JsonElement node, string expectedType)
    {
        return node.ValueKind == JsonValueKind.Object &&
               ReadTypes(node).Any(x => string.Equals(x, expectedType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEmptySchemaValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.Object => !value.EnumerateObject().Any(),
            _ => false
        };

    private static SeoAuditIssue Error(string code, string? route, string message) => new("error", code, route, message);

    private static SeoAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}
