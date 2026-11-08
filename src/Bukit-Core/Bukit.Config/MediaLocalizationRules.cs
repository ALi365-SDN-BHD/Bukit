using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Bukit.Config;

/// <summary>
/// Shared rules for <c>content.media.onFailure</c> and host patterns, used by configuration
/// validation and by the media localizer (Bukit.Content) so both always agree.
/// </summary>
internal static class MediaLocalizationRules
{
    /// <summary>
    /// Parses <c>onFailure</c>: "placeholder" (also when blank) or "keep", in any case.
    /// </summary>
    internal static bool TryParseOnFailure(string? value, out bool keepOriginal)
    {
        var trimmed = value?.Trim();
        keepOriginal = string.Equals(trimmed, "keep", StringComparison.OrdinalIgnoreCase);
        return keepOriginal ||
               string.IsNullOrEmpty(trimmed) ||
               string.Equals(trimmed, "placeholder", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates and normalizes a host pattern: an exact host name, a canonical IPv4 address,
    /// or "*." plus a host name. The result is lowercase ASCII (punycode for IDN names).
    /// </summary>
    internal static bool TryNormalizeHostPattern(string? pattern, out string normalized)
    {
        normalized = string.Empty;
        var value = pattern?.Trim() ?? string.Empty;
        var wildcard = value.StartsWith("*.", StringComparison.Ordinal);
        if (wildcard)
        {
            value = value[2..];
        }

        if (value.Length == 0 || value.StartsWith('.') || value.EndsWith('.'))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (ch is '*' or '/' or ':' or '@' or '?' or '#' or '[' or ']' || char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        // Lowercase first: invariant-globalization IDN conversion does no case folding.
        value = value.ToLowerInvariant();
        if (!value.All(char.IsAscii))
        {
            try
            {
                value = new IdnMapping().GetAscii(value);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        var valid = Uri.CheckHostName(value) switch
        {
            UriHostNameType.Dns => true,
            // Only canonical dotted-decimal: URL hosts are canonicalized ("010.0.0.1" becomes "8.0.0.1").
            UriHostNameType.IPv4 => !wildcard && IsCanonicalIPv4(value),
            _ => false
        };
        if (!valid)
        {
            return false;
        }

        normalized = wildcard ? "*." + value : value;
        return true;
    }

    /// <summary>
    /// Normalizes a pattern list, skipping blank entries. On failure <paramref name="invalid"/>
    /// is the first invalid entry.
    /// </summary>
    internal static bool TryNormalizeHostPatterns(
        IReadOnlyList<string>? patterns,
        out string[] normalized,
        out string? invalid)
    {
        normalized = [];
        invalid = null;
        if (patterns is null || patterns.Count == 0)
        {
            return true;
        }

        var result = new List<string>(patterns.Count);
        foreach (var raw in patterns)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (!TryNormalizeHostPattern(raw, out var pattern))
            {
                invalid = raw;
                return false;
            }

            result.Add(pattern);
        }

        normalized = result.Distinct(StringComparer.Ordinal).ToArray();
        return true;
    }

    internal static string InvalidHostPatternMessage(string fieldName, string? pattern)
        => $"{fieldName} entry '{pattern}' must be a host name, a canonical IPv4 address, or '*.' followed by a host name.";

    internal const string InvalidOnFailureMessage = "content.media.onFailure must be one of: placeholder, keep.";

    private static bool IsCanonicalIPv4(string value)
        => IPAddress.TryParse(value, out var address) &&
           address.AddressFamily == AddressFamily.InterNetwork &&
           address.ToString() == value;
}
