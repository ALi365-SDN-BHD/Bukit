using System.Globalization;

namespace Bukit.Content.Media;

/// <summary>
/// Decides from the original URL's host whether an image is downloaded.
/// Patterns are exact hosts or "*." plus a host (subdomains only); skip rules win.
/// </summary>
internal sealed class MediaHostFilter
{
    private static readonly IdnMapping s_idn = new();
    private readonly string[] _downloadHosts;
    private readonly string[] _skipHosts;

    internal MediaHostFilter(IReadOnlyList<string>? downloadHosts, IReadOnlyList<string>? skipHosts)
    {
        _downloadHosts = Normalize(downloadHosts);
        _skipHosts = Normalize(skipHosts);
    }

    internal bool ShouldDownload(Uri uri)
    {
        if (_downloadHosts.Length == 0 && _skipHosts.Length == 0)
        {
            return true;
        }

        var host = NormalizeHost(uri.IdnHost);
        if (MatchesAny(host, _skipHosts))
        {
            return false;
        }

        return _downloadHosts.Length == 0 || MatchesAny(host, _downloadHosts);
    }

    private static bool MatchesAny(string host, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                // "*.example.com" matches "a.example.com" but not "example.com".
                if (host.Length > pattern.Length - 1 &&
                    host.EndsWith(pattern[1..], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (host.Equals(pattern, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Normalize(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return [];
        }

        var result = new List<string>(patterns.Count);
        foreach (var raw in patterns)
        {
            var pattern = raw?.Trim() ?? string.Empty;
            if (pattern.Length == 0)
            {
                continue;
            }

            var wildcard = pattern.StartsWith("*.", StringComparison.Ordinal);
            var host = NormalizeHost(wildcard ? pattern[2..] : pattern);
            if (host.Length > 0)
            {
                result.Add(wildcard ? "*." + host : host);
            }
        }

        return result.ToArray();
    }

    private static string NormalizeHost(string host)
    {
        // Lowercase first: invariant-globalization IDN conversion does no case folding.
        var value = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            value = s_idn.GetAscii(value);
        }
        catch (ArgumentException)
        {
            // Keep the raw value; it simply will not match a valid host.
        }

        return value;
    }
}
