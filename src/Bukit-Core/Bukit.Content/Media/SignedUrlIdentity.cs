using System.Globalization;

namespace Bukit.Content.Media;

/// <summary>
/// The request target used for a media file's identity: signature and expiry parameters of
/// a recognized signing scheme are removed, so re-signing the same object keeps its file
/// name. Everything else, including the parameter order and spelling, is kept as is. The
/// download itself always uses the full, signed URL.
/// </summary>
internal static class SignedUrlIdentity
{
    private static readonly string[] AwsV4Parameters =
    [
        "X-Amz-Algorithm", "X-Amz-Credential", "X-Amz-Date", "X-Amz-Expires",
        "X-Amz-SignedHeaders", "X-Amz-Signature", "X-Amz-Security-Token"
    ];

    private static readonly string[] GoogleV4Parameters =
    [
        "X-Goog-Algorithm", "X-Goog-Credential", "X-Goog-Date", "X-Goog-Expires",
        "X-Goog-SignedHeaders", "X-Goog-Signature"
    ];

    private static readonly string[] V2Parameters =
    [
        "Signature", "Expires", "Policy", "AWSAccessKeyId", "GoogleAccessId", "Key-Pair-Id"
    ];

    private static readonly string[] NotionParameters = ["expirationTimestamp", "signature"];

    /// <summary>The escaped request target (no fragment), as sent on the wire.</summary>
    internal static string RequestTarget(Uri uri)
        => uri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped);

    /// <summary>
    /// The escaped request target without recognized signature parameters. Names are matched
    /// case-insensitively on the escaped text as <see cref="Uri"/> normalizes it (escaped
    /// unreserved chars such as <c>%2D</c> are already restored, as on the wire); other escapes
    /// are not decoded, so a name such as <c>X-Amz-Signature%26</c> is kept. When nothing is
    /// removed the result is the request target unchanged.
    /// </summary>
    internal static string IdentityTarget(Uri uri)
    {
        var target = RequestTarget(uri);
        var queryStart = target.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return target;
        }

        var segments = target[(queryStart + 1)..].Split('&');
        var names = segments.Select(ParameterName).ToArray();
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (HasAny(names, "X-Amz-Signature"))
        {
            removed.UnionWith(AwsV4Parameters);
        }

        if (HasAny(names, "X-Goog-Signature"))
        {
            removed.UnionWith(GoogleV4Parameters);
        }

        if (HasAny(names, "Signature") && HasAny(names, "AWSAccessKeyId", "GoogleAccessId", "Key-Pair-Id"))
        {
            removed.UnionWith(V2Parameters);
        }

        if (IsNotionHost(uri.Host))
        {
            removed.UnionWith(NotionParameters);
        }

        if (removed.Count == 0 || !names.Any(removed.Contains))
        {
            return target;
        }

        var kept = segments.Where((_, index) => !removed.Contains(names[index])).ToArray();
        var path = target[..queryStart];
        return kept.Length == 0 ? path : path + "?" + string.Join('&', kept);
    }

    /// <summary>
    /// True when the URL carries a recognized signing scheme whose own expiry time is at or
    /// before <paramref name="now"/>. A refusal of such a URL cannot succeed on retry. False
    /// when no scheme is recognized or its expiry cannot be read.
    /// </summary>
    internal static bool IsSignatureExpired(Uri uri, DateTimeOffset now)
        => TryGetSignatureExpiry(uri, out var expiry) && expiry <= now;

    internal static bool TryGetSignatureExpiry(Uri uri, out DateTimeOffset expiry)
    {
        expiry = default;
        var target = RequestTarget(uri);
        var queryStart = target.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return false;
        }

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in target[(queryStart + 1)..].Split('&'))
        {
            var equals = segment.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                parameters.TryAdd(segment[..equals], Uri.UnescapeDataString(segment[(equals + 1)..]));
            }
        }

        if (parameters.ContainsKey("X-Amz-Signature"))
        {
            return TryV4Expiry(parameters, "X-Amz-Date", "X-Amz-Expires", out expiry);
        }

        if (parameters.ContainsKey("X-Goog-Signature"))
        {
            return TryV4Expiry(parameters, "X-Goog-Date", "X-Goog-Expires", out expiry);
        }

        if (parameters.ContainsKey("Signature") &&
            (parameters.ContainsKey("AWSAccessKeyId") || parameters.ContainsKey("GoogleAccessId") ||
             parameters.ContainsKey("Key-Pair-Id")))
        {
            return TryUnixExpiry(parameters, "Expires", milliseconds: false, out expiry);
        }

        if (IsNotionHost(uri.Host) && parameters.ContainsKey("signature"))
        {
            return TryUnixExpiry(parameters, "expirationTimestamp", milliseconds: true, out expiry);
        }

        return false;
    }

    // V4 schemes: a signing time (yyyyMMdd'T'HHmmss'Z') plus a lifetime in seconds.
    private static bool TryV4Expiry(
        Dictionary<string, string> parameters, string dateName, string expiresName, out DateTimeOffset expiry)
    {
        expiry = default;
        if (!parameters.TryGetValue(dateName, out var date) ||
            !parameters.TryGetValue(expiresName, out var expires) ||
            !DateTimeOffset.TryParseExact(
                date,
                "yyyyMMdd'T'HHmmss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var signedAt) ||
            !int.TryParse(expires, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds > (DateTimeOffset.MaxValue - signedAt).TotalSeconds)
        {
            return false;
        }

        expiry = signedAt.AddSeconds(seconds);
        return true;
    }

    private static bool TryUnixExpiry(
        Dictionary<string, string> parameters, string name, bool milliseconds, out DateTimeOffset expiry)
    {
        expiry = default;
        if (!parameters.TryGetValue(name, out var value) ||
            !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        try
        {
            expiry = milliseconds
                ? DateTimeOffset.FromUnixTimeMilliseconds(number)
                : DateTimeOffset.FromUnixTimeSeconds(number);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string ParameterName(string segment)
    {
        var equals = segment.IndexOf('=', StringComparison.Ordinal);
        return equals < 0 ? segment : segment[..equals];
    }

    private static bool HasAny(string[] names, params string[] wanted)
        => names.Any(name => wanted.Contains(name, StringComparer.OrdinalIgnoreCase));

    private static bool IsNotionHost(string host)
        => host.Equals("notion.so", StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith(".notion.so", StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith(".notion.site", StringComparison.OrdinalIgnoreCase);
}
