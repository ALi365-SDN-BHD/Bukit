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
            ("taxonomy", Obj(("type", "object"))),
            ("logging", Obj(("type", "object"), ("properties", Obj(("level", EnumSchema("debug", "info", "warn", "error")))))),
            ("deploy", Obj(("type", "object"))));

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
            ("autoSummary", BoolSchema()),
            ("autoSummaryMaxLength", IntSchema(1)),
            ("baseUrl", StringSchema()),
            ("outputPathEncoding", EnumSchema("none", "slug", "urlencode", "sanitize")),
            ("language", StringSchema()),
            ("languages", StringArraySchema()),
            ("defaultLanguage", StringSchema()),
            ("sitemapMode", EnumSchema("split", "root")),
            ("rssMode", EnumSchema("split", "root")),
            ("searchMode", EnumSchema("split", "root")),
            ("pluginFailMode", EnumSchema("strict", "warn", "ignore")),
            ("deriveConflictPolicy", EnumSchema("fail", "warn", "first", "last")),
            ("timezone", StringSchema()),
            ("collections", Obj(("type", "object"))),
            ("plugins", Obj(("type", "object"))));
        return schema;
    }

    private static JsonObject ContentSchema()
    {
        var schema = Obj(("type", "object"));
        schema["required"] = Arr("provider");
        schema["properties"] = Obj(
            ("provider", EnumSchema("markdown", "notion", "sources")),
            ("markdown", Obj(("type", "object"), ("properties", Obj(
                ("dir", StringSchema()),
                ("defaultType", StringSchema()),
                ("maxItems", IntSchema(1)),
                ("includePaths", StringArraySchema()),
                ("includeGlobs", StringArraySchema()))))),
            ("notion", Obj(("type", "object"))),
            ("sources", Obj(("type", "array"))),
            ("media", Obj(("type", "object"))));
        return schema;
    }

    private static JsonObject BuildSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("output", StringSchema()),
            ("clean", BoolSchema()),
            ("draft", BoolSchema()),
            ("listPageContentMode", EnumSchema("auto", "summary", "none", "full")),
            ("schemaFailMode", EnumSchema("off", "warn", "strict")))));

    private static JsonObject ThemeSchema()
        => Obj(("type", "object"), ("properties", Obj(
            ("name", StringSchema()),
            ("source", StringSchema()),
            ("extends", StringSchema()),
            ("layouts", StringSchema()),
            ("assets", StringSchema()),
            ("static", StringSchema()),
            ("staticTemplate", StringSchema()),
            ("componentValidation", EnumSchema("off", "warn", "strict")))));

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
