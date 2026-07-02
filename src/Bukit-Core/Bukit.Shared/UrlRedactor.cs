namespace Bukit.Shared;

public static class UrlRedactor
{
    /// <summary>
    /// Strips query string and fragment from a URL to avoid logging sensitive tokens.
    /// Returns the original string if it cannot be parsed as a URI.
    /// </summary>
    public static string Redact(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var qIdx = url.IndexOf('?');
        var fIdx = url.IndexOf('#');

        if (qIdx < 0 && fIdx < 0)
        {
            return url;
        }

        var cutoff = (qIdx >= 0, fIdx >= 0) switch
        {
            (true, true) => Math.Min(qIdx, fIdx),
            (true, false) => qIdx,
            (false, true) => fIdx,
            _ => url.Length
        };

        return url[..cutoff] + "?[REDACTED]";
    }
}
