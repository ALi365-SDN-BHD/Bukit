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
            ("externalProtocolIncludeRoutedPages", BoolSchema()),
            ("pluginFailMode", EnumSchema("strict", "warn")),
            ("deriveConflictPolicy", EnumSchema("fail", "warn", "last-wins")),
            ("timezone", StringSchema()),
            ("collections", CollectionSchema()),
            ("permalinks", Obj(("type", "object"))),
            ("externalPlugins", ExternalPluginsSchema()),
            ("plugins", Obj(("type", "object"))),
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
            ("sources", Obj(("type", "array"), ("items", ContentSourceItemSchema())))
            );
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
            ("assetHashMode", EnumSchema("size-time", "sha256")),
            ("fingerprintMode", EnumSchema("size-time", "sha256")),
            ("publishDotFiles", BoolSchema()),
            ("followSymlinks", BoolSchema()),
            ("languageJobs", IntSchema(1)))));

    private static JsonObject ThemeSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("name", StringSchema()),
            ("source", StringSchema()),
            ("extends", StringSchema()),
            ("layouts", StringSchema()),
            ("assets", StringSchema()),
            ("static", StringSchema()),
            ("staticTemplate", StringSchema()),
            ("params", Obj(("type", "object"))),
            ("shortcodes", Obj(("type", "object"))),
            ("components", Obj(("type", "object"), ("additionalProperties", ComponentDefinitionSchema()))),
            ("scss", ScssSchema()),
            ("images", ImageOptimizationSchema()),
            ("componentValidation", EnumSchema("off", "warn", "strict")))));

    private static JsonObject TaxonomySchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("template", StringSchema()),
            ("indexTemplate", StringSchema()),
            ("termTemplate", StringSchema()),
            ("templates", Obj(("type", "object"), ("properties", Obj(
                ("tags", Obj(("type", "object"), ("properties", Obj(
                    ("template", StringSchema()),
                    ("indexTemplate", StringSchema()),
                    ("termTemplate", StringSchema()))))),
                ("categories", Obj(("type", "object"), ("properties", Obj(
                    ("template", StringSchema()),
                    ("indexTemplate", StringSchema()),
                    ("termTemplate", StringSchema()))))))))),
            ("kinds", Obj(("type", "array"), ("items", Obj(("type", "object"), ("properties", Obj(
                ("key", StringSchema()),
                ("kind", StringSchema()),
                ("title", StringSchema()),
                ("singularTitlePrefix", StringSchema()),
                ("template", StringSchema()),
                ("indexTemplate", StringSchema()),
                ("termTemplate", StringSchema()),
                ("indexEnabled", BoolSchema()),
                ("hierarchical", BoolSchema()))))))),
            ("outputMode", EnumSchema("both", "index", "term")),
            ("itemFields", StringArraySchema()),
            ("pageSize", IntSchema(1)),
            ("indexEnabled", BoolSchema()),
            ("pinField", StringSchema()),
            ("pinOrderField", StringSchema()),
            ("pinFieldBySource", Obj(("type", "object"))),
            ("pinOrderFieldBySource", Obj(("type", "object"))))));

    private static JsonObject DeploySchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("provider", StringSchema()),
            ("branch", StringSchema()),
            ("message", StringSchema()),
            ("cname", StringSchema()),
            ("keepHistory", BoolSchema()),
            ("options", Obj(("type", "object"))))));

    private static JsonObject SeoSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("title", StringSchema()),
            ("titleTemplate", StringSchema()),
            ("description", StringSchema()),
            ("ogImage", StringSchema()),
            ("favicon", StringSchema()),
            ("authorName", StringSchema()),
            ("robotsTxt", SeoRobotsTxtSchema()),
            ("schema", SeoSchemaDetailSchema()),
            ("organization", SeoOrganizationSchema()),
            ("geo", SeoGeoSchema()))));

    private static JsonObject SeoRobotsTxtSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("disallow", StringSchema()),
            ("userAgent", StringSchema()),
            ("sitemapUrl", StringSchema()))));

    private static JsonObject SeoSchemaDetailSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("type", StringSchema()),
            ("mode", StringSchema()))));

    private static JsonObject SeoOrganizationSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("name", StringSchema()),
            ("url", StringSchema()))));

    private static JsonObject SeoGeoSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("llmsTxt", BoolSchema()),
            ("llmsFullTxt", BoolSchema()),
            ("faqSchema", BoolSchema()),
            ("howToSchema", BoolSchema()))));

    private static JsonObject AnalyticsSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("measurementId", StringSchema()),
            ("provider", StringSchema()),
            ("template", StringSchema()))));

    private static JsonObject FeedSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("formats", StringArraySchema()),
            ("limit", IntSchema(1)),
            ("language", StringSchema()),
            ("authorName", StringSchema()))));

    private static JsonObject SitemapDetailSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("changefreq", StringSchema()),
            ("priority", Obj(("type", "number"), ("minimum", 0.0), ("maximum", 1.0))),
            ("lastmod", StringSchema()),
            ("priorityMode", EnumSchema("auto", "manual", "default")))));

    private static JsonObject PaginationGlobalSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("pageSize", IntSchema(1)),
            ("pagerTemplate", StringSchema()),
            ("pagePathPrefix", StringSchema()))));

    private static JsonObject SearchDetailSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("mode", EnumSchema("split", "merged", "index")),
            ("ui", StringSchema()),
            ("uiTheme", EnumSchema("light", "dark", "auto")),
            ("placeholderText", StringSchema()),
            ("maxContentLength", IntSchema(1)))));

    private static JsonObject RelatedSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("template", StringSchema()),
            ("maxResults", IntSchema(1)),
            ("scoreThreshold", Obj(("type", "number"))),
            ("fields", StringArraySchema()))));

    private static JsonObject MenusSchema()
        => Obj(
            ("type", "object"),
            ("additionalProperties", Obj(("type", "array"), ("items", MenuConfigItemSchema()))));

    private static JsonObject MenuConfigItemSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("label", StringSchema()),
            ("url", StringSchema()),
            ("target", StringSchema()),
            ("weight", IntSchema(0)),
            ("children", Obj(("type", "object"))))));

    private static JsonObject ExternalPluginsSchema()
        => Obj(
            ("type", "object"),
            ("additionalProperties", ExternalPluginItemSchema()));

    private static JsonObject ExternalPluginItemSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("runtime", EnumSchema("process")),
            ("entry", StringSchema()),
            ("hooks", Obj(("type", "array"), ("items", EnumSchema("after-build", "derive-pages")))),
            ("enabled", BoolSchema()),
            ("timeoutMs", IntSchema(1)),
            ("maxStdoutBytes", IntSchema(1)),
            ("maxStderrBytes", IntSchema(1)),
            ("allowEnvironment", StringArraySchema()),
            ("capabilities", Obj(("type", "array"), ("items", EnumSchema("emit-outputs", "derive-pages")))),
            ("templateRequirements", StringArraySchema()),
            ("allowAbsoluteEntry", BoolSchema()),
            ("sha256", StringSchema()),
            ("options", Obj(("type", "object"))))));

    private static JsonObject BuildReportSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("outputPath", StringSchema()),
            ("enabled", BoolSchema()),
            ("securityFailMode", EnumSchema("auto", "off", "warn", "strict")))));

    private static JsonObject ComponentDefinitionSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("template", StringSchema()),
            ("fields", StringArraySchema()),
            ("description", StringSchema()),
            ("schema", Obj(("type", "object"))),
            ("contextModes", StringArraySchema()))));

    private static JsonObject ScssSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("path", StringSchema()),
            ("entryPoint", StringSchema()),
            ("outDir", StringSchema()),
            ("includePaths", StringArraySchema()))));

    private static JsonObject ImageOptimizationSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("enabled", BoolSchema()),
            ("formats", StringArraySchema()),
            ("sizes", StringArraySchema()),
            ("quality", IntSchema(0)),
            ("lazy", BoolSchema()),
            ("resolutions", Obj(("type", "array"), ("items", IntSchema(1)))),
            ("concurrency", IntSchema(1)),
            ("contextConditional", StringSchema()))));

    private static JsonObject MediaSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("downloadImages", BoolSchema()),
            ("blockPrivateNetworks", BoolSchema()),
            ("imageRootPath", StringSchema()),
            ("maxImageSize", IntSchema(1)),
            ("concurrency", IntSchema(1)),
            ("contextConditional", StringSchema()),
            ("remoteProxyUrl", StringSchema()),
            ("retryCount", IntSchema(0)),
            ("timeoutSeconds", IntSchema(1)),
            ("cacheDir", StringSchema()),
            ("allowHosts", StringArraySchema()))));

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
            ("notion", NotionSchema()))));

    private static JsonObject NotionSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("token", StringSchema()),
            ("rootBlockId", StringSchema()),
            ("databaseId", StringSchema()),
            ("textOnly", BoolSchema()),
            ("propertyMap", NotionPropertyMapSchema()),
            ("fieldPolicies", NotionFieldPoliciesSchema()),
            ("pageSize", IntSchema(1)),
            ("maxItems", IntSchema(1)),
            ("renderContent", BoolSchema()),
            ("renderConcurrency", IntSchema(1)),
            ("maxRps", IntSchema(1)),
            ("maxRetries", IntSchema(0)),
            ("filterProperty", StringSchema()),
            ("filterType", StringSchema()),
            ("filterValue", StringSchema()),
            ("sortProperty", StringSchema()),
            ("sortDirection", EnumSchema("ascending", "descending")),
            ("includeSlugs", StringArraySchema()),
            ("includeSlugProperty", StringSchema()),
            ("cacheMode", EnumSchema("off", "readwrite", "readonly")),
            ("cacheDir", StringSchema()))));

    private static JsonObject NotionPropertyMapSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("slug", StringSchema()),
            ("lang", StringSchema()),
            ("body", StringSchema()),
            ("draft", StringSchema()),
            ("date", StringSchema()),
            ("type", StringSchema()),
            ("image", StringSchema()),
            ("tags", StringSchema()),
            ("categories", StringSchema()),
            ("pinned", StringSchema()),
            ("i18nKey", StringSchema()),
            ("summary", StringSchema()),
            ("commitMessage", StringSchema()),
            ("archive", StringSchema()),
            ("translations", StringSchema()),
            ("link", StringSchema()))));

    private static JsonObject NotionFieldPoliciesSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("mode", EnumSchema("auto", "discard", "lax")))));

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
        schema["required"] = Arr("field", "value", "listRoute");
        schema["properties"] = Obj(
            ("field", StringSchema()),
            ("value", StringSchema()),
            ("listRoute", StringSchema()),
            ("listTemplate", StringSchema()));
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
            ("scopedFields", Obj(("type", "object"), ("additionalProperties", Obj(("type", "array"), ("items", ContentModelFieldSchema()))))),
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
            ("field", StringSchema()),
            ("rawKey", StringSchema()),
            ("semanticType", StringSchema()),
            ("required", BoolSchema()))));

    private static JsonObject ContentModelFieldSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("name");
        schema["properties"] = Obj(
            ("name", StringSchema()),
            ("type", StringSchema()),
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
            ("reference", ContentReferenceRuleSchema()),
            ("referenceRule", ContentReferenceRuleSchema()));
        return schema;
    }

    private static JsonObject EntityMappingSchema()
        => Obj(("type", "object"), ("required", Arr("rawKey", "entityType")), ("properties", Obj(
            ("rawKey", StringSchema()),
            ("entityType", StringSchema()),
            ("type", StringSchema()),
            ("idField", StringSchema()),
            ("nameField", StringSchema()),
            ("descriptionField", StringSchema()),
            ("urlField", StringSchema()),
            ("sameAsField", StringSchema()),
            ("required", BoolSchema()),
            ("reference", ContentReferenceRuleSchema()),
            ("referenceRule", ContentReferenceRuleSchema()))));

    private static JsonObject RelationMappingSchema()
        => Obj(("type", "object"), ("required", Arr("rawKey", "relationType")), ("properties", Obj(
            ("rawKey", StringSchema()),
            ("relationType", StringSchema()),
            ("type", StringSchema()),
            ("targetType", StringSchema()),
            ("targetField", StringSchema()),
            ("labelField", StringSchema()),
            ("targetIdField", StringSchema()),
            ("idField", StringSchema()),
            ("required", BoolSchema()),
            ("reference", ContentReferenceRuleSchema()),
            ("referenceRule", ContentReferenceRuleSchema()))));

    private static JsonObject ContentReferenceRuleSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("targetType", StringSchema()),
            ("idField", StringSchema()),
            ("labelField", StringSchema()),
            ("nameField", StringSchema()),
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

        return obj;
    }
}
