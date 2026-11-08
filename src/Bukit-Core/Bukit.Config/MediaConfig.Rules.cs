using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Bukit.Config;

// Shared rules for content.media.onFailure and host patterns, used by configuration
// validation and by the media localizer so both always agree.
public sealed partial record MediaConfig
{
    /// <summary>
    /// Parses <c>onFailure</c>: "placeholder" (also when blank) or "keep", in any case.
    /// </summary>
    public static bool TryParseOnFailure(string? value, out bool keepOriginal)
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
    public static bool TryNormalizeHostPattern(string? pattern, out string normalized)
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

    private static bool IsCanonicalIPv4(string value)
        => IPAddress.TryParse(value, out var address) &&
           address.AddressFamily == AddressFamily.InterNetwork &&
           address.ToString() == value;
}
