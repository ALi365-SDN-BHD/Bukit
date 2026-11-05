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

    private static readonly HashSet<string> ListComponentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "article-card", "company-card", "service-card", "card"
    };

    private static readonly Regex HtmlTagRegex = HtmlTagPattern();
    private static readonly Regex HeadingRegex = HeadingPattern();
    private static readonly Regex LinkRegex = LinkPattern();
    private static readonly Regex ParagraphRegex = ParagraphPattern();

    internal static List<DiscoveredComponent> Extract(List<DiscoveredPage> pages)
    {
        var components = new List<DiscoveredComponent>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            foreach (var (cssClass, componentName) in ClassToComponent)
            {
                var className = cssClass.StartsWith('.') ? cssClass[1..] : cssClass;
                if (TemplateBodyTransformer.ContainsClass(page.FullHtml, className))
                {
                    var fragment = ExtractComponentHtml(page, cssClass);
                    if (!string.IsNullOrWhiteSpace(fragment))
                    {
                        var dedupKey = componentName + "|" + NormalizeForDedup(fragment);
                        if (!seen.Add(dedupKey))
                        {
                            var existingForDedup = components.FirstOrDefault(c =>
                                c.Name == componentName &&
                                NormalizeForDedup(c.HtmlFragment) == NormalizeForDedup(fragment));
                            if (existingForDedup != null &&
                                !existingForDedup.UsedBy.Any(u => u.FilePath == page.FilePath))
                            {
                                existingForDedup.UsedBy.Add(page);
                            }
                            continue;
                        }
                    }

                    var existing = components.FirstOrDefault(c =>
                        c.Name == componentName && c.UsedBy.Any(u => u.FilePath == page.FilePath));

                    if (existing != null)
                    {
                        if (!existing.UsedBy.Any(u => u.FilePath == page.FilePath))
                            existing.UsedBy.Add(page);
                    }
                    else
                    {
                        components.Add(new DiscoveredComponent
                        {
                            Name = componentName,
                            HtmlFragment = fragment,
                            UsedBy = [page],
                            NormalizedTemplate = GenerateTemplate(fragment, componentName)
                        });
                    }
                }
            }
        }

        return components;
    }

    private static string ExtractComponentHtml(DiscoveredPage page, string cssClass)
    {
        var tagEnd = cssClass.StartsWith('.') ? cssClass[1..] : cssClass;
        var pattern = $@"<([a-zA-Z][a-zA-Z0-9]*)[^>]*?\bclass\s*=\s*""(?:[^""]*\s)?{Regex.Escape(tagEnd)}(?:\s[^""]*)?""[^>]*>";
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

    private static string GenerateTemplate(string fragment, string componentName)
    {
        fragment = ScribanText.Escape(fragment);
        if (componentName.Equals("pagination", StringComparison.OrdinalIgnoreCase))
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
        if (!ListComponentNames.Contains(componentName))
            return fragment;

        var content = ParagraphRegex.Replace(fragment, _ => "<p>{{ item.summary }}</p>");
        content = LinkRegex.Replace(content, _ =>
            "<a href=\"{{ base_url }}{{ item.url }}\">{{ item.title }}</a>");
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

    private static string NormalizeForDedup(string html)
    {
        var result = HtmlTagRegex.Replace(html, "<$1>");
        result = Regex.Replace(result, ">[^<]+<", "><");
        return result;
    }

    [GeneratedRegex(@"<([a-zA-Z][a-zA-Z0-9]*)(?:[^/>][^>]*)?>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"<(h[1-6])[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"<a[^>]*href=[""'][^""']*[""'][^>]*>.*?</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"<p[^>]*>.*?</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParagraphPattern();
}
