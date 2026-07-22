using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands;

internal static partial class SeoReportValidator
{
    internal const string SeoReportSchema = "https://bukit.dev/schemas/seo-report.v1.json";
    internal const string PublishAuditReportSchema = "https://bukit.dev/schemas/publish-audit-report.v1.json";

    internal enum AuditReportContract
    {
        SeoOnly,
        PublishOnly,
        SeoOrPublish
    }

    internal static AuditReportContract ValidateReportContract(JsonElement root, AuditReportContract contractMode)
        => AuditReportContractValidator.ValidateReportContract(root, contractMode);

    internal static void ValidateSeoReportContract(JsonElement root)
        => SeoAuditReportContractValidator.Validate(root);

    internal static void ValidatePublishReportContract(JsonElement root)
        => PublishAuditReportContractValidator.Validate(root);

    internal static string? ReadString(JsonElement element, string property)
        => AuditReportJsonReader.ReadString(element, property);

    internal static JsonElement ReadRequiredObject(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadRequiredObject(element, path, property);

    internal static JsonElement ReadRequiredArray(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadRequiredArray(element, path, property);

    internal static string ReadRequiredString(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadRequiredString(element, path, property);

    internal static void ReadOptionalString(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadOptionalString(element, path, property);

    internal static int ReadRequiredInt(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadRequiredInt(element, path, property);

    internal static void ReadOptionalInt(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadOptionalInt(element, path, property);

    internal static int? TryReadOptionalInt(JsonElement element, string property)
        => AuditReportJsonReader.TryReadOptionalInt(element, property);

    internal static bool ReadRequiredBool(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadRequiredBool(element, path, property);

    internal static void ReadOptionalBool(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadOptionalBool(element, path, property);

    internal static void ReadOptionalStringArray(JsonElement element, string path, string property)
        => AuditReportJsonReader.ReadOptionalStringArray(element, path, property);

    internal static void EnsureObject(JsonElement element, string path)
        => AuditReportJsonReader.EnsureObject(element, path);

    internal static void EnsureAllowedProperties(JsonElement element, string path, params string[] allowed)
        => AuditReportJsonReader.EnsureAllowedProperties(element, path, allowed);

    internal static int? ReadOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, out var result) || result < 0)
        {
            throw new InvalidDataException($"Expected a non-negative integer, got '{value}'.");
        }

        return result;
    }

    internal static IReadOnlySet<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    internal sealed record SeoReportSnapshot(
        IReadOnlyDictionary<string, SeoRouteSnapshot> Routes,
        IReadOnlyList<SeoIssueSnapshot> Issues)
    {
        public static SeoReportSnapshot From(JsonElement root)
            => SeoReportDiffSnapshotReader.Read(root);
    }

    internal sealed record SeoRouteSnapshot(string Url, bool Indexable);

    internal sealed record SeoIssueSnapshot(string Severity, string Code, string? Route, string Message)
    {
        public string SortKey => $"{Severity}\u001f{Route}\u001f{Code}\u001f{Message}";
    }

    [GeneratedRegex(@"<meta\b(?=[^>]*(?:property|name)\s*=\s*[""'](?:og:image|twitter:image)[""'])(?=[^>]*content\s*=\s*[""']([^""']+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex SocialImageRegex();

    [GeneratedRegex(@"<img\b(?=[^>]*src\s*=\s*[""']([^""']+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex ImageSourceRegex();

    [GeneratedRegex(@"<a\b(?=[^>]*href\s*=\s*[""']([^""'#]+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex AnchorHrefRegex();
}
