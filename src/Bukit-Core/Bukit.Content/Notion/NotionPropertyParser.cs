using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content.Notion;

public static class NotionPropertyParser
{
    public static IReadOnlyDictionary<string, ContentField> ExtractFields(JsonElement properties)
        => NotionContentPropertyParser.ExtractFields(properties);

    public static IReadOnlyDictionary<string, ContentField> ExtractAllFields(JsonElement properties)
        => NotionContentPropertyParser.ExtractAllFields(properties);

    internal static IReadOnlyDictionary<string, ContentField> ExtractFields(
        JsonElement properties,
        string policyMode,
        HashSet<string>? allowed,
        out IReadOnlyList<string> relationKeys)
        => NotionContentPropertyParser.ExtractFields(properties, policyMode, allowed, out relationKeys);

    internal static string? ExtractTitle(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
        => NotionContentPropertyParser.ExtractTitle(properties, propertyMap);

    internal static string? ExtractTitleProperty(JsonElement property)
        => NotionContentPropertyParser.ExtractTitleProperty(property);

    internal static string? ExtractSlug(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
        => NotionContentPropertyParser.ExtractSlug(properties, propertyMap);

    internal static string? ExtractType(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
        => NotionContentPropertyParser.ExtractType(properties, propertyMap);

    internal static string? ExtractCollection(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
        => NotionContentPropertyParser.ExtractCollection(properties, propertyMap);

    internal static DateTimeOffset? ExtractPublishAt(JsonElement properties, NotionPropertyMapConfig? propertyMap = null)
        => NotionContentPropertyParser.ExtractPublishAt(properties, propertyMap);

    internal static DateTimeOffset? ReadDateProperty(JsonElement property)
        => NotionContentPropertyParser.ReadDateProperty(property);

    internal static bool IsReservedNotionField(string normalizedKey)
        => NotionContentPropertyParser.IsReservedNotionField(normalizedKey);

    internal static string NormalizeFieldKey(string text)
        => NotionContentPropertyParser.NormalizeFieldKey(text);

    internal static string? GetRichTextPlain(JsonElement property)
        => NotionContentPropertyParser.GetRichTextPlain(property);

    internal static void ProjectSeoFields(
        Dictionary<string, object> projectedValues,
        JsonElement properties,
        NotionPropertyMapConfig? propertyMap)
        => NotionContentPropertyParser.ProjectSeoFields(projectedValues, properties, propertyMap);

    internal static void ProjectCanonicalFields(
        Dictionary<string, object> projectedValues,
        JsonElement properties,
        NotionPropertyMapConfig? propertyMap,
        string pageId)
        => NotionContentPropertyParser.ProjectCanonicalFields(projectedValues, properties, propertyMap, pageId);
}
