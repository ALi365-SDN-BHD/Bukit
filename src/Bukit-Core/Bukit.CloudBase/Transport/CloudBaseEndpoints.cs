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

    internal static string GatewayHost(string envId, string region)
        => region == "intl"
            ? $"{envId}.api.intl.tcloudbasegateway.com"
            : $"{envId}.api.tcloudbasegateway.com";

    internal static string StorageCdnHost(string envId)
        => $"{envId}.storage.tcloudbase.com";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    internal static partial Regex EnvIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,62}$", RegexOptions.CultureInvariant)]
    internal static partial Regex BucketPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,62}$", RegexOptions.CultureInvariant)]
    internal static partial Regex IdentifierPattern();
}
