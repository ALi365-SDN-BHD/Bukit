using System.Text;
using Bukit.Engine.Output;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyRedirectWriter
{
    internal static void WriteRedirects(string outputDir, string kind, Dictionary<string, TaxonomyTerm> terms, string? routePrefix = null, ICollection<string>? generatedPaths = null)
    {
        var redirects = new List<(string RelativePath, string FullPath, string TargetUrl)>();
        // Self aliases write nothing but their output path is still validated before any write.
        foreach (var (relativePath, targetUrl, isSelfAlias) in PlanAliases(kind, terms, routePrefix))
        {
            try
            {
                var path = FileWriter.GetSafeFullPath(outputDir, relativePath);
                if (!isSelfAlias)
                {
                    redirects.Add((relativePath, path, targetUrl));
                }
            }
            catch (Exception ex) when (ex is OutputPathSecurityException or ArgumentException)
            {
                throw new ConfigException($"Unsafe taxonomy redirect output '{relativePath}'.", ex, DiagnosticCode.BuildOutputUnsafe);
            }
        }

        foreach (var (relativePath, path, targetUrl) in redirects)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, RedirectPageHtml.Build(targetUrl), Encoding.UTF8);
            generatedPaths?.Add(relativePath);
        }
    }

    /// <summary>Alias outputs (relative, '/'-separated) and their targets, without writing.</summary>
    private static List<(string Path, string TargetUrl, bool IsSelfAlias)> PlanAliases(string kind, Dictionary<string, TaxonomyTerm> terms, string? routePrefix)
    {
        var normalizedRoutePrefix = TaxonomyPageCreator.NormalizeRoutePrefix(kind, routePrefix);
        var redirects = new List<(string Path, string TargetUrl, bool IsSelfAlias)>();
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
                redirects.Add((
                    BuildPathUtils.NormalizeRelPath(Path.Combine(aliasUrl.Trim('/'), "index.html")),
                    targetUrl,
                    aliasSlug.Equals(term.Slug, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return redirects;
    }
}
