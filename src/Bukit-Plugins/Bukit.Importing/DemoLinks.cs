namespace Bukit.Importing;

/// <summary>Resolves links between files of an HTML demo.</summary>
internal static class DemoLinks
{
    /// <summary>
    /// Returns the demo-relative path ("news/a.html") that an internal href points to,
    /// resolved against the page that contains it, or null for external and in-page links.
    /// </summary>
    internal static string? ResolvePath(string sourceRelativePath, string href)
    {
        href = href.Trim();
        if (href.Length == 0 ||
            href.StartsWith('#') ||
            href.StartsWith("//", StringComparison.Ordinal) ||
            href.Contains("{{", StringComparison.Ordinal) ||
            href.Contains(':'))
        {
            // Covers http:, https:, mailto:, tel:, javascript:, data: and template output.
            return null;
        }

        var cut = href.IndexOfAny(['?', '#']);
        var path = cut < 0 ? href : href[..cut];
        if (path.Length == 0)
            return null;

        string combined;
        if (path.StartsWith('/'))
        {
            combined = path.TrimStart('/');
        }
        else
        {
            var dir = Path.GetDirectoryName(Normalize(sourceRelativePath))?.Replace('\\', '/') ?? "";
            combined = dir.Length == 0 ? path : dir + "/" + path;
        }

        var segments = new List<string>();
        foreach (var segment in combined.Split('/'))
        {
            if (segment is "" or ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                    return null;
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(Uri.UnescapeDataString(segment));
        }

        var resolved = string.Join('/', segments);
        if (resolved.Length == 0 || path.EndsWith('/'))
            resolved = resolved.Length == 0 ? "index.html" : resolved + "/index.html";
        return resolved;
    }

    internal static string Normalize(string relativePath) => relativePath.Replace('\\', '/');

    /// <summary>The same page under the other HTML extension, if it has one.</summary>
    internal static string? AlternatePageExtension(string path)
    {
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            return path[..^1];
        if (path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            return path + "l";
        return null;
    }
}
