using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class TemplateBodyTransformer
{
    private static readonly string[] SectionComponentClasses =
    [
        "hero", "faq", "cta", "stats", "testimonial", "pagination"
    ];

    private static readonly string[] ListComponentClasses =
    [
        "article-card", "company-card", "service-card", "card"
    ];

    internal static string Transform(DiscoveredPage page, Dictionary<string, string> pathMappings)
    {
        var body = AssetImporter.RewritePaths(page.UniqueBody, pathMappings).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return ThemeGenerator.GetDefaultTemplateBody(page.Type);
        }

        return page.Type switch
        {
            PageType.PostList or PageType.CompanyList or PageType.ServiceList => TransformList(body),
            PageType.Home => TransformHome(body),
            _ => TransformDetail(body)
        };
    }

    private static string TransformHome(string body)
    {
        var result = ReplaceFirstHeading(body, "page.title");
        result = ReplaceSectionComponents(result);
        result = ReplaceListCards(result);
        if (!result.Contains("{{ page.content }}", StringComparison.Ordinal) &&
            !HasRecognizedSectionComponent(body) &&
            !HasRecognizedListComponent(body))
        {
            result = ReplaceMainContentAfterHeading(result, "page.content");
        }

        return result;
    }

    private static string TransformDetail(string body)
    {
        var result = ReplaceFirstHeading(body, "page.title");
        result = ReplaceSectionComponents(result);
        if (!result.Contains("{{ page.content }}", StringComparison.Ordinal))
        {
            result = ReplaceMainContentAfterHeading(result, "page.content");
        }

        return result;
    }

    private static string TransformList(string body)
    {
        var result = ReplaceFirstHeading(body, "page.title");
        result = ReplaceListCards(result);
        if (!result.Contains("{{ for item in pages }}", StringComparison.Ordinal) &&
            !result.Contains("{{ for p in pages }}", StringComparison.Ordinal))
        {
            result += Environment.NewLine + """
{{ for p in pages }}
<article>
  <h2><a href="{{ base_url }}{{ p.url }}">{{ p.title }}</a></h2>
  {{ if p.summary }}<p>{{ p.summary }}</p>{{ end }}
</article>
{{ end }}
""";
        }

        return result;
    }

    private static string ReplaceSectionComponents(string html)
    {
        var result = html;
        foreach (var className in SectionComponentClasses)
        {
            if (!ContainsClass(result, className)) continue;

            // The component file holds the whole element, so the include replaces it;
            // keeping the original tag around it rendered the element twice.
            var componentName = className == "faq-item" ? "faq" : className;
            result = ReplaceElementsByClass(result, className, _ =>
                "{{ include 'components/" + componentName + ".html' }}");
        }

        if (ContainsClass(result, "faq-item") && !result.Contains("components/faq.html", StringComparison.Ordinal))
        {
            result = ReplaceElementsByClass(result, "faq-item", _ => "{{ include 'components/faq.html' }}");
        }

        return result;
    }

    private static string ReplaceListCards(string html)
    {
        var result = html;
        foreach (var className in ListComponentClasses)
        {
            if (!ContainsClass(result, className)) continue;

            var componentName = className == "card" ? "card" : className;
            result = ReplaceCardGroupWithLoop(result, className, componentName);
            break;
        }

        return result;
    }

    private static string ReplaceCardGroupWithLoop(string html, string className, string componentName)
    {
        var matches = Regex.Matches(
            html,
            ElementWithClassPattern(className),
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matches.Count == 0) return html;

        var first = matches[0];
        var last = matches[^1];
        // The card component is the whole card element; no extra wrapper per item.
        var loop = "{{ for item in pages }}" + Environment.NewLine +
                   "  {{ include 'components/" + componentName + ".html' }}" + Environment.NewLine +
                   "{{ end }}";

        var start = first.Index;
        var end = last.Index + last.Length;
        return html[..start] + loop + html[end..];
    }

    private static bool HasRecognizedSectionComponent(string html)
        => SectionComponentClasses.Any(className => ContainsClass(html, className)) ||
           ContainsClass(html, "faq-item");

    private static bool HasRecognizedListComponent(string html)
        => ListComponentClasses.Any(className => ContainsClass(html, className));

    private static string ReplaceFirstHeading(string html, string variable)
    {
        return H1Regex().Replace(
            html,
            match => $"{match.Groups["open"].Value}{{{{ {variable} }}}}{match.Groups["close"].Value}",
            count: 1);
    }

    private static string ReplaceMainContentAfterHeading(string html, string variable)
    {
        var h1 = H1Regex().Match(html);
        if (!h1.Success)
        {
            return html;
        }

        var mainClose = html.LastIndexOf("</main>", StringComparison.OrdinalIgnoreCase);
        if (mainClose <= h1.Index)
        {
            return html;
        }

        var contentStart = h1.Index + h1.Length;
        // Wrappers opened before the heading (e.g. <article>) close inside the replaced
        // part; keep those closing tags so the template stays well formed.
        var closers = UnmatchedClosingTags(html[contentStart..mainClose]);
        return html[..contentStart] + $"{Environment.NewLine}  {{{{ {variable} }}}}{Environment.NewLine}" + closers + html[mainClose..];
    }

    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"
    };

    private static string UnmatchedClosingTags(string fragment)
    {
        var open = new Stack<string>();
        var closers = new System.Text.StringBuilder();
        foreach (Match tag in TagRegex().Matches(fragment))
        {
            var name = tag.Groups["name"].Value;
            if (VoidElements.Contains(name) || tag.Groups["self"].Success)
                continue;

            if (!tag.Groups["close"].Success)
            {
                open.Push(name);
            }
            else if (open.Count > 0 && open.Peek().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                open.Pop();
            }
            else
            {
                closers.Append(tag.Value);
            }
        }

        return closers.ToString();
    }

    [GeneratedRegex(@"<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9-]*)\b[^>]*?(?<self>/)?>")]
    private static partial Regex TagRegex();

    private static string ReplaceElementsByClass(string html, string className, Func<Match, string> replacement)
    {
        var pattern = ElementWithClassPattern(className);
        return Regex.Replace(html, pattern, m => replacement(m), RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    internal static bool ContainsClass(string html, string className)
    {
        var pattern = $@"class\s*=\s*[""'](?:[^""']*\s)?{Regex.Escape(className)}(?:\s[^""']*)?[""']";
        return Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase);
    }

    private static string ElementWithClassPattern(string className)
    {
        var token = Regex.Escape(className);
        return $@"<(?<tag>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*\bclass\s*=\s*[""'](?:[^""']*\s)?{token}(?:\s[^""']*)?[""'][^>]*)>.*?</\k<tag>>";
    }

    [GeneratedRegex(@"(?<open><h1[^>]*>).*?(?<close></h1>)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1Regex();
}
