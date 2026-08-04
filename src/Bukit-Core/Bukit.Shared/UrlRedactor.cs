using System.Globalization;

namespace Bukit.Shared;

public static class UrlRedactor
{
    /// <summary>
    /// Reduces a URL to a log-safe identity: normalized scheme, host and optional port
    /// followed by a fixed redacted path marker. Userinfo, path, query and fragment are
    /// never logged because any of them may carry credentials or tokens. Unparseable
    /// values are replaced by a fixed marker instead of being echoed.
    /// </summary>
    public static string Redact(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            string.IsNullOrEmpty(uri.Scheme) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return "<redacted-url>";
        }

        var port = uri.IsDefaultPort
            ? string.Empty
            : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return $"{uri.Scheme}://{uri.Host}{port}/<redacted-path>";
    }
}
