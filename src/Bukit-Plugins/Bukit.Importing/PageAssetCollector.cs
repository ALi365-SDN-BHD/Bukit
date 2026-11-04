using System.Text.RegularExpressions;

namespace Bukit.Importing;

/// <summary>
/// Collects the stylesheets, styles and scripts a demo page declares so they can be
/// written into that page's own template. Nothing here is shared between pages.
/// </summary>
internal static partial class PageAssetCollector
{
    internal sealed record PageAssets(IReadOnlyList<string> Head, IReadOnlyList<string> Scripts);

    internal static PageAssets Collect(DiscoveredPage page, Dictionary<string, string> pathMappings)
    {
        var head = new List<string>();
        var scripts = new List<string>();

        // Head order is kept as authored: links, styles and scripts interleave on purpose.
        foreach (Match match in AssetTagRegex.Matches(StripComments(page.HeadContent)))
        {
            var tag = match.Value;
            if (IsLink(tag) && IsCanonicalLink(tag))
                continue;
            head.Add(Render(tag, pathMappings));
        }

        // Outside <main> the page may still load styles (kept in <head>) and scripts
        // (kept before </body>, where the page put them).
        var outsideMain = StripComments(page.BodyOpening) + "\n" + StripComments(page.BodyClosing);
        foreach (Match match in AssetTagRegex.Matches(outsideMain))
        {
            var tag = match.Value;
            if (IsScript(tag))
                scripts.Add(Render(tag, pathMappings));
            else if (!IsLink(tag) || IsStylesheetLink(tag))
                head.Add(Render(tag, pathMappings));
        }

        return new PageAssets(head, scripts);
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

    /// <summary>Rewrites href/src through the asset mappings and prefixes base_url.</summary>
    internal static string RewriteAssetTag(string tag, Dictionary<string, string> pathMappings)
    {
        var rewritten = AssetImporter.RewritePaths(tag, pathMappings);
        return RootRelativeAttributeRegex.Replace(
            rewritten,
            m => $"{m.Groups[1].Value}=\"{{{{ base_url }}}}{m.Groups[2].Value}\"");
    }

    // Inline CSS/JS is literal text; Scriban delimiters inside it must not execute.
    private static string EscapeScriban(string text)
        => ScribanDelimiterRegex.Replace(text, m => "{{ '" + m.Value + "' }}");

    private static string RewriteCssUrls(string css, Dictionary<string, string> pathMappings)
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

    private static bool IsStylesheetLink(string tag) => IsLink(tag) && RelRegex("stylesheet").IsMatch(tag);

    private static bool IsCanonicalLink(string tag) => RelRegex("canonical").IsMatch(tag);

    private static Regex RelRegex(string rel)
        => new($@"\brel\s*=\s*[""']?[^""'>]*\b{rel}\b", RegexOptions.IgnoreCase);

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

    [GeneratedRegex(@"\b(href|src)=[""'](/(?!/)[^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex RootRelativeAttributePattern();
    private static readonly Regex RootRelativeAttributeRegex = RootRelativeAttributePattern();

    [GeneratedRegex(@"\{%\{|\}%\}|\{\{|\}\}")]
    private static partial Regex ScribanDelimiterPattern();
    private static readonly Regex ScribanDelimiterRegex = ScribanDelimiterPattern();

    [GeneratedRegex(@"url\(\s*(?<quote>['""]?)(?<value>[^'"")]+)\k<quote>\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrlPattern();
    private static readonly Regex CssUrlRegex = CssUrlPattern();
}
