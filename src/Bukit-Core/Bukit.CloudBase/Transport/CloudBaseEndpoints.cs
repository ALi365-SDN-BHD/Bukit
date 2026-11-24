using System.Text.RegularExpressions;

namespace Bukit.CloudBase.Transport;

/// <summary>
/// Gateway host, storage host, path prefixes and the signed-link token parameter are defined here only.
/// </summary>
internal static partial class CloudBaseEndpoints
{
    internal const string RestPrefix = "/v1/rdb/rest/";
    internal const string SignPrefix = "/v1/storages/object/sign/";
    internal const string SignedUrlTokenParameter = "token";
    internal const string GatewaySuffix = ".api.tcloudbasegateway.com";
    internal const string IntlGatewaySuffix = ".api.intl.tcloudbasegateway.com";
    internal const string StorageSuffix = ".storage.tcloudbase.com";

    internal static string GatewayHost(string envId, string region)
        => region == "intl"
            ? envId + IntlGatewaySuffix
            : envId + GatewaySuffix;

    internal static string StorageCdnHost(string envId)
        => envId + StorageSuffix;

    /// <summary>A storage object name: non-blank, relative, with no empty or <c>..</c> segment.</summary>
    internal static bool IsValidObjectName([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in name.Split('/'))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}\\z", RegexOptions.CultureInvariant)]
    internal static partial Regex EnvIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,62}\\z", RegexOptions.CultureInvariant)]
    internal static partial Regex BucketPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,62}\\z", RegexOptions.CultureInvariant)]
    internal static partial Regex IdentifierPattern();
}
