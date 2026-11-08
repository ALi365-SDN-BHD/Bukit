using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Content.Media;

/// <summary>
/// Decides from the original URL's host whether an image is downloaded.
/// Patterns follow <see cref="MediaLocalizationRules.TryNormalizeHostPattern"/>; skip rules win.
/// </summary>
internal sealed class MediaHostFilter
{
    private readonly string[] _downloadHosts;
    private readonly string[] _skipHosts;

    /// <exception cref="ConfigException">A pattern is not a valid host pattern.</exception>
    internal MediaHostFilter(IReadOnlyList<string>? downloadHosts, IReadOnlyList<string>? skipHosts)
    {
        _downloadHosts = Normalize(downloadHosts, "content.media.downloadHosts");
        _skipHosts = Normalize(skipHosts, "content.media.skipHosts");
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

    private static string[] Normalize(IReadOnlyList<string>? patterns, string fieldName)
        => MediaLocalizationRules.TryNormalizeHostPatterns(patterns, out var normalized, out var invalid)
            ? normalized
            : throw new ConfigException(
                MediaLocalizationRules.InvalidHostPatternMessage(fieldName, invalid),
                DiagnosticCode.ConfigInvalidValue);
}
