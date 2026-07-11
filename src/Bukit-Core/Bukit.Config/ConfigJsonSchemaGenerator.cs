using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bukit.Config;

public static class ConfigJsonSchemaGenerator
{
    public static string Generate()
    {
        var root = Obj(
            ("$schema", "https://json-schema.org/draft/2020-12/schema"),
            ("$id", "https://bukit.dev/schemas/site.schema.json"),
            ("title", "Bukit site.yaml"),
            ("type", "object"));

        root["required"] = Arr("site", "content");
        root["additionalProperties"] = false;
        root["properties"] = Obj(
            ("site", SiteSchema()),
            ("content", ContentSchema()),
            ("build", BuildSchema()),
            ("theme", ThemeSchema()),
            ("taxonomy", TaxonomySchema()),
            ("logging", Obj(("type", "object"), ("properties", Obj(("level", EnumSchema("debug", "info", "warn", "error")))))),
            ("deploy", DeploySchema()));

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject SiteSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("name", "title");
        schema["properties"] = Obj(
            ("name", StringSchema()),
            ("title", StringSchema()),
            ("url", Obj(("type", "string"), ("format", "uri"))),
            ("description", StringSchema()),
            ("seo", SeoSchema()),
            ("analytics", AnalyticsSchema()),
            ("autoSummary", BoolSchema()),
            ("autoSummaryMaxLength", IntSchema(1)),
            ("baseUrl", StringSchema()),
            ("outputPathEncoding", EnumSchema("none", "slug", "urlencode", "sanitize")),
            ("language", StringSchema()),
            ("languages", StringArraySchema()),
            ("defaultLanguage", StringSchema()),
            ("sitemapMode", EnumSchema("split", "merged", "index")),
            ("searchIncludeDerived", BoolSchema()),
            ("pluginFailMode", EnumSchema("strict", "warn")),
            ("deriveConflictPolicy", EnumSchema("fail", "warn", "last-wins")),
            ("timezone", StringSchema()),
            ("collections", CollectionSchema()),
            ("permalinks", StringMapSchema()),
            ("plugins", Obj(("type", "object"), ("additionalProperties", true))),
            ("feed", FeedSchema()),
            ("sitemapDetail", SitemapDetailSchema()),
            ("pagination", PaginationGlobalSchema()),
            ("search", SearchDetailSchema()),
            ("related", RelatedSchema()),
            ("menus", MenusSchema()));
        return schema;
    }

    private static JsonObject ContentSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("sources");
        schema["properties"] = Obj(
            ("media", MediaSchema()),
            ("modelSchema", ContentModelSchemaSchema()),
            ("routeMetadata", RouteMetadataSchema()),
            ("sources", Obj(("type", "array"), ("items", ContentSourceItemSchema())))
            );
        return schema;
    }

    private static JsonObject RouteMetadataSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("source");
        schema["properties"] = Obj(
            ("source", StringSchema()),
            ("routeField", StringSchema()),
            ("titleField", StringSchema()),
            ("summaryField", StringSchema()),
            ("seoTitleField", StringSchema()),
            ("seoDescriptionField", StringSchema()),
            ("requiredRoutes", StringArraySchema()));
        return schema;
    }

    private static JsonObject BuildSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("output", StringSchema()),
            ("clean", BoolSchema()),
            ("draft", BoolSchema()),
            ("listPageContentMode", EnumSchema("auto", "always", "never")),
            ("schemaFailMode", EnumSchema("off", "warn", "strict")),
            ("report", BuildReportSchema()),
            ("fingerprintMode", EnumSchema("size-time", "sha256")),
            ("publishDotFiles", BoolSchema()),
            ("followSymlinks", BoolSchema()),
            ("languageJobs", IntSchema(1)))));

    private static JsonObject ThemeSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("name", StringSchema()),
            ("layouts", StringSchema()),
            ("assets", StringSchema()),
            ("static", StringSchema()),
            ("staticTemplate", StringSchema()),
            ("params", Obj(("type", "object"), ("additionalProperties", true))),
            ("shortcodes", StringMapSchema()),
            ("components", Obj(("type", "object"), ("additionalProperties", ComponentDefinitionSchema()))),
            ("scss", ScssSchema()),
            ("images", ImageOptimizationSchema()),
            ("componentValidation", EnumSchema("off", "warn", "strict")))));

    private static JsonObject TaxonomySchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("outputMode", EnumSchema("both", "pages", "data", "fields_only")),
            ("itemFields", StringArraySchema()),
            ("pageSize", IntSchema(1)),
            ("indexEnabled", BoolSchema()),
            ("pinField", StringSchema()),
            ("pinOrderField", StringSchema()),
            ("pinFieldBySource", StringMapSchema()),
            ("pinOrderFieldBySource", StringMapSchema()),
            ("kinds", Obj(("type", "array"), ("items", Obj(("type", "object"), ("properties", Obj(
                ("key", StringSchema()),
                ("kind", StringSchema()),
                ("title", StringSchema()),
                ("description", StringSchema()),
                ("singularTitlePrefix", StringSchema()),
                ("template", StringSchema()),
                ("indexTemplate", StringSchema()),
                ("termTemplate", StringSchema()),
                ("indexEnabled", BoolSchema()),
                ("hierarchical", BoolSchema()),
                ("routePrefix", StringSchema()))))))))));

    private static JsonObject DeploySchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("provider", EnumSchema("github-pages")),
            ("branch", StringSchema()),
            ("message", StringSchema()),
            ("cname", StringSchema()),
            ("keepHistory", BoolSchema()))));

    private static JsonObject SeoSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("renderMode", EnumSchema("theme", "inject", "off")),
            ("diagnostics", EnumSchema("off", "warn", "strict")),
            ("homeTitleTemplate", StringSchema()),
            ("pageTitleTemplate", StringSchema()),
            ("titleSeparator", StringSchema()),
            ("defaultImage", StringSchema()),
            ("twitterSite", StringSchema()),
            ("organization", SeoOrganizationSchema()),
            ("robotsTxt", SeoRobotsTxtSchema()),
            ("schema", SeoSchemaDetailSchema()),
            ("geo", SeoGeoSchema()))));

    private static JsonObject SeoRobotsTxtSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()))));

    private static JsonObject SeoSchemaDetailSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("webPage", BoolSchema()),
            ("collectionPage", BoolSchema()),
            ("searchAction", BoolSchema()))));

    private static JsonObject SeoOrganizationSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("name", StringSchema()),
            ("url", StringSchema()),
            ("logo", StringSchema()))));

    private static JsonObject SeoGeoSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("llmsTxt", BoolSchema()),
            ("llmsFullTxt", BoolSchema()),
            ("llmsTxtMaxArticles", IntSchema(1)),
            ("aiBotMode", EnumSchema("allow", "block", "selective")),
            ("aiBotAllowList", StringArraySchema()),
            ("aiBotBlockList", StringArraySchema()),
            ("llmsTxtOptionalLinks", Obj(("type", "array"), ("items", LlmsTxtOptionalLinkSchema()))))));

    private static JsonObject LlmsTxtOptionalLinkSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("title", "url");
        schema["properties"] = Obj(
            ("title", StringSchema()),
            ("url", StringSchema()),
            ("description", StringSchema()));
        return schema;
    }

    private static JsonObject AnalyticsSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("googleAnalyticsId", StringSchema()),
            ("disableInPreview", BoolSchema()))));

    private static JsonObject FeedSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("mode", EnumSchema("split", "merged")),
            ("formats", StringArraySchema()),
            ("limit", IntSchema(1)),
            ("path", StringSchema()))));

    private static JsonObject SitemapDetailSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("defaultPriority", Obj(("type", "number"), ("minimum", 0.0), ("maximum", 1.0))),
            ("defaultChangefreq", StringSchema()),
            ("imageEnabled", BoolSchema()),
            ("videoEnabled", BoolSchema()))));

    private static JsonObject PaginationGlobalSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("pageSize", IntSchema(1)))));

    private static JsonObject SearchDetailSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("mode", EnumSchema("split", "merged", "index")),
            ("ui", StringSchema()),
            ("uiTheme", EnumSchema("light", "dark", "auto")),
            ("placeholderText", StringSchema()),
            ("maxContentLength", IntSchema(1)))));

    private static JsonObject RelatedSchema()
    {
        var indexSchema = Obj(("type", "object"));
        indexSchema["properties"] = Obj(
            ("name", StringSchema()),
            ("weight", IntSchema(0)));

        return Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("threshold", IntSchema(0)),
            ("limit", IntSchema(1)),
            ("indices", Obj(("type", "array"), ("items", indexSchema))))));
    }

    private static JsonObject MenusSchema()
        => Obj(
            ("type", "object"),
            ("additionalProperties", Obj(("type", "array"), ("items", MenuConfigItemSchema()))));

    private static JsonObject MenuConfigItemSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("identifier", StringSchema()),
            ("name", StringSchema()),
            ("url", StringSchema()),
            ("weight", IntSchema(0)),
            ("children", Obj(("type", "array"), ("items", Obj(("type", "object"), ("properties", Obj(
                ("identifier", StringSchema()),
                ("name", StringSchema()),
                ("url", StringSchema()),
                ("weight", IntSchema(0)))))))))));

    private static JsonObject BuildReportSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("securityFailMode", EnumSchema("auto", "off", "warn", "strict")))));

    private static JsonObject ComponentDefinitionSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("template", StringSchema()),
            ("props", StringMapSchema()))));

    private static JsonObject ScssSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("entryPoint", StringSchema()),
            ("outputDir", StringSchema()))));

    private static JsonObject ImageOptimizationSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("formats", StringArraySchema()),
            ("sizes", Obj(("type", "array"), ("items", IntSchema(1)))),
            ("quality", IntSchema(0)))));

    private static JsonObject MediaSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("downloadToLocal", BoolSchema()),
            ("downloadDir", StringSchema()),
            ("urlBase", StringSchema()),
            ("defaultImageUrl", StringSchema()),
            ("fieldKeys", StringArraySchema()),
            ("maxConcurrency", IntSchema(1)),
            ("maxRetries", IntSchema(0)),
            ("timeoutMs", IntSchema(1)),
            ("maxFileSizeBytes", IntSchema(1)),
            ("blockPrivateNetworks", BoolSchema()),
            ("retryBaseDelayMs", IntSchema(0)))));

    private static JsonObject ContentSourceItemSchema()
        => Obj(("type", "object"), ("required", Arr("type")), ("properties", Obj(
            ("type", EnumSchema("markdown", "notion")),
            ("name", StringSchema()),
            ("mode", EnumSchema("content", "data")),
            ("collection", StringSchema()),
            ("addToCollections", StringArraySchema()),
            ("markdown", Obj(("type", "object"), ("properties", Obj(
                ("dir", StringSchema()),
                ("defaultType", StringSchema()),
                ("maxItems", IntSchema(1)),
                ("includePaths", StringArraySchema()),
                ("includeGlobs", StringArraySchema()))))),
            ("notion", NotionSchema()),
            ("dataIndex", DataIndexSchema()))));

    private static JsonObject DataIndexSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("scopeField", StringSchema()),
            ("keyField", StringSchema()),
            ("valueField", StringSchema()),
            ("valueTypeField", StringSchema()),
            ("requiredKeys", StringArraySchema()))));

    private static JsonObject NotionSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("databaseId", StringSchema()),
            ("pageSize", IntSchema(1)),
            ("maxItems", IntSchema(1)),
            ("renderContent", BoolSchema()),
            ("renderConcurrency", IntSchema(1)),
            ("maxRps", IntSchema(1)),
            ("maxRetries", IntSchema(0)),
            ("fieldPolicy", NotionFieldPolicySchema()),
            ("filterProperty", StringSchema()),
            ("filterType", StringSchema()),
            ("filterValue", StringSchema()),
            ("sortProperty", StringSchema()),
            ("sortDirection", EnumSchema("ascending", "descending")),
            ("includeSlugs", StringArraySchema()),
            ("includeSlugProperty", StringSchema()),
            ("cacheMode", EnumSchema("off", "readwrite", "readonly")),
            ("cacheDir", StringSchema()),
            ("propertyMap", NotionPropertyMapSchema()))));

    private static JsonObject NotionPropertyMapSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("Title", StringSchema()),
            ("Slug", StringSchema()),
            ("Type", StringSchema()),
            ("PublishAt", StringSchema()),
            ("Language", StringSchema()),
            ("I18nKey", StringSchema()),
            ("Summary", StringSchema()),
            ("Collection", StringSchema()),
            ("SeoTitle", StringSchema()),
            ("SeoDescription", StringSchema()),
            ("SeoImage", StringSchema()),
            ("Canonical", StringSchema()))));

    private static JsonObject NotionFieldPolicySchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("mode", EnumSchema("whitelist", "all")),
            ("allowed", StringArraySchema()))));

    private static JsonObject CollectionSchema()
        => Obj(
            ("type", "object"),
            ("additionalProperties", CollectionItemSchema()));

    private static JsonObject CollectionItemSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("permalink");
        schema["properties"] = Obj(
            ("permalink", StringSchema()),
            ("template", StringSchema()),
            ("listRoute", StringSchema()),
            ("listTitle", StringSchema()),
            ("listDescription", StringSchema()),
            ("listTemplate", StringSchema()),
            ("schemaFailMode", EnumSchema("off", "warn", "strict")),
            ("pagination", CollectionPaginationSchema()),
            ("output", CollectionOutputSchema()),
            ("filteredLists", Obj(("type", "array"), ("items", CollectionFilteredListItemSchema()))));
        return schema;
    }

    private static JsonObject CollectionPaginationSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("pageSize", IntSchema(1)),
            ("urlPattern", StringSchema()),
            ("firstPageUsesListRoute", BoolSchema()))));

    private static JsonObject CollectionOutputSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("rss", BoolSchema()),
            ("sitemap", BoolSchema()),
            ("archive", BoolSchema()),
            ("feedPath", StringSchema()),
            ("feedTitle", StringSchema()),
            ("feedDescription", StringSchema()),
            ("archiveDetail", CollectionArchiveDetailSchema()))));

    private static JsonObject CollectionArchiveDetailSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("depth", StringSchema()),
            ("template", StringSchema()),
            ("routePrefix", StringSchema()))));

    private static JsonObject CollectionFilteredListItemSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("field", "listRoute");
        schema["properties"] = Obj(
            ("field", StringSchema()),
            ("operator", EnumSchema("equals", "contains", "in")),
            ("value", StringSchema()),
            ("values", StringArraySchema()),
            ("listRoute", StringSchema()),
            ("title", StringSchema()),
            ("description", StringSchema()),
            ("listTemplate", StringSchema()),
            ("pageSize", IntSchema(1)),
            ("urlPattern", StringSchema()),
            ("emptyBehavior", EnumSchema("render", "skip")));
        return schema;
    }

    private static JsonObject ContentModelSchemaSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("contentTypes", StringArraySchema()),
            ("statuses", StringArraySchema()),
            ("reviewStatuses", StringArraySchema()),
            ("syncStatuses", StringArraySchema()),
            ("canonicalMappings", Obj(("type", "array"), ("items", CanonicalFieldMappingSchema()))),
            ("customFields", Obj(("type", "array"), ("items", ContentModelFieldSchema()))),
            ("fieldScopes", Obj(("type", "object"), ("additionalProperties", Obj(("type", "array"), ("items", ContentModelFieldSchema()))))),
            ("entityMappings", Obj(("type", "array"), ("items", EntityMappingSchema()))),
            ("relationMappings", Obj(("type", "array"), ("items", RelationMappingSchema()))),
            ("media", ContentModelMediaPolicySchema()),
            ("rejectUnknownRawKeys", BoolSchema()),
            ("requireSummary", BoolSchema()),
            ("requireAuthor", BoolSchema()),
            ("requireOrganization", BoolSchema()),
            ("requireUpdatedAt", BoolSchema()),
            ("requireProvenance", BoolSchema()),
            ("requireReviewedAt", BoolSchema()),
            ("requireMediaAlt", BoolSchema()),
            ("requireMediaDescription", BoolSchema()),
            ("requireMediaLicense", BoolSchema()),
            ("requireEntityIds", BoolSchema()),
            ("requireRelationTargets", BoolSchema()))));

    private static JsonObject CanonicalFieldMappingSchema()
        => Obj(("type", "object"), ("required", Arr("canonicalField")), ("properties", Obj(
            ("canonicalField", StringSchema()),
            ("rawKey", StringSchema()),
            ("semanticType", StringSchema()),
            ("required", BoolSchema()))));

    private static JsonObject ContentModelFieldSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("name");
        schema["properties"] = Obj(
            ("name", StringSchema()),
            ("fieldType", StringSchema()),
            ("label", StringSchema()),
            ("semanticType", StringSchema()),
            ("format", StringSchema()),
            ("enum", StringArraySchema()),
            ("min", Obj(("type", "number"))),
            ("max", Obj(("type", "number"))),
            ("required", BoolSchema()),
            ("default", Obj()),
            ("sourcePolicy", StringSchema()),
            ("reference", ContentReferenceRuleSchema()));
        return schema;
    }

    private static JsonObject EntityMappingSchema()
        => Obj(("type", "object"), ("required", Arr("rawKey", "entityType")), ("properties", Obj(
            ("rawKey", StringSchema()),
            ("entityType", StringSchema()),
            ("idField", StringSchema()),
            ("nameField", StringSchema()),
            ("descriptionField", StringSchema()),
            ("urlField", StringSchema()),
            ("sameAsField", StringSchema()),
            ("required", BoolSchema()),
            ("reference", ContentReferenceRuleSchema()))));

    private static JsonObject RelationMappingSchema()
        => Obj(("type", "object"), ("required", Arr("rawKey", "relationType")), ("properties", Obj(
            ("rawKey", StringSchema()),
            ("relationType", StringSchema()),
            ("targetType", StringSchema()),
            ("targetField", StringSchema()),
            ("targetIdField", StringSchema()),
            ("required", BoolSchema()),
            ("reference", ContentReferenceRuleSchema()))));

    private static JsonObject ContentReferenceRuleSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("targetType", StringSchema()),
            ("idField", StringSchema()),
            ("labelField", StringSchema()),
            ("urlField", StringSchema()),
            ("required", BoolSchema()))));

    private static JsonObject ContentModelMediaPolicySchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("requireAlt", BoolSchema()),
            ("requireDescription", BoolSchema()),
            ("requireLicense", BoolSchema()),
            ("allowedKinds", StringArraySchema()))));

    private static JsonObject StringSchema() => Obj(("type", "string"));

    private static JsonObject BoolSchema() => Obj(("type", "boolean"));

    private static JsonObject IntSchema(int? min = null)
    {
        var schema = Obj(("type", "integer"));
        if (min is not null)
        {
            schema["minimum"] = min.Value;
        }

        return schema;
    }

    private static JsonObject StringArraySchema() => Obj(("type", "array"), ("items", StringSchema()));

    private static JsonObject StringMapSchema()
        => Obj(("type", "object"), ("additionalProperties", StringSchema()));

    private static JsonObject EnumSchema(params string[] values)
    {
        var schema = StringSchema();
        schema["enum"] = new JsonArray(values.Select(v => JsonValue.Create(v)).ToArray<JsonNode?>());
        return schema;
    }

    private static JsonArray Arr(params string[] values)
        => new(values.Select(v => JsonValue.Create(v)).ToArray<JsonNode?>());

    private static JsonObject Obj(params (string Key, object? Value)[] values)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in values)
        {
            obj[key] = value switch
            {
                null => null,
                JsonNode node => node,
                string s => s,
                bool b => b,
                int i => i,
                double d => d,
                _ => value.ToString()
            };
        }

        if (
            obj.TryGetPropertyValue("type", out var type)
            && type is JsonValue typeValue
            && typeValue.GetValue<string>() == "object"
            && !obj.ContainsKey("additionalProperties"))
        {
            obj["additionalProperties"] = false;
        }

        return obj;
    }
}
