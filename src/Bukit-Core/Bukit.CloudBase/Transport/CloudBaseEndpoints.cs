using System.Diagnostics.CodeAnalysis;
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

    /// <summary>True for an https link on the gateway's default port under the sign path of the bucket.</summary>
    internal static bool IsSignUrl(Uri uri, string gatewayHost, string bucket)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) &&
           uri.IsDefaultPort &&
           string.Equals(uri.Host, gatewayHost, StringComparison.OrdinalIgnoreCase) &&
           uri.AbsolutePath.StartsWith($"{SignPrefix}{bucket}/", StringComparison.Ordinal);

    internal static string StorageCdnHost(string envId)
        => envId + StorageSuffix;

    /// <summary>
    /// Parses <c>cloud://{envId}.{bucket}/{objectName}</c>. Only a reference to the given environment and
    /// bucket (ordinal match, non-empty bucket) with a valid object name resolves; build and doctor share
    /// this rule so doctor cannot pass a reference that build would reject.
    /// </summary>
    internal static bool TryParseCloudReference(string value, string envId, string? bucket, out string objectName)
    {
        objectName = string.Empty;
        if (!value.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = value.AsSpan("cloud://".Length);
        var dot = rest.IndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        var slash = rest[(dot + 1)..].IndexOf('/');
        if (slash < 0)
        {
            return false;
        }

        var referenceBucket = rest.Slice(dot + 1, slash);
        var path = rest[(dot + 1 + slash + 1)..].ToString();
        if (referenceBucket.IsEmpty ||
            !rest[..dot].Equals(envId, StringComparison.Ordinal) ||
            !referenceBucket.Equals(bucket, StringComparison.Ordinal) ||
            !IsValidObjectName(path))
        {
            return false;
        }

        objectName = path;
        return true;
    }

    /// <summary>A storage object name: non-blank, relative, with no empty or <c>..</c> segment.</summary>
    internal static bool IsValidObjectName([NotNullWhen(true)] string? name)
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
