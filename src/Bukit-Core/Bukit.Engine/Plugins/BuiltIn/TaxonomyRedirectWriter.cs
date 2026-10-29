using System.Text;
using Bukit.Engine.Output;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyRedirectWriter
{
    internal static void WriteRedirects(string outputDir, string kind, Dictionary<string, TaxonomyTerm> terms, string? routePrefix = null, ICollection<string>? generatedPaths = null)
    {
        var normalizedRoutePrefix = TaxonomyPageCreator.NormalizeRoutePrefix(kind, routePrefix);
        var redirects = new List<(string Path, string TargetUrl)>();
        foreach (var term in terms.Values)
        {
            if (term.Aliases is not { Count: > 0 })
            {
                continue;
            }

            var targetUrl = TaxonomyPageCreator.BuildTermUrl(normalizedRoutePrefix, term.Slug);

            foreach (var alias in term.Aliases)
            {
                var aliasSlug = alias.Trim();
                if (string.IsNullOrWhiteSpace(aliasSlug))
                {
                    continue;
                }

                if (aliasSlug.StartsWith('/')
                    || aliasSlug.Contains('\\')
                    || aliasSlug.Contains(':')
                    || aliasSlug.Split('/').Any(segment => segment is "." or ".."))
                {
                    throw new ConfigException($"Unsafe taxonomy redirect alias '{aliasSlug}'. Use a safe relative slash-separated alias without '.' or '..' segments.", DiagnosticCode.BuildOutputUnsafe);
                }

                var aliasUrl = TaxonomyPageCreator.BuildTermUrl(normalizedRoutePrefix, aliasSlug);
                try
                {
                    var path = FileWriter.GetSafeFullPath(outputDir, Path.Combine(aliasUrl.Trim('/'), "index.html"));
                    if (!aliasSlug.Equals(term.Slug, StringComparison.OrdinalIgnoreCase))
                    {
                        redirects.Add((path, targetUrl));
                    }
                }
                catch (Exception ex) when (ex is OutputPathSecurityException or ArgumentException)
                {
                    throw new ConfigException($"Unsafe taxonomy redirect output for alias '{aliasSlug}'.", ex, DiagnosticCode.BuildOutputUnsafe);
                }
            }
        }

        foreach (var (path, targetUrl) in redirects)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, RenderRedirect(targetUrl), Encoding.UTF8);
            generatedPaths?.Add(BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(outputDir, path)));
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
