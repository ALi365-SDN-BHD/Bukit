using System.Net;

namespace Bukit.Engine;

/// <summary>Shared HTML for alias, taxonomy and default-language redirect pages.</summary>
internal static class RedirectPageHtml
{
    /// <summary>Marks a derived route whose body HTML is already complete and must not be template-rendered.</summary>
    internal const string RouteTemplate = "__redirect__";

    internal static string Build(string targetUrl)
    {
        var escaped = WebUtility.HtmlEncode(targetUrl ?? string.Empty);
        return
            "<!DOCTYPE html>\n" +
            "<html>\n" +
            "<head>\n" +
            $"<meta http-equiv=\"refresh\" content=\"0; url={escaped}\">\n" +
            $"<link rel=\"canonical\" href=\"{escaped}\">\n" +
            "<meta name=\"robots\" content=\"noindex\">\n" +
            "<title>Redirect</title>\n" +
            "</head>\n" +
            "<body>\n" +
            $"<p>Redirecting to <a href=\"{escaped}\">{escaped}</a></p>\n" +
            "</body>\n" +
            "</html>\n";
    }
}
