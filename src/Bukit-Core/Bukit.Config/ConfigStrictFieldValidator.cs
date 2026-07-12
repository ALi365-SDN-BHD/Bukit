using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static class ConfigStrictFieldValidator
{
    private static readonly HashSet<string> RootKeys = Set("site", "content", "build", "theme", "taxonomy", "logging", "deploy");
    private static readonly HashSet<string> SiteKeys = Set(
        "name", "title", "url", "description", "seo", "analytics", "autoSummary", "autoSummaryMaxLength",
        "baseUrl", "outputPathEncoding", "language", "languages", "defaultLanguage", "sitemapMode",
        "searchIncludeDerived", "pluginFailMode", "deriveConflictPolicy",
        "timezone", "permalinks", "collections", "plugins", "feed",
        "sitemapDetail", "pagination", "search", "related", "menus");
    private static readonly HashSet<string> ContentKeys = Set("sources", "media", "modelSchema", "routeMetadata");
    private static readonly HashSet<string> BuildKeys = Set(
        "output", "clean", "draft", "listPageContentMode", "schemaFailMode", "fingerprintMode",
        "publishDotFiles", "followSymlinks", "languageJobs", "report");
    private static readonly HashSet<string> BuildReportKeys = Set("enabled", "securityFailMode");
    private static readonly HashSet<string> ThemeKeys = Set(
        "name", "layouts", "assets", "static", "staticTemplate", "params", "shortcodes",
        "components", "scss", "images", "componentValidation");
    private static readonly HashSet<string> TaxonomyKeys = Set(
        "kinds", "outputMode", "itemFields", "pageSize", "indexEnabled", "pinField", "pinOrderField",
        "pinFieldBySource", "pinOrderFieldBySource");
    private static readonly HashSet<string> LoggingKeys = Set("level");
    private static readonly HashSet<string> DeployKeys = Set("provider", "branch", "message", "cname", "keepHistory");

    internal static void Validate(YamlMappingNode root)
    {
        RequireOnly(root, RootKeys, "root");

        if (Map(root, "site") is { } site) ValidateSite(site);
        if (Map(root, "content") is { } content) ValidateContent(content);
        if (Map(root, "build") is { } build) ValidateBuild(build);
        if (Map(root, "theme") is { } theme) ValidateTheme(theme);
        if (Map(root, "taxonomy") is { } taxonomy) ValidateTaxonomy(taxonomy);
        if (Map(root, "logging") is { } logging) RequireOnly(logging, LoggingKeys, "logging");
        if (Map(root, "deploy") is { } deploy) RequireOnly(deploy, DeployKeys, "deploy");
    }

    private static void ValidateSite(YamlMappingNode site)
    {
        RequireOnly(site, SiteKeys, "site");
        if (Map(site, "collections") is { } collections)
        {
            foreach (var (name, collection) in MappingChildren(collections, "site.collections"))
            {
                RequireOnly(collection, Set("permalink", "template", "listRoute", "listTitle", "listDescription", "listTemplate", "schemaFailMode", "pagination", "output", "filteredLists"), $"site.collections.{name}");
                if (Map(collection, "pagination") is { } collectionPagination)
                {
                    RequireOnly(collectionPagination, Set("enabled", "pageSize", "urlPattern", "firstPageUsesListRoute"), $"site.collections.{name}.pagination");
                }

                if (Map(collection, "output") is { } output)
                {
                    RequireOnly(output, Set("rss", "sitemap", "archive", "feedPath", "feedTitle", "feedDescription", "archiveDetail"), $"site.collections.{name}.output");
                    if (Map(output, "archiveDetail") is { } archiveDetail)
                    {
                        RequireOnly(archiveDetail, Set("depth", "template", "routePrefix"), $"site.collections.{name}.output.archiveDetail");
                    }
                }

                if (Seq(collection, "filteredLists") is { } filteredLists)
                {
                    ValidateSequenceMappings(filteredLists, Set("field", "operator", "value", "values", "listRoute", "title", "description", "listTemplate", "pageSize", "urlPattern", "emptyBehavior"), $"site.collections.{name}.filteredLists");
                }
            }
        }

        if (Map(site, "seo") is { } seo) ValidateSeo(seo);
        if (Map(site, "analytics") is { } analytics) RequireOnly(analytics, Set("enabled", "googleAnalyticsId", "disableInPreview"), "site.analytics");
        if (Map(site, "feed") is { } feed) RequireOnly(feed, Set("mode", "formats", "limit", "path"), "site.feed");
        if (Map(site, "search") is { } search) RequireOnly(search, Set("mode", "ui", "uiTheme", "placeholderText", "maxContentLength"), "site.search");
        if (Map(site, "related") is { } related)
        {
            RequireOnly(related, Set("enabled", "threshold", "limit", "indices"), "site.related");
            if (Seq(related, "indices") is { } indices)
            {
                ValidateSequenceMappings(indices, Set("name", "weight"), "site.related.indices");
            }
        }

        if (Map(site, "sitemapDetail") is { } sitemap) RequireOnly(sitemap, Set("defaultPriority", "defaultChangefreq", "imageEnabled", "videoEnabled"), "site.sitemapDetail");
        if (Map(site, "pagination") is { } pagination) RequireOnly(pagination, Set("enabled", "pageSize"), "site.pagination");
        if (Map(site, "menus") is { } menus) ValidateMenus(menus);
    }

    private static void ValidateSeo(YamlMappingNode seo)
    {
        RequireOnly(seo, Set(
            "enabled", "renderMode", "diagnostics",
            "homeTitleTemplate", "pageTitleTemplate", "titleSeparator",
            "defaultImage", "twitterSite", "organization", "robotsTxt", "schema", "geo"), "site.seo");
        if (Map(seo, "organization") is { } organization) RequireOnly(organization, Set("name", "url", "logo"), "site.seo.organization");
        if (Map(seo, "robotsTxt") is { } robots) RequireOnly(robots, Set("enabled"), "site.seo.robotsTxt");
        if (Map(seo, "schema") is { } schema) RequireOnly(schema, Set("webPage", "collectionPage", "searchAction"), "site.seo.schema");
        if (Map(seo, "geo") is { } geo)
        {
            RequireOnly(geo, Set(
                "enabled", "llmsTxt", "llmsFullTxt", "llmsTxtMaxArticles", "aiBotMode",
                "aiBotAllowList", "aiBotBlockList", "llmsTxtOptionalLinks"), "site.seo.geo");
            if (Seq(geo, "llmsTxtOptionalLinks") is { } optionalLinks)
            {
                ValidateSequenceMappings(optionalLinks, Set("title", "url", "description"), "site.seo.geo.llmsTxtOptionalLinks");
            }
        }
    }

    private static void ValidateContent(YamlMappingNode content)
    {
        RequireOnly(content, ContentKeys, "content");
        if (Seq(content, "sources") is { } sources)
        {
            var index = 0;
            foreach (var source in sources.Children)
            {
                if (source is not YamlMappingNode sourceMap)
                {
                    index++;
                    continue;
                }

                var path = $"content.sources[{index}]";
                RequireOnly(sourceMap, Set("type", "name", "mode", "collection", "addToCollections", "markdown", "notion", "dataIndex"), path);
                if (Map(sourceMap, "markdown") is { } markdown) RequireOnly(markdown, Set("dir", "defaultType", "maxItems", "includePaths", "includeGlobs"), $"{path}.markdown");
                if (Map(sourceMap, "notion") is { } notion) ValidateNotion(notion, $"{path}.notion");
                if (Map(sourceMap, "dataIndex") is { } dataIndex)
                {
                    RequireOnly(dataIndex, Set("scopeField", "keyField", "valueField", "valueTypeField", "requiredKeys"), $"{path}.dataIndex");
                }
                index++;
            }
        }

        if (Map(content, "media") is { } media)
        {
            RequireOnly(media, Set(
                "downloadToLocal", "downloadDir", "urlBase", "defaultImageUrl", "fieldKeys", "maxConcurrency",
                "maxRetries", "timeoutMs", "maxFileSizeBytes", "blockPrivateNetworks", "retryBaseDelayMs"), "content.media");
        }

        if (Map(content, "modelSchema") is { } modelSchema) ValidateModelSchema(modelSchema);
        if (Map(content, "routeMetadata") is { } routeMetadata)
        {
            RequireOnly(routeMetadata, Set(
                "source", "routeField", "titleField", "summaryField", "seoTitleField",
                "seoDescriptionField", "requiredRoutes"), "content.routeMetadata");
        }
    }

    private static void ValidateNotion(YamlMappingNode notion, string path)
    {
        RequireOnly(notion, Set(
            "databaseId", "pageSize", "maxItems", "renderContent", "renderConcurrency", "maxRps", "maxRetries",
            "fieldPolicy", "filterProperty", "filterType", "filterValue", "sortProperty", "sortDirection",
            "includeSlugs", "includeSlugProperty", "cacheMode", "cacheDir", "propertyMap"), path);
        if (Map(notion, "fieldPolicy") is { } fieldPolicy)
        {
            RequireOnly(fieldPolicy, Set("mode", "allowed"), $"{path}.fieldPolicy");
        }

        if (Map(notion, "propertyMap") is { } propertyMap)
        {
            RequireOnly(propertyMap, Set(
                "Title", "Slug", "Type", "PublishAt", "Language", "I18nKey", "Summary", "Collection",
                "SeoTitle", "SeoDescription", "SeoImage", "Canonical", "OriginalUrl", "References",
                "EntitiesJson", "Cover", "CoverAlt", "CoverCaption"), $"{path}.propertyMap");
        }
    }

    private static void ValidateModelSchema(YamlMappingNode modelSchema)
    {
        RequireOnly(modelSchema, Set(
            "contentTypes", "statuses", "reviewStatuses", "syncStatuses", "canonicalMappings", "customFields",
            "fieldScopes", "entityMappings", "relationMappings", "media", "rejectUnknownRawKeys", "requireSummary",
            "requireAuthor", "requireOrganization", "requireUpdatedAt", "requireProvenance", "requireReviewedAt",
            "requireMediaAlt", "requireMediaDescription", "requireMediaLicense", "requireEntityIds", "requireRelationTargets"),
            "content.modelSchema");

        if (Seq(modelSchema, "canonicalMappings") is { } canonicalMappings) ValidateSequenceMappings(canonicalMappings, Set("canonicalField", "rawKey", "semanticType", "required"), "content.modelSchema.canonicalMappings");
        if (Seq(modelSchema, "customFields") is { } customFields) ValidateFieldDefinitions(customFields, "content.modelSchema.customFields");
        if (Map(modelSchema, "fieldScopes") is { } fieldScopes)
        {
            foreach (var (scope, fields) in SequenceChildren(fieldScopes, "content.modelSchema.fieldScopes"))
            {
                ValidateFieldDefinitions(fields, $"content.modelSchema.fieldScopes.{scope}");
            }
        }

        if (Seq(modelSchema, "entityMappings") is { } entityMappings)
        {
            ValidateSequenceMappings(entityMappings, Set("rawKey", "entityType", "idField", "nameField", "descriptionField", "urlField", "sameAsField", "required", "reference"), "content.modelSchema.entityMappings", ValidateReference);
        }

        if (Seq(modelSchema, "relationMappings") is { } relationMappings)
        {
            ValidateSequenceMappings(relationMappings, Set("rawKey", "relationType", "targetType", "targetField", "targetIdField", "required", "reference"), "content.modelSchema.relationMappings", ValidateReference);
        }

        if (Map(modelSchema, "media") is { } media) RequireOnly(media, Set("requireAlt", "requireDescription", "requireLicense", "allowedKinds"), "content.modelSchema.media");
    }

    private static void ValidateFieldDefinitions(YamlSequenceNode fields, string path)
        => ValidateSequenceMappings(fields, Set("name", "fieldType", "required", "semanticType", "label", "format", "enum", "min", "max", "default", "sourcePolicy", "reference"), path, ValidateReference);

    private static void ValidateReference(YamlMappingNode node, string path)
    {
        if (Map(node, "reference") is { } reference)
        {
            RequireOnly(reference, Set("targetType", "idField", "labelField", "urlField", "required"), $"{path}.reference");
        }
    }

    private static void ValidateBuild(YamlMappingNode build)
    {
        RequireOnly(build, BuildKeys, "build");
        if (Map(build, "report") is { } report) RequireOnly(report, BuildReportKeys, "build.report");
    }

    private static void ValidateTheme(YamlMappingNode theme)
    {
        RequireOnly(theme, ThemeKeys, "theme");
        if (Map(theme, "components") is { } components) ValidateComponents(components);
        if (Map(theme, "scss") is { } scss) RequireOnly(scss, Set("enabled", "entryPoint", "outputDir"), "theme.scss");
        if (Map(theme, "images") is { } images) RequireOnly(images, Set("enabled", "formats", "sizes", "quality"), "theme.images");
    }

    private static void ValidateTaxonomy(YamlMappingNode taxonomy)
    {
        RequireOnly(taxonomy, TaxonomyKeys, "taxonomy");
        if (Seq(taxonomy, "kinds") is { } kinds)
        {
            ValidateSequenceMappings(kinds, Set("key", "kind", "title", "description", "singularTitlePrefix", "template", "indexTemplate", "termTemplate", "indexEnabled", "hierarchical", "routePrefix"), "taxonomy.kinds");
        }
    }

    private static void ValidateMenus(YamlMappingNode menus)
    {
        foreach (var (name, menuItems) in SequenceChildren(menus, "site.menus"))
        {
            ValidateMenuItems(menuItems, $"site.menus.{name}");
        }
    }

    private static void ValidateComponents(YamlMappingNode components)
    {
        foreach (var (name, component) in MappingChildren(components, "theme.components"))
        {
            RequireOnly(component, Set("template", "props"), $"theme.components.{name}");
            if (Map(component, "props") is { } props)
            {
                foreach (var child in props.Children)
                {
                    KeyName(child.Key, $"theme.components.{name}.props");
                }
            }
        }
    }

    private static void ValidateMenuItems(YamlSequenceNode items, string path)
    {
        var index = 0;
        foreach (var child in items.Children)
        {
            if (child is YamlMappingNode item)
            {
                var itemPath = $"{path}[{index}]";
                RequireOnly(item, Set("identifier", "name", "url", "weight", "children"), itemPath);
                if (Seq(item, "children") is { } children)
                {
                    ValidateMenuItems(children, $"{itemPath}.children");
                }
            }

            index++;
        }
    }

    private static void ValidateSequenceMappings(YamlSequenceNode sequence, HashSet<string> allowedKeys, string path, Action<YamlMappingNode, string>? after = null)
    {
        var index = 0;
        foreach (var child in sequence.Children)
        {
            if (child is YamlMappingNode map)
            {
                var itemPath = $"{path}[{index}]";
                RequireOnly(map, allowedKeys, itemPath);
                after?.Invoke(map, itemPath);
            }

            index++;
        }
    }

    private static IEnumerable<(string Name, YamlMappingNode Node)> MappingChildren(YamlMappingNode node, string path)
    {
        foreach (var child in node.Children)
        {
            var name = KeyName(child.Key, path);
            if (child.Value is YamlMappingNode map)
            {
                yield return (name, map);
            }
        }
    }

    private static IEnumerable<(string Name, YamlSequenceNode Node)> SequenceChildren(YamlMappingNode node, string path)
    {
        foreach (var child in node.Children)
        {
            var name = KeyName(child.Key, path);
            if (child.Value is YamlSequenceNode sequence)
            {
                yield return (name, sequence);
            }
        }
    }

    private static void RequireOnly(YamlMappingNode node, HashSet<string> allowedKeys, string path)
    {
        foreach (var child in node.Children)
        {
            var key = KeyName(child.Key, path);
            if (!allowedKeys.Contains(key))
            {
                throw new ConfigException($"Unknown config field '{path}.{key}'.", DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static YamlMappingNode? Map(YamlMappingNode node, string key)
        => node.Children.TryGetValue(new YamlScalarNode(key), out var child) ? child as YamlMappingNode : null;

    private static YamlSequenceNode? Seq(YamlMappingNode node, string key)
        => node.Children.TryGetValue(new YamlScalarNode(key), out var child) ? child as YamlSequenceNode : null;

    private static string KeyName(YamlNode key, string path)
        => key is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value)
            ? scalar.Value.Trim()
            : throw new ConfigException($"Config key under {path} must be a non-empty scalar.", DiagnosticCode.ConfigInvalidValue);

    private static HashSet<string> Set(params string[] values)
        => new(values, StringComparer.Ordinal);
}
