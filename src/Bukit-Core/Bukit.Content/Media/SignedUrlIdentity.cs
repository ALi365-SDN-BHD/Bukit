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
