using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands.SeoInsights;

internal static class SeoInsightsRuleProfileReader
{
    internal const string Schema = "https://bukit.dev/schemas/seo-insights-rules.v1.json";
    internal const string SchemaVersion = "1.0";

    private static readonly HashSet<string> RootProperties =
        ["schema", "schemaVersion", "siteHost", "hostAliases", "ignoredQueryParameters", "thresholds", "priorities"];
    private static readonly HashSet<string> ThresholdProperties =
    [
        "minimumSearchImpressions", "maximumLowImpressions", "minimumAnalyticsSessions", "lowCtr",
        "lowEngagementRate", "highEngagementRate", "opportunityPositionMinimum", "opportunityPositionMaximum"
    ];
    private static readonly HashSet<string> PriorityProperties =
        ["snippetMismatch", "landingQuality", "discoverability", "positionOpportunity"];

    internal static SeoInsightsRuleProfile Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRemoteUri(path))
        {
            throw Invalid("rules.path_invalid", "A local SEO insight rule file path is required.");
        }

        try
        {
            using var stream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = JsonDocument.Parse(stream);
            return ReadDocument(document.RootElement);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid("rules.json_invalid", "SEO insight rules are not valid JSON.", exception);
        }
    }

    private static SeoInsightsRuleProfile ReadDocument(JsonElement root)
    {
        ValidateObject(root, RootProperties, "rules.json_invalid");

        var schema = ReadRequiredString(root, "schema", "rules.schema_invalid");
        var schemaVersion = ReadRequiredString(root, "schemaVersion", "rules.schema_invalid");
        if (!string.Equals(schema, Schema, StringComparison.Ordinal) ||
            !string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid("rules.schema_invalid", "SEO insight rules must use the exact v1 contract.");
        }

        var thresholdsElement = root.GetProperty("thresholds");
        ValidateObject(thresholdsElement, ThresholdProperties, "rules.threshold_invalid");
        ValidateThresholdShape(thresholdsElement);

        var prioritiesElement = root.GetProperty("priorities");
        ValidateObject(prioritiesElement, PriorityProperties, "rules.priority_invalid");
        ValidatePriorityShape(prioritiesElement);

        var siteHost = NormalizeDnsHost(ReadRequiredString(root, "siteHost", "rules.host_invalid"));
        var hostAliases = ReadHostAliases(root.GetProperty("hostAliases"), siteHost);
        var ignoredQueryParameters = ReadIgnoredParameters(root.GetProperty("ignoredQueryParameters"));

        SeoInsightsRuleProfile? profile;
        try
        {
            profile = root.Deserialize(SeoInsightsRuleJsonContext.Default.SeoInsightsRuleProfile);
        }
        catch (JsonException exception)
        {
            throw Invalid("rules.json_invalid", "SEO insight rule values do not match the v1 contract.", exception);
        }

        if (profile is null)
        {
            throw Invalid("rules.json_invalid", "SEO insight rule profile is empty.");
        }

        ValidateThresholds(profile.Thresholds);
        ValidatePriorities(profile.Priorities);
        return profile with
        {
            SiteHost = siteHost,
            HostAliases = hostAliases,
            IgnoredQueryParameters = ignoredQueryParameters
        };
    }

    private static void ValidateObject(JsonElement value, IReadOnlySet<string> properties, string kindCode)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(kindCode, "SEO insight rule object has an invalid shape.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw Invalid("rules.duplicate_field", "SEO insight rule object contains a duplicate field.");
            }

            if (!properties.Contains(property.Name))
            {
                throw Invalid("rules.unknown_field", $"Unknown SEO insight rule field '{property.Name}'.");
            }
        }

        foreach (var property in properties)
        {
            if (!names.Contains(property))
            {
                throw Invalid("rules.field_required", $"SEO insight rule field '{property}' is required.");
            }
        }
    }

    private static void ValidateThresholdShape(JsonElement thresholds)
    {
        ValidateCount(thresholds, "minimumSearchImpressions");
        ValidateCount(thresholds, "maximumLowImpressions");
        ValidateCount(thresholds, "minimumAnalyticsSessions");
        ValidateRatio(thresholds, "lowCtr");
        ValidateRatio(thresholds, "lowEngagementRate");
        ValidateRatio(thresholds, "highEngagementRate");
        var minimum = ValidateFiniteNumber(thresholds, "opportunityPositionMinimum");
        var maximum = ValidateFiniteNumber(thresholds, "opportunityPositionMaximum");
        if (minimum <= 0 || minimum > maximum)
        {
            throw Invalid("rules.threshold_invalid", "SEO insight opportunity-position thresholds are invalid.");
        }
    }

    private static void ValidateThresholds(SeoInsightsThresholds thresholds)
    {
        if (thresholds is null ||
            thresholds.MinimumSearchImpressions < 0 ||
            thresholds.MaximumLowImpressions < 0 ||
            thresholds.MinimumAnalyticsSessions < 0 ||
            !IsRatio(thresholds.LowCtr) ||
            !IsRatio(thresholds.LowEngagementRate) ||
            !IsRatio(thresholds.HighEngagementRate) ||
            !double.IsFinite(thresholds.OpportunityPositionMinimum) ||
            !double.IsFinite(thresholds.OpportunityPositionMaximum) ||
            thresholds.OpportunityPositionMinimum <= 0 ||
            thresholds.OpportunityPositionMinimum > thresholds.OpportunityPositionMaximum)
        {
            throw Invalid("rules.threshold_invalid", "SEO insight thresholds are invalid.");
        }
    }

    private static void ValidatePriorityShape(JsonElement priorities)
    {
        foreach (var property in PriorityProperties)
        {
            var value = priorities.GetProperty(property);
            if (value.ValueKind != JsonValueKind.String || !IsPriority(value.GetString()))
            {
                throw Invalid("rules.priority_invalid", "SEO insight priorities must be exact P0, P1, or P2 values.");
            }
        }
    }

    private static void ValidatePriorities(SeoInsightsPriorities priorities)
    {
        if (priorities is null ||
            !IsPriority(priorities.SnippetMismatch) ||
            !IsPriority(priorities.LandingQuality) ||
            !IsPriority(priorities.Discoverability) ||
            !IsPriority(priorities.PositionOpportunity))
        {
            throw Invalid("rules.priority_invalid", "SEO insight priorities must be exact P0, P1, or P2 values.");
        }
    }

    private static IReadOnlyList<string> ReadHostAliases(JsonElement aliases, string siteHost)
    {
        if (aliases.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("rules.host_invalid", "SEO insight host aliases must be an array of DNS hosts.");
        }

        var values = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { siteHost };
        foreach (var alias in aliases.EnumerateArray())
        {
            if (alias.ValueKind != JsonValueKind.String)
            {
                throw Invalid("rules.host_invalid", "SEO insight host aliases must be DNS hosts.");
            }

            var normalized = NormalizeDnsHost(alias.GetString()!);
            if (!unique.Add(normalized))
            {
                throw Invalid("rules.alias_duplicate", "SEO insight host aliases contain an ambiguous duplicate.");
            }

            values.Add(normalized);
        }

        return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> ReadIgnoredParameters(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("rules.parameter_invalid", "Ignored query parameters must be an array of names.");
        }

        var values = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.ValueKind != JsonValueKind.String ||
                !Regex.IsMatch(parameter.GetString()!, "^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant))
            {
                throw Invalid("rules.parameter_invalid", "Ignored query parameter name is invalid.");
            }

            var value = parameter.GetString()!;
            if (!unique.Add(value))
            {
                throw Invalid("rules.parameter_duplicate", "Ignored query parameters contain an ambiguous duplicate.");
            }

            values.Add(value);
        }

        return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string NormalizeDnsHost(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() ||
            value.IndexOfAny([':', '/', '@', '?', '#', '[', ']']) >= 0)
        {
            throw Invalid("rules.host_invalid", "SEO insight host must be a DNS host without URI components.");
        }

        var host = value.EndsWith(".", StringComparison.Ordinal) ? value[..^1] : value;
        if (host.Length == 0 || host.Length > 253 || host.Any(character => character > 127) ||
            Regex.IsMatch(host, "^[0-9]+(?:\\.[0-9]+){3}$", RegexOptions.CultureInvariant))
        {
            throw Invalid("rules.host_invalid", "SEO insight host must be a valid DNS host.");
        }

        foreach (var label in host.Split('.'))
        {
            if (label.Length is < 1 or > 63 ||
                !char.IsAsciiLetterOrDigit(label[0]) ||
                !char.IsAsciiLetterOrDigit(label[^1]) ||
                label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                throw Invalid("rules.host_invalid", "SEO insight host must be a valid DNS host.");
            }
        }

        return host.ToLowerInvariant();
    }

    private static void ValidateCount(JsonElement thresholds, string property)
    {
        var value = thresholds.GetProperty(property);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var count) || count < 0)
        {
            throw Invalid("rules.threshold_invalid", "SEO insight count threshold is invalid.");
        }
    }

    private static void ValidateRatio(JsonElement thresholds, string property)
    {
        var value = ValidateFiniteNumber(thresholds, property);
        if (!IsRatio(value))
        {
            throw Invalid("rules.threshold_invalid", "SEO insight ratio threshold is invalid.");
        }
    }

    private static double ValidateFiniteNumber(JsonElement value, string property)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetDouble(out var number) ||
            !double.IsFinite(number))
        {
            throw Invalid("rules.threshold_invalid", "SEO insight numeric threshold must be finite.");
        }

        return number;
    }

    private static bool IsRatio(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    private static bool IsPriority(string? value) => value is "P0" or "P1" or "P2";

    private static string ReadRequiredString(JsonElement value, string property, string code)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid(code, $"SEO insight rule field '{property}' must be a string.");
        }

        return element.GetString()!;
    }

    private static bool IsRemoteUri(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is not "file";

    private static InvalidDataException Invalid(string code, string detail, Exception? inner = null)
        => new($"{code}: {detail}", inner);
}
