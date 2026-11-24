using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SeoAuditReportWriter
{
    internal static bool IsAbsoluteHttpUrl(string value)
        => MachineReadabilityTrustAuditBuilder.IsAbsoluteHttpUrl(value);

    // Merged keys are the merged output path, matching the merged route's OutputPath.
    private static string BuildMergedKey(string language, string key, IReadOnlyDictionary<string, string> outputPrefixes)
        => BuildPathUtils.NormalizeRelPath(CombineOutputPath(language, key, outputPrefixes));

    private static string CombineBaseUrl(string baseUrl, string routeUrl)
    {
        var b = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var r = routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;
        return string.IsNullOrWhiteSpace(b) ? r : b + r;
    }
}
