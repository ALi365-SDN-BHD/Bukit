using System.Net;
using System.Text.RegularExpressions;

namespace Bukit.Content.Notion;

internal static class NotionHtmlLinkExpiry
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static DateTimeOffset? GetEarliestExpiry(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        DateTimeOffset? earliest = null;
        foreach (Match match in UrlRegex.Matches(html))
        {
            var decoded = WebUtility.HtmlDecode(match.Value);
            if (!Uri.TryCreate(decoded, UriKind.Absolute, out var uri) ||
                !TryGetExpiry(uri, out var expiry))
            {
                continue;
            }

            if (earliest is null || expiry < earliest.Value)
            {
                earliest = expiry;
            }
        }

        return earliest;
    }

    private static bool TryGetExpiry(Uri uri, out DateTimeOffset expiry)
    {
        expiry = default;
        var query = ParseQuery(uri.Query);

        if (TryGetDate(query, "X-Amz-Date", out var awsDate) &&
            TryGetSeconds(query, "X-Amz-Expires", out var awsExpires) &&
            TryAddSeconds(awsDate, awsExpires, out expiry))
        {
            return true;
        }

        if (TryGetDate(query, "X-Goog-Date", out var googleDate) &&
            TryGetSeconds(query, "X-Goog-Expires", out var googleExpires) &&
            TryAddSeconds(googleDate, googleExpires, out expiry))
        {
            return true;
        }

        if (TryGetUnixSeconds(query, "Expires", out var unixExpiry))
        {
            expiry = unixExpiry;
            return true;
        }

        if (query.TryGetValue("expiry_time", out var rawExpiry) &&
            DateTimeOffset.TryParse(rawExpiry, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out expiry))
        {
            return true;
        }

        return false;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            values[WebUtility.UrlDecode(pair[..separator])] = WebUtility.UrlDecode(pair[(separator + 1)..]);
        }

        return values;
    }

    private static bool TryGetDate(
        IReadOnlyDictionary<string, string> query,
        string key,
        out DateTimeOffset value)
    {
        value = default;
        return query.TryGetValue(key, out var raw) &&
               DateTimeOffset.TryParseExact(
                   raw,
                   "yyyyMMdd'T'HHmmss'Z'",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                   out value);
    }

    private static bool TryGetSeconds(
        IReadOnlyDictionary<string, string> query,
        string key,
        out double value)
    {
        value = 0;
        return query.TryGetValue(key, out var raw) &&
               double.TryParse(raw, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out value) &&
               value >= 0;
    }

    private static bool TryAddSeconds(DateTimeOffset start, double seconds, out DateTimeOffset value)
    {
        value = default;
        try
        {
            value = start.AddSeconds(seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryGetUnixSeconds(
        IReadOnlyDictionary<string, string> query,
        string key,
        out DateTimeOffset value)
    {
        value = default;
        if (!query.TryGetValue(key, out var raw) ||
            !long.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds) ||
            seconds is <= 0 or > 253402300799)
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }
}
