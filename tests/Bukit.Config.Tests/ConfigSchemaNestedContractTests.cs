using System.Text.Json;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigSchemaNestedContractTests
{
    private static readonly SchemaCase[] NestedCases =
    [
        new("site/seo", "enabled", "renderMode", "diagnostics", "homeTitleTemplate", "pageTitleTemplate", "titleSeparator", "defaultImage", "twitterSite", "organization", "robotsTxt", "schema", "geo"),
        new("site/seo/organization", "type", "name", "url", "logo", "sameAs"),
        new("site/seo/robotsTxt", "enabled"),
        new("site/seo/schema", "webPage", "collectionPage", "searchAction"),
        new("site/seo/geo", "enabled", "llmsTxt", "llmsFullTxt", "llmsTxtMaxArticles", "aiBotMode", "aiBotAllowList", "aiBotBlockList", "llmsTxtOptionalLinks"),
        new("site/seo/geo/llmsTxtOptionalLinks[]", "title", "url", "description"),
        new("site/analytics", "enabled", "productionOnly", "consent", "csp", "providers"),
        new("site/analytics/csp", "mode"),
        new("site/analytics/consent", "google"),
        new("site/analytics/consent/google", "mode", "defaults", "waitForUpdateMs"),
        new("site/analytics/consent/google/defaults", "adStorage", "analyticsStorage", "adUserData", "adPersonalization"),
        new("site/feed", "mode", "formats", "limit", "path"),
        new("site/search", "mode", "route", "ui", "uiTheme", "placeholderText", "maxContentLength"),
        new("site/related", "enabled", "threshold", "limit", "indices"),
        new("site/related/indices[]", "name", "weight"),
        new("site/sitemapDetail", "defaultPriority", "defaultChangefreq", "imageEnabled", "videoEnabled"),
        new("site/pagination", "enabled", "pageSize"),
        new("site/collections/*", "permalink", "template", "listRoute", "listTitle", "listDescription", "listTemplate", "schemaFailMode", "noindexWhenEmpty", "indexPolicy", "pagination", "output", "filteredLists"),
        new("site/collections/*/indexPolicy", "minimumItems", "belowMinimum"),
        new("site/collections/*/pagination", "enabled", "pageSize", "urlPattern", "firstPageUsesListRoute"),
        new("site/collections/*/output", "rss", "sitemap", "archive", "feedPath", "feedTitle", "feedDescription", "archiveDetail"),
        new("site/collections/*/output/archiveDetail", "depth", "template", "routePrefix"),
        new("site/collections/*/filteredLists[]", "field", "operator", "value", "values", "listRoute", "title", "description", "listTemplate", "pageSize", "urlPattern", "emptyBehavior"),
        new("site/menus/*[]", "identifier", "name", "url", "weight", "children"),
        new("content/sources[]", "type", "name", "mode", "collection", "addToCollections", "markdown", "notion", "dataIndex"),
        new("content/sources[]/markdown", "dir", "defaultType", "maxItems", "includePaths", "includeGlobs"),
        new("content/sources[]/notion", "databaseId", "pageSize", "maxItems", "renderContent", "renderConcurrency", "maxRps", "maxRetries", "fieldPolicy", "filterProperty", "filterType", "filterValue", "sortProperty", "sortDirection", "includeSlugs", "includeSlugProperty", "cacheMode", "cacheDir", "propertyMap"),
        new("content/sources[]/notion/fieldPolicy", "mode", "allowed"),
        new("content/sources[]/notion/propertyMap", "Title", "Slug", "Type", "PublishAt", "Language", "I18nKey", "Summary", "Collection", "SeoTitle", "SeoDescription", "SeoImage", "Canonical", "OriginalUrl", "References", "EntitiesJson", "Cover", "CoverAlt", "CoverCaption"),
        new("content/sources[]/dataIndex", "scopeField", "keyField", "valueField", "valueTypeField", "requiredKeys"),
        new("content/media", "downloadToLocal", "downloadDir", "urlBase", "defaultImageUrl", "fieldKeys", "maxConcurrency", "maxRetries", "timeoutMs", "maxFileSizeBytes", "blockPrivateNetworks", "retryBaseDelayMs"),
        new("content/modelSchema", "contentTypes", "statuses", "reviewStatuses", "syncStatuses", "canonicalMappings", "customFields", "fieldScopes", "entityMappings", "relationMappings", "media", "rejectUnknownRawKeys", "requireSummary", "requireAuthor", "requireOrganization", "requireUpdatedAt", "requireProvenance", "requireReviewedAt", "requireMediaAlt", "requireMediaDescription", "requireMediaLicense", "requireEntityIds", "requireRelationTargets"),
        new("content/modelSchema/canonicalMappings[]", "canonicalField", "rawKey", "semanticType", "required"),
        new("content/modelSchema/customFields[]", "name", "fieldType", "required", "semanticType", "label", "format", "enum", "min", "max", "default", "sourcePolicy", "reference"),
        new("content/modelSchema/customFields[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/fieldScopes/*[]", "name", "fieldType", "required", "semanticType", "label", "format", "enum", "min", "max", "default", "sourcePolicy", "reference"),
        new("content/modelSchema/fieldScopes/*[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/entityMappings[]", "rawKey", "entityType", "idField", "nameField", "descriptionField", "urlField", "sameAsField", "required", "reference"),
        new("content/modelSchema/entityMappings[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/relationMappings[]", "rawKey", "relationType", "targetType", "targetField", "targetIdField", "required", "reference"),
        new("content/modelSchema/relationMappings[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/media", "requireAlt", "requireDescription", "requireLicense", "allowedKinds"),
        new("content/routeMetadata", "source", "routeField", "titleField", "summaryField", "seoTitleField", "seoDescriptionField", "requiredRoutes"),
        new("build/report", "enabled", "securityFailMode"),
        new("theme/scss", "enabled", "entryPoint", "outputDir"),
        new("theme/images", "enabled", "formats", "sizes", "quality"),
        new("theme/components/*", "template", "props"),
        new("taxonomy/kinds[]", "key", "kind", "title", "description", "singularTitlePrefix", "template", "indexTemplate", "termTemplate", "indexEnabled", "hierarchical", "routePrefix"),
        new("logging", "level"),
        new("deploy", "provider", "branch", "message", "cname", "keepHistory")
    ];

    private static readonly string[] DynamicMapPaths =
    [
        "site/permalinks",
        "theme/params",
        "theme/shortcodes",
        "theme/components/*/props",
        "taxonomy/pinFieldBySource",
        "taxonomy/pinOrderFieldBySource"
    ];

    [Fact]
    public void Generate_NestedSections_MatchStrictValidatorWhitelists()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var failures = new List<string>();

        foreach (var item in NestedCases)
        {
            try
            {
                var actual = ResolveProperties(schema.RootElement, item.Path);
                var expected = item.Fields.ToHashSet(StringComparer.Ordinal);
                if (!expected.SetEquals(actual))
                {
                    failures.Add($"{item.Path}: expected [{string.Join(", ", expected.Order(StringComparer.Ordinal))}], actual [{string.Join(", ", actual.Order(StringComparer.Ordinal))}]");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{item.Path}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Generate_AnalyticsProviderVariants_MatchStrictTypeSpecificFields()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var providers = Resolve(schema.RootElement, "site/analytics/providers[]");
        var variants = providers.GetProperty("oneOf").EnumerateArray().ToDictionary(
            item => item.GetProperty("properties").GetProperty("type").GetProperty("const").GetString()!,
            item => item.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["google-analytics"] = ["measurementId", "type"],
            ["google-tag-manager"] = ["containerId", "type"],
            ["plausible"] = ["domain", "scriptUrl", "snippetMode", "type"],
            ["umami"] = ["scriptUrl", "type", "websiteId"]
        };

        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            variants.Keys.Order(StringComparer.Ordinal));
        foreach (var (type, fields) in expected)
        {
            Assert.Equal(fields, variants[type].Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Generate_DynamicMapLocations_RemainOpenObjects()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());

        foreach (var path in DynamicMapPaths)
        {
            var node = Resolve(schema.RootElement, path);
            Assert.True(
                node.TryGetProperty("additionalProperties", out _),
                $"{path} must remain an explicitly open dynamic map.");
        }
    }

    [Fact]
    public void Generate_PluginToggle_MappingVariantMatchesStrictFields()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var toggle = Resolve(schema.RootElement, "site/plugins/*");
        var variants = toggle.GetProperty("oneOf").EnumerateArray().ToArray();
        var mapping = Assert.Single(variants, item => item.TryGetProperty("properties", out _));

        Assert.Equal(
            ["enabled", "options"],
            mapping.GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Contains(variants, item => item.GetProperty("type").GetString() == "boolean");
    }

    [Fact]
    public void Generate_PluginOptions_RemainOpenDynamicMap()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var toggle = Resolve(schema.RootElement, "site/plugins/*");
        var mapping = toggle.GetProperty("oneOf").EnumerateArray()
            .Single(item => item.TryGetProperty("properties", out _));
        var options = mapping.GetProperty("properties").GetProperty("options");

        Assert.True(options.TryGetProperty("additionalProperties", out _));
    }

    private static IReadOnlySet<string> ResolveProperties(JsonElement root, string path)
    {
        var node = Resolve(root, path);
        if (!node.TryGetProperty("properties", out var properties))
        {
            throw new InvalidOperationException($"{path} has no properties schema.");
        }

        return properties.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static JsonElement Resolve(JsonElement root, string path)
    {
        var current = root;
        var currentPath = "$";
        foreach (var rawSegment in path.Split('/'))
        {
            var segment = rawSegment;
            var isArray = segment.EndsWith("[]", StringComparison.Ordinal);
            if (isArray)
            {
                segment = segment[..^2];
            }

            currentPath += "/" + rawSegment;
            if (segment == "*")
            {
                if (!current.TryGetProperty("additionalProperties", out current))
                {
                    throw new InvalidOperationException($"{currentPath} has no additionalProperties.");
                }
            }
            else
            {
                if (!current.TryGetProperty("properties", out var properties) ||
                    !properties.TryGetProperty(segment, out current))
                {
                    throw new InvalidOperationException($"{currentPath} has no schema property '{segment}'.");
                }
            }

            if (isArray)
            {
                if (!current.TryGetProperty("items", out current))
                {
                    throw new InvalidOperationException($"{currentPath} has no items schema.");
                }
            }
        }

        return current;
    }

    private sealed record SchemaCase(string Path, params string[] Fields);
}
