using Bukit.Config;

namespace Bukit.Content.Media;

/// <summary>
/// Decides from the original URL's host whether an image is downloaded.
/// Patterns follow <see cref="MediaConfig.TryNormalizeHostPattern"/>; skip rules win.
/// </summary>
internal sealed class MediaHostFilter
{
    private readonly string[] _downloadHosts;
    private readonly string[] _skipHosts;

    /// <exception cref="ArgumentException">A pattern is not a valid host pattern.</exception>
    internal MediaHostFilter(IReadOnlyList<string>? downloadHosts, IReadOnlyList<string>? skipHosts)
    {
        _downloadHosts = Normalize(downloadHosts, nameof(downloadHosts));
        _skipHosts = Normalize(skipHosts, nameof(skipHosts));
    }

    internal bool ShouldDownload(Uri uri)
    {
        if (_downloadHosts.Length == 0 && _skipHosts.Length == 0)
        {
            return true;
        }

        // IdnHost is already ASCII (punycode) and canonical; only case and a trailing dot need normalizing.
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
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

    private static string[] Normalize(IReadOnlyList<string>? patterns, string paramName)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return [];
        }

        var result = new List<string>(patterns.Count);
        foreach (var raw in patterns)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (!MediaConfig.TryNormalizeHostPattern(raw, out var normalized))
            {
                throw new ArgumentException($"Invalid media host pattern '{raw}'.", paramName);
            }

            result.Add(normalized);
        }

        return result.Distinct(StringComparer.Ordinal).ToArray();
    }
}
