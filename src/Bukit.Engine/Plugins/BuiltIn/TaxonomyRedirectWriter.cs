using System.Text;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyRedirectWriter
{
    internal static void WriteRedirects(string outputDir, string kind, Dictionary<string, TaxonomyTerm> terms)
    {
        foreach (var term in terms.Values)
        {
            if (term.Aliases is not { Count: > 0 })
            {
                continue;
            }

            var targetUrl = $"/{kind}/{term.Slug}/";

            foreach (var alias in term.Aliases)
            {
                var aliasSlug = alias.Trim();
                if (string.IsNullOrWhiteSpace(aliasSlug))
                {
                    continue;
                }

                if (aliasSlug.Equals(term.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var aliasDir = Path.Combine(outputDir, kind, aliasSlug);
                Directory.CreateDirectory(aliasDir);

                var html = RenderRedirect(targetUrl);
                File.WriteAllText(Path.Combine(aliasDir, "index.html"), html, Encoding.UTF8);
            }
        }
    }

    private static string RenderRedirect(string targetUrl)
    {
        var escaped = EscapeHtml(targetUrl);
        return $"<!DOCTYPE html>\n<html>\n<head>\n<meta http-equiv=\"refresh\" content=\"0;url={escaped}\">\n<link rel=\"canonical\" href=\"{escaped}\">\n<title>Redirect</title>\n</head>\n<body>\n<p>Redirecting to <a href=\"{escaped}\">{escaped}</a></p>\n</body>\n</html>\n";
    }

    private static string EscapeHtml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }
}
