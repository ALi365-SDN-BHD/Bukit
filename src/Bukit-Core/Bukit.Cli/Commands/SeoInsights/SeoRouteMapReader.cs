using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Engine;

namespace Bukit.Cli.Commands.SeoInsights;

internal static partial class SeoRouteMapReader
{
    internal const string Schema = "https://bukit.dev/schemas/seo-route-map.v1.json";
    internal const string SchemaVersion = "1.0";

    private static readonly HashSet<string> RouteMapProperties =
        ["schema", "schemaVersion", "generatedAt", "siteUrl", "baseUrl", "routes"];
    private static readonly HashSet<string> RouteProperties =
    [
        "routeKey", "contentKey", "route", "canonical", "language", "contentType", "collection",
        "indexable", "publishedAt", "updatedAt"
    ];
    private static readonly HashSet<string> RequiredRouteProperties =
    [
        "routeKey", "route", "canonical", "language", "contentType", "collection", "indexable",
        "publishedAt", "updatedAt"
    ];

    internal static SeoRouteMap Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = JsonDocument.Parse(stream);
            return ReadRouteMapDocument(document.RootElement);
        }
        catch (RouteMapDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException)
        {
            throw Invalid("route_map_invalid");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            throw Invalid("route_map_unavailable");
        }
    }

    private static SeoRouteMap ReadRouteMapDocument(JsonElement root)
    {
        ValidateObject(root, RouteMapProperties, RouteMapProperties);
        RequireString(root, "schema");
        RequireString(root, "schemaVersion");
        RequireDateTimeOffset(root, "generatedAt", nullable: false);
        var siteUrl = RequireString(root, "siteUrl");
        var baseUrl = RequireString(root, "baseUrl");
        if (root.GetProperty("routes").ValueKind != JsonValueKind.Array)
        {
            throw Invalid("route_map_invalid");
        }

        if (!string.Equals(root.GetProperty("schema").GetString(), Schema, StringComparison.Ordinal) ||
            !string.Equals(root.GetProperty("schemaVersion").GetString(), SchemaVersion, StringComparison.Ordinal) ||
            !IsValidSiteUrl(siteUrl) ||
            !baseUrl.StartsWith("/", StringComparison.Ordinal))
        {
            throw Invalid("route_map_invalid");
        }

        var routeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in root.GetProperty("routes").EnumerateArray())
        {
            ValidateObject(route, RouteProperties, RequiredRouteProperties);
            var routeKey = RequireString(route, "routeKey");
            var routePath = RequireString(route, "route");
            var canonical = RequireString(route, "canonical");
            ValidateNullableString(route, "contentKey");
            ValidateNullableString(route, "language");
            ValidateNullableString(route, "contentType");
            ValidateNullableString(route, "collection");
            RequireDateTimeOffset(route, "publishedAt", nullable: true);
            RequireDateTimeOffset(route, "updatedAt", nullable: true);
            if (route.GetProperty("indexable").ValueKind is not JsonValueKind.True and not JsonValueKind.False ||
                !RouteKeyRegex().IsMatch(routeKey) ||
                !routeKeys.Add(routeKey) ||
                !routePath.StartsWith("/", StringComparison.Ordinal) ||
                !IsValidCanonical(canonical))
            {
                throw Invalid("route_map_invalid");
            }

            if (route.TryGetProperty("contentKey", out var contentKey) &&
                contentKey.ValueKind == JsonValueKind.String &&
                !ContentKeyRegex().IsMatch(contentKey.GetString()!))
            {
                throw Invalid("route_map_invalid");
            }
        }

        SeoRouteMap? routeMap;
        try
        {
            routeMap = root.Deserialize(SeoRouteMapJsonContext.Default.SeoRouteMap);
        }
        catch (JsonException)
        {
            throw Invalid("route_map_invalid");
        }

        if (routeMap is null || routeMap.Routes is null ||
            routeMap.Routes.Count != root.GetProperty("routes").GetArrayLength())
        {
            throw Invalid("route_map_invalid");
        }

        return routeMap;
    }

    private static void ValidateObject(
        JsonElement value,
        IReadOnlySet<string> allowedProperties,
        IReadOnlySet<string> requiredProperties)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("route_map_invalid");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name) || !allowedProperties.Contains(property.Name))
            {
                throw Invalid("route_map_invalid");
            }
        }

        if (requiredProperties.Any(property => !names.Contains(property)))
        {
            throw Invalid("route_map_invalid");
        }
    }

    private static string RequireString(JsonElement value, string property)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid("route_map_invalid");
        }

        return element.GetString()!;
    }

    private static void ValidateNullableString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var element))
        {
            return;
        }

        if (element.ValueKind is not JsonValueKind.String and not JsonValueKind.Null)
        {
            throw Invalid("route_map_invalid");
        }
    }

    private static void RequireDateTimeOffset(JsonElement value, string property, bool nullable)
    {
        var element = value.GetProperty(property);
        if (nullable && element.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.String || !element.TryGetDateTimeOffset(out _))
        {
            throw Invalid("route_map_invalid");
        }
    }

    private static bool IsValidSiteUrl(string value)
        => value.Length == 0 || IsAbsoluteHttpUrl(value);

    private static bool IsValidCanonical(string value)
        => value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal) ||
           IsAbsoluteHttpUrl(value);

    private static bool IsAbsoluteHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           uri.Scheme is "http" or "https" &&
           !string.IsNullOrWhiteSpace(uri.Host) &&
           string.IsNullOrEmpty(uri.UserInfo);

    private static RouteMapDataException Invalid(string code) => new(code);

    [GeneratedRegex("^route:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex RouteKeyRegex();

    [GeneratedRegex("^content:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ContentKeyRegex();

    internal sealed class RouteMapDataException(string code) : Exception(code)
    {
        internal string Code { get; } = code;
    }
}
