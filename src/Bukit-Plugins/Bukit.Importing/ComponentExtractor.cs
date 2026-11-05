using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class ComponentExtractor
{
    // Only data-bound components are extracted. Section components (hero, cta, faq,
    // stats, testimonial) are the demo's own copy; they stay in the page template.
    private static readonly Dictionary<string, string> ClassToComponent = new(StringComparer.OrdinalIgnoreCase)
    {
        [".article-card"] = "article-card",
        [".post-card"] = "article-card",
        [".company-card"] = "company-card",
        [".service-card"] = "service-card",
        [".pagination"] = "pagination",
        [".card"] = "card",
    };

    private static readonly HashSet<string> ListComponentClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "article-card", "post-card", "company-card", "service-card", "card"
    };

    private static readonly Regex HtmlTagRegex = HtmlTagPattern();
    private static readonly Regex HeadingRegex = HeadingPattern();
    private static readonly Regex LinkRegex = LinkPattern();
    private static readonly Regex ParagraphRegex = ParagraphPattern();
    private static readonly Regex ClassAttrRegex = ClassAttrPattern();
    private static readonly Regex ImgTagRegex = ImgTagPattern();
    private static readonly Regex ImgSrcRegex = ImgSrcPattern();
    private static readonly Regex ImgLazyRegex = ImgLazyPattern();
    private static readonly Regex ImgSrcsetRegex = ImgSrcsetPattern();

    internal static List<DiscoveredComponent> Extract(List<DiscoveredPage> pages)
    {
        var components = new List<DiscoveredComponent>();

        foreach (var page in pages)
        {
            foreach (var (cssClass, componentName) in ClassToComponent)
            {
                var className = cssClass.StartsWith('.') ? cssClass[1..] : cssClass;
                if (!TemplateBodyTransformer.ContainsClass(page.FullHtml, className))
                    continue;

                var fragment = ExtractComponentHtml(page, cssClass);
                if (string.IsNullOrWhiteSpace(fragment))
                    continue;

                var normalized = DedupeKey(fragment);
                var existing = components.FirstOrDefault(c =>
                    string.Equals(c.SourceClass, className, StringComparison.OrdinalIgnoreCase) &&
                    DedupeKey(c.HtmlFragment) == normalized);
                if (existing is not null)
                {
                    if (!existing.UsedBy.Any(u => u.FilePath == page.FilePath))
                        existing.UsedBy.Add(page);
                    continue;
                }

                // Different markup for the same class is a different component, even
                // though both use the same demo class. Give each its own file.
                var name = componentName.Equals("pagination", StringComparison.OrdinalIgnoreCase)
                    ? componentName
                    : UniqueName(components, componentName);
                components.Add(new DiscoveredComponent
                {
                    Name = name,
                    SourceClass = className,
                    HtmlFragment = fragment,
                    UsedBy = [page],
                    NormalizedTemplate = GenerateTemplate(fragment, name, className)
                });
            }
        }

        return components;
    }

    private static string UniqueName(List<DiscoveredComponent> components, string baseName)
    {
        if (!components.Any(c => c.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        for (var n = 2; ; n++)
        {
            var name = $"{baseName}-{n}";
            if (!components.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return name;
        }
    }

    private static string ExtractComponentHtml(DiscoveredPage page, string cssClass)
    {
        var tagEnd = cssClass.StartsWith('.') ? cssClass[1..] : cssClass;
        var pattern = $@"<([a-zA-Z][a-zA-Z0-9]*)[^>]*?\bclass\s*=\s*[""'](?:[^""']*\s)?{Regex.Escape(tagEnd)}(?:\s[^""']*)?[""'][^>]*>";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var match = regex.Match(page.FullHtml);

        if (!match.Success) return "";

        var tagName = match.Groups[1].Value.ToLowerInvariant();
        var closingTag = $"</{tagName}>";

        var startIndex = match.Index;
        var rest = page.FullHtml[startIndex..];
        var depth = 1;
        var pos = match.Length;

        while (pos < rest.Length && depth > 0)
        {
            var nextOpen = rest.IndexOf($"<{tagName}", pos, StringComparison.OrdinalIgnoreCase);
            var nextClose = rest.IndexOf(closingTag, pos, StringComparison.OrdinalIgnoreCase);

            if (nextClose < 0) break;

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                pos = nextOpen + tagName.Length + 1;
            }
            else
            {
                depth--;
                pos = nextClose + closingTag.Length;
            }
        }

        return depth == 0 ? rest[..pos] : "";
    }

    private static string GenerateTemplate(string fragment, string componentName, string className)
    {
        fragment = ScribanText.Escape(fragment);
        if (className.Equals("pagination", StringComparison.OrdinalIgnoreCase))
        {
            return """
{{ if pagination.has_prev }}
<nav class="pagination" aria-label="Pagination">
  {{ if pagination.has_prev }}<a href="{{ pagination.prev_page }}" rel="prev">‹</a>{{ end }}
  <span>{{ pagination.page }} / {{ pagination.total_pages }}</span>
  {{ if pagination.has_next }}<a href="{{ pagination.next_page }}" rel="next">›</a>{{ end }}
</nav>
{{ end }}
""";
        }

        // Section data is not wired up yet, so keep the demo copy verbatim instead of
        // binding every heading/paragraph to page fields (which duplicated the body).
        if (!ListComponentClasses.Contains(className))
            return fragment;

        var content = ParagraphRegex.Replace(fragment, _ => "<p>{{ item.summary }}</p>");
        content = LinkRegex.Replace(content, _ =>
            "<a href=\"{{ base_url }}{{ item.url }}\">{{ item.title }}</a>");
        content = ImgTagRegex.Replace(content, match =>
        {
            var tag = match.Value;
            if (!ImgSrcRegex.IsMatch(tag) && !ImgLazyRegex.IsMatch(tag))
                return tag;
            if (IsExternalImage(tag))
                return tag;

            tag = ImgSrcRegex.Replace(tag, m =>
                $"{m.Groups[1].Value}=\"{{{{ base_url }}}}{{{{ item.fields.image.value }}}}\"");
            tag = ImgLazyRegex.Replace(tag, "");
            tag = ImgSrcsetRegex.Replace(tag, "");
            if (!ImgSrcRegex.IsMatch(tag))
                tag = tag.Insert(4, " src=\"{{ base_url }}{{ item.fields.image.value }}\"");
            return tag;
        });
        content = HeadingRegex.Replace(content, m =>
        {
            var tagName = m.Groups[1].Value;
            // A linked heading keeps its link, or the card no longer reaches its detail page.
            return m.Value.Contains("{{ item.url }}", StringComparison.Ordinal)
                ? $"<{tagName}><a href=\"{{{{ base_url }}}}{{{{ item.url }}}}\">{{{{ item.title }}}}</a></{tagName}>"
                : $"<{tagName}>{{{{ item.title }}}}</{tagName}>";
        });

        return content;
    }

    private static bool IsExternalImage(string tag)
    {
        var source = ImgLazyRegex.Match(tag) is { Success: true } lazy
            ? lazy.Groups[2].Value
            : ImgSrcRegex.Match(tag).Groups[2].Value;
        return source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               source.StartsWith("//", StringComparison.Ordinal) ||
               source.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    private static string DedupeKey(string html)
    {
        var structure = HtmlTagRegex.Replace(html, "<$1>");
        structure = Regex.Replace(structure, ">[^<]+<", "><");
        // Classes drive the design; keep them so two differently styled cards do
        // not collapse into one component file.
        var classes = string.Join("|", ClassAttrRegex.Matches(html)
            .Select(match => match.Groups[1].Value.Trim()));
        return structure + "|" + classes;
    }

    [GeneratedRegex(@"<([a-zA-Z][a-zA-Z0-9]*)(?:[^/>][^>]*)?>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"\bclass\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttrPattern();

    [GeneratedRegex(@"<(h[1-6])[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"<a[^>]*href=[""'][^""']*[""'][^>]*>.*?</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"<p[^>]*>.*?</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParagraphPattern();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ImgTagPattern();

    [GeneratedRegex(@"\bsrc\s*=\s*([""'])([^""']*)\1", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcPattern();

    [GeneratedRegex(@"\s(?:data-src|data-original)\s*=\s*([""'])([^""']*)\1", RegexOptions.IgnoreCase)]
    private static partial Regex ImgLazyPattern();

    [GeneratedRegex(@"\s(?:srcset|data-srcset)\s*=\s*([""'])[^""']*\1", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcsetPattern();
}
