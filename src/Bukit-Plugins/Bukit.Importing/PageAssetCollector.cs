using System.Text.RegularExpressions;

namespace Bukit.Importing;

/// <summary>
/// Collects the stylesheets, styles and scripts a demo page declares so they can be
/// written into that page's own template. Nothing here is shared between pages.
/// </summary>
internal static partial class PageAssetCollector
{
    internal sealed record PageAssets(
        IReadOnlyList<string> Head,
        IReadOnlyList<string> BodyStart,
        IReadOnlyList<string> Scripts);

    internal static PageAssets Collect(DiscoveredPage page, Dictionary<string, string> pathMappings)
    {
        var head = new List<string>();
        var bodyStart = new List<string>();
        var scripts = new List<string>();

        // Head order is kept as authored: links, styles and scripts interleave on purpose.
        foreach (Match match in AssetTagRegex.Matches(StripComments(page.HeadContent)))
        {
            var tag = match.Value;
            if (IsLink(tag) && IsCanonicalLink(tag))
                continue;
            head.Add(Render(tag, pathMappings));
        }

        // Before <main> the page may still load styles (kept in <head>) and scripts
        // (kept where they were, before the content). Scripts after </main> stay at the end.
        foreach (Match match in AssetTagRegex.Matches(StripComments(page.BodyOpening)))
        {
            var tag = match.Value;
            if (IsScript(tag))
                bodyStart.Add(Render(tag, pathMappings));
            else if (!IsLink(tag) || IsStylesheetLink(tag))
                head.Add(Render(tag, pathMappings));
        }

        foreach (Match match in AssetTagRegex.Matches(StripComments(page.BodyClosing)))
        {
            var tag = match.Value;
            if (IsScript(tag))
                scripts.Add(Render(tag, pathMappings));
            else if (!IsLink(tag) || IsStylesheetLink(tag))
                head.Add(Render(tag, pathMappings));
        }

        return new PageAssets(head, bodyStart, scripts);
    }

    /// <summary>Removes page assets from shared partials; each page template owns them.</summary>
    internal static string StripAssets(string html)
        => AssetTagRegex.Replace(html, match =>
            IsScript(match.Value) || IsStyle(match.Value) || IsStylesheetLink(match.Value) ? "" : match.Value);

    private static string Render(string tag, Dictionary<string, string> pathMappings)
    {
        if (IsStyle(tag))
            return RewriteCssUrls(EscapeScriban(tag.Trim()), pathMappings);

        if (IsScript(tag) && !SrcAttributeRegex.IsMatch(tag))
            return EscapeScriban(tag.Trim());

        return RewriteAssetTag(tag.Trim(), pathMappings);
    }

    /// <summary>Rewrites asset attributes through the mappings and prefixes base_url.</summary>
    internal static string RewriteAssetTag(string tag, Dictionary<string, string> pathMappings)
        => RewriteAssetAttributes(AssetImporter.RewritePaths(tag, pathMappings), pathMappings);

    internal static string RewriteAssetAttributes(string html, Dictionary<string, string> pathMappings)
    {
        html = SrcsetAttributeRegex.Replace(html, match =>
        {
            var attribute = match.Groups[1].Value;
            var value = RewriteSrcset(match.Groups[2].Value, pathMappings, "{{ base_url }}");
            return $"{attribute}=\"{value}\"";
        });
        return RootRelativeAttributeRegex.Replace(
            html,
            m => $"{m.Groups[1].Value}=\"{{{{ base_url }}}}{m.Groups[2].Value}\"");
    }

    internal static string RewriteSrcsets(string html, Dictionary<string, string> pathMappings, string basePath)
        => SrcsetAttributeRegex.Replace(html, match =>
        {
            var attribute = match.Groups[1].Value;
            var value = RewriteSrcset(match.Groups[2].Value, pathMappings, basePath);
            return $"{attribute}=\"{value}\"";
        });

    private static string RewriteSrcset(
        string srcset,
        Dictionary<string, string> pathMappings,
        string basePath)
    {
        var candidates = new List<string>();
        foreach (var candidate in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = candidate.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            var url = parts[0];
            if (pathMappings.TryGetValue(url, out var mapped))
                url = mapped;
            if (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
                url = basePath + url;

            parts[0] = url;
            candidates.Add(string.Join(' ', parts));
        }

        return string.Join(", ", candidates);
    }

    // Inline CSS/JS is literal text; Scriban delimiters inside it must not execute.
    private static string EscapeScriban(string text) => ScribanText.Escape(text);

    internal static string RewriteCssUrls(string css, Dictionary<string, string> pathMappings)
        => CssUrlRegex.Replace(css, match =>
        {
            var quote = match.Groups["quote"].Value;
            var value = match.Groups["value"].Value.Trim();
            var cut = value.IndexOfAny(['?', '#']);
            var path = cut < 0 ? value : value[..cut];
            var suffix = cut < 0 ? "" : value[cut..];

            if (!pathMappings.TryGetValue(path, out var mapped) || !mapped.StartsWith('/'))
                return match.Value;

            return $"url({quote}{{{{ base_url }}}}{mapped}{suffix}{quote})";
        });

    private static string StripComments(string? html)
        => string.IsNullOrEmpty(html) ? "" : CommentRegex.Replace(html, "");

    private static bool IsScript(string tag) => tag.StartsWith("<script", StringComparison.OrdinalIgnoreCase);

    private static bool IsStyle(string tag) => tag.StartsWith("<style", StringComparison.OrdinalIgnoreCase);

    private static bool IsLink(string tag) => tag.StartsWith("<link", StringComparison.OrdinalIgnoreCase);

    private static bool IsStylesheetLink(string tag) => IsLink(tag) && StylesheetRel.IsMatch(tag);

    private static bool IsCanonicalLink(string tag) => IsLink(tag) && CanonicalRel.IsMatch(tag);

    [GeneratedRegex(@"\brel\s*=\s*[""']?[^""'>]*\bstylesheet\b", RegexOptions.IgnoreCase)]
    private static partial Regex StylesheetRelPattern();
    private static readonly Regex StylesheetRel = StylesheetRelPattern();

    [GeneratedRegex(@"\brel\s*=\s*[""']?[^""'>]*\bcanonical\b", RegexOptions.IgnoreCase)]
    private static partial Regex CanonicalRelPattern();
    private static readonly Regex CanonicalRel = CanonicalRelPattern();

    [GeneratedRegex(@"<script\b[^>]*>.*?</script\s*>|<style\b[^>]*>.*?</style\s*>|<link\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AssetTagPattern();
    private static readonly Regex AssetTagRegex = AssetTagPattern();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();
    private static readonly Regex CommentRegex = CommentPattern();

    [GeneratedRegex(@"^<script\b[^>]*\bsrc\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex SrcAttributePattern();
    private static readonly Regex SrcAttributeRegex = SrcAttributePattern();

    [GeneratedRegex(@"\b(href|src|data-src|poster|data-poster)=[""'](/(?!/)[^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex RootRelativeAttributePattern();
    private static readonly Regex RootRelativeAttributeRegex = RootRelativeAttributePattern();

    [GeneratedRegex(@"\b(srcset|data-srcset)\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex SrcsetAttributePattern();
    private static readonly Regex SrcsetAttributeRegex = SrcsetAttributePattern();


    [GeneratedRegex(@"url\(\s*(?<quote>['""]?)(?<value>[^'"")]+)\k<quote>\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrlPattern();
    private static readonly Regex CssUrlRegex = CssUrlPattern();
}
