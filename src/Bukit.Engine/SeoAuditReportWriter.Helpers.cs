using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SeoAuditReportWriter
{
    internal static bool IsAbsoluteHttpUrl(string value)
        => MachineReadabilityTrustAuditBuilder.IsAbsoluteHttpUrl(value);

    private static string BuildMergedKey(string language, string key) => language + "/" + key;

    private static string CombineBaseUrl(string baseUrl, string routeUrl)
    {
        var b = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var r = routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;
        return string.IsNullOrWhiteSpace(b) ? r : b + r;
    }
}
