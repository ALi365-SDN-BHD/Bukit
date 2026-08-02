using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Cli.Commands.SeoInsights;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Engine;

namespace Bukit.Cli.Commands;

internal static partial class SeoInsightsCommand
{
    private const int MaximumObservationFiles = 10;
    private const string RouteMapSchema = "https://bukit.dev/schemas/seo-route-map.v1.json";
    private const string RouteMapSchemaVersion = "1.0";

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

    internal static int Run(CliBoundCommand command)
    {
        try
        {
            var outputDirectory = ResolveLocalPath(command.GetString("--dir") ?? "dist", "dir_invalid");
            var routeMapPath = ResolveLocalPath(
                command.GetString("--routes") ?? Path.Combine(outputDirectory, ".bukit", "seo-route-map.json"),
                "routes_path_invalid");
            var rulePath = ResolveRequiredLocalPath(command.GetString("--rules"), "rules_required", "rules_path_invalid");
            var observationPaths = ResolveObservationPaths(command.GetString("--observations"));
            var outputPath = ResolveLocalPath(
                command.GetString("--out") ?? Path.Combine(outputDirectory, ".bukit", SeoInsightsReportWriter.FileName),
                "output_path_invalid");

            RejectOutputConflict(outputPath, routeMapPath, rulePath, observationPaths);

            var routeMap = ReadRouteMap(routeMapPath);
            var ruleProfile = ReadRuleProfile(rulePath);
            var datasets = observationPaths.Select(ReadObservationDataset).ToArray();
            var matcher = CreateMatcher(routeMap, ruleProfile);
            var report = AssembleReport(matcher, datasets, ruleProfile);

            WriteReport(outputPath, report);

            var counts = report.JoinQuality.Overall;
            var findingCount = report.Routes.Sum(route => (long)route.Findings.Count);
            var hasJoinGaps = counts.Unmatched != 0 || counts.Ambiguous != 0;
            var strictJoinFailed = command.GetBool("--strict-join") && hasJoinGaps;
            var classification = strictJoinFailed
                ? "strict-join-failed"
                : hasJoinGaps ? "join-gaps-allowed" : "complete";

            Console.WriteLine(
                $"SEO insights: sourceRows={counts.Total} matched={counts.Matched} unmatched={counts.Unmatched} ambiguous={counts.Ambiguous} findings={findingCount}");
            Console.WriteLine($"SEO insights report: {outputPath}");
            Console.WriteLine($"SEO insights classification: {classification}");
            return strictJoinFailed ? 1 : 0;
        }
        catch (SeoInsightsCommandException exception)
        {
            Console.Error.WriteLine($"SEO insights failed: {exception.Code}.");
            return 2;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"SEO insights failed: {StableDataCode(exception)}.");
            return 2;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("SEO insights failed: input_unavailable.");
            return 2;
        }
    }

    private static IReadOnlyList<string> ResolveObservationPaths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Failure("observations_required");
        }

        var entries = value.Split(',', StringSplitOptions.None);
        if (entries.Length is < 1 or > MaximumObservationFiles)
        {
            throw Failure("observations_count_invalid");
        }

        var comparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var unique = new HashSet<string>(comparer);
        var paths = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0)
            {
                throw Failure("observations_list_invalid");
            }

            var path = ResolveLocalPath(trimmed, "observations_path_invalid");
            var semanticPath = ResolveSemanticPath(path);
            if (!unique.Add(semanticPath))
            {
                throw Failure("observations_duplicate");
            }

            paths.Add(path);
        }

        return paths;
    }

    private static string ResolveRequiredLocalPath(string? value, string requiredCode, string invalidCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Failure(requiredCode);
        }

        return ResolveLocalPath(value, invalidCode);
    }

    private static string ResolveLocalPath(string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value) || IsUriOrNetworkPath(value))
        {
            throw Failure(code);
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or SecurityException or IOException)
        {
            throw Failure(code);
        }
    }

    private static string ResolveSemanticPath(string path)
    {
        try
        {
            return new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return path;
        }
    }

    private static bool IsUriOrNetworkPath(string path)
    {
        if (path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\')
        {
            return false;
        }

        return UriSchemeRegex().IsMatch(path);
    }

    private static void RejectOutputConflict(
        string outputPath,
        string routeMapPath,
        string rulePath,
        IReadOnlyList<string> observationPaths)
    {
        var comparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var outputSemanticPath = ResolveSemanticPath(outputPath);
        if (comparer.Equals(outputSemanticPath, ResolveSemanticPath(routeMapPath)) ||
            comparer.Equals(outputSemanticPath, ResolveSemanticPath(rulePath)) ||
            observationPaths.Any(path => comparer.Equals(outputSemanticPath, ResolveSemanticPath(path))))
        {
            throw Failure("output_conflict");
        }
    }

    private static SeoRouteMap ReadRouteMap(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = JsonDocument.Parse(stream);
            return ReadRouteMapDocument(document.RootElement);
        }
        catch (SeoInsightsCommandException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException)
        {
            throw Failure("route_map_invalid");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            throw Failure("route_map_unavailable");
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
            throw Failure("route_map_invalid");
        }

        if (!string.Equals(root.GetProperty("schema").GetString(), RouteMapSchema, StringComparison.Ordinal) ||
            !string.Equals(root.GetProperty("schemaVersion").GetString(), RouteMapSchemaVersion, StringComparison.Ordinal) ||
            !IsValidSiteUrl(siteUrl) ||
            !baseUrl.StartsWith("/", StringComparison.Ordinal))
        {
            throw Failure("route_map_invalid");
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
                throw Failure("route_map_invalid");
            }

            if (route.TryGetProperty("contentKey", out var contentKey) &&
                contentKey.ValueKind == JsonValueKind.String &&
                !ContentKeyRegex().IsMatch(contentKey.GetString()!))
            {
                throw Failure("route_map_invalid");
            }
        }

        SeoRouteMap? routeMap;
        try
        {
            routeMap = root.Deserialize(SeoRouteMapJsonContext.Default.SeoRouteMap);
        }
        catch (JsonException)
        {
            throw Failure("route_map_invalid");
        }

        if (routeMap is null || routeMap.Routes is null ||
            routeMap.Routes.Count != root.GetProperty("routes").GetArrayLength())
        {
            throw Failure("route_map_invalid");
        }

        return routeMap;
    }

    private static SeoInsightsRuleProfile ReadRuleProfile(string path)
    {
        try
        {
            return SeoInsightsRuleProfileReader.Read(path);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure("rules_unavailable");
        }
    }

    private static SeoObservationDataset ReadObservationDataset(string path)
    {
        try
        {
            return SeoObservationDatasetReader.Read(path);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure("observation_unavailable");
        }
    }

    private static SeoObservationRouteMatcher CreateMatcher(SeoRouteMap routeMap, SeoInsightsRuleProfile profile)
    {
        try
        {
            return new SeoObservationRouteMatcher(
                routeMap,
                new SeoObservationUrlOptions(
                    profile.SiteHost,
                    new HashSet<string>(profile.HostAliases, StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(profile.IgnoredQueryParameters, StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception)
        {
            throw Failure("route_map_invalid");
        }
    }

    private static SeoInsightsReport AssembleReport(
        SeoObservationRouteMatcher matcher,
        IReadOnlyList<SeoObservationDataset> datasets,
        SeoInsightsRuleProfile profile)
    {
        try
        {
            return SeoInsightsReportWriter.Assemble(matcher, datasets, profile);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure("report_invalid");
        }
    }

    private static void WriteReport(string outputPath, SeoInsightsReport report)
    {
        var parent = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw Failure("output_path_invalid");
        }

        var stagingRoot = Path.Combine(parent, $".seo-insights-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(parent);
            SeoInsightsReportWriter.Write(stagingRoot, report);
            var stagedReport = Path.Combine(stagingRoot, ".bukit", SeoInsightsReportWriter.FileName);
            File.Move(stagedReport, outputPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            throw Failure("output_unavailable");
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, recursive: true);
                }
            }
            catch (Exception)
            {
                // The report result has already been decided; never leak cleanup paths.
            }
        }
    }

    private static void ValidateObject(
        JsonElement value,
        IReadOnlySet<string> allowedProperties,
        IReadOnlySet<string> requiredProperties)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Failure("route_map_invalid");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name) || !allowedProperties.Contains(property.Name))
            {
                throw Failure("route_map_invalid");
            }
        }

        if (requiredProperties.Any(property => !names.Contains(property)))
        {
            throw Failure("route_map_invalid");
        }
    }

    private static string RequireString(JsonElement value, string property)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Failure("route_map_invalid");
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
            throw Failure("route_map_invalid");
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
            throw Failure("route_map_invalid");
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
           !string.IsNullOrWhiteSpace(uri.Host);

    private static string StableDataCode(InvalidDataException exception)
    {
        var separator = exception.Message.IndexOf(':');
        var code = separator < 0 ? exception.Message : exception.Message[..separator];
        if (!DataCodeRegex().IsMatch(code))
        {
            return "input_invalid";
        }

        return code.Replace('.', '_').Replace('-', '_');
    }

    private static SeoInsightsCommandException Failure(string code) => new(code);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex UriSchemeRegex();

    [GeneratedRegex("^route:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex RouteKeyRegex();

    [GeneratedRegex("^content:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ContentKeyRegex();

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DataCodeRegex();

    private sealed class SeoInsightsCommandException(string code) : Exception
    {
        internal string Code { get; } = code;
    }
}
