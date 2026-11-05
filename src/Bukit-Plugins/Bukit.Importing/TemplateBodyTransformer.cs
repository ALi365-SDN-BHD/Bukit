using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace Bukit.Importing;

internal static partial class TemplateBodyTransformer
{
    // Only data-bound components are included. Section copy stays in the page.
    private static readonly string[] SectionComponentClasses =
    [
        "pagination"
    ];

    private static readonly Regex RawFormRegex = RawFormPattern();
    private static readonly Regex RawIframeRegex = RawIframePattern();
    private static readonly Regex RawVideoRegex = RawVideoPattern();
    private static readonly Regex RawAudioRegex = RawAudioPattern();
    private static readonly Regex RawEmbedRegex = RawEmbedPattern();

    private static readonly (string ClassName, string ComponentName)[] ListComponents =
    [
        ("article-card", "article-card"),
        ("post-card", "article-card"),
        ("company-card", "company-card"),
        ("service-card", "service-card"),
        ("card", "card")
    ];

    internal static string Transform(
        DiscoveredPage page,
        Dictionary<string, string> pathMappings,
        IReadOnlyList<DiscoveredComponent>? components = null)
    {
        // Escape demo text before this class adds template tags of its own.
        var body = ScribanText.Escape(AssetImporter.RewritePaths(page.UniqueBody, pathMappings).Trim());
        body = PageAssetCollector.RewriteCssUrls(body, pathMappings);
        body = PageAssetCollector.RewriteSrcsets(body, pathMappings, "{{ base_url }}");
        if (string.IsNullOrWhiteSpace(body))
        {
            return ThemeGenerator.GetDefaultTemplateBody(page.Type);
        }

        return page.Type switch
        {
            PageType.PostList or PageType.CompanyList or PageType.ServiceList =>
                TransformList(body, page, components),
            PageType.Home => TransformHome(body, components),
            _ => TransformDetail(body, components)
        };
    }

    // Core renders the home route from pages/index.html with no content record of
    // its own, so the home template keeps the demo's heading and copy as markup.
    // Home cards are demo copy too; there is no collection list to bind them to.
    private static string TransformHome(string body, IReadOnlyList<DiscoveredComponent>? components)
        => ReplaceSectionComponents(body, components);

    private static string TransformDetail(string body, IReadOnlyList<DiscoveredComponent>? components)
    {
        var result = ReplaceFirstHeading(body, "page.title");
        result = ReplaceSectionComponents(result, components);
        if (!result.Contains("{{ page.content }}", StringComparison.Ordinal))
        {
            result = ReplaceMainContentAfterHeading(result, "page.content");
        }

        return result;
    }

    private static string TransformList(
        string body,
        DiscoveredPage page,
        IReadOnlyList<DiscoveredComponent>? components)
    {
        var result = ReplaceFirstHeading(body, "page.title");
        var cards = ReplaceListCards(result, page, components);
        result = cards.Html;
        if (!cards.FoundCards &&
            !result.Contains("{{ for item in pages }}", StringComparison.Ordinal) &&
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

    private static string ReplaceSectionComponents(
        string html,
        IReadOnlyList<DiscoveredComponent>? components)
    {
        var result = html;
        foreach (var className in SectionComponentClasses)
        {
            if (!ContainsClass(result, className)) continue;
            if (components is { Count: 0 }) continue;
            if (components is not null &&
                !components.Any(c =>
                    string.Equals(c.SourceClass, className, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(c.NormalizedTemplate)))
                continue;

            // The component file holds the whole element, so the include replaces it;
            // keeping the original tag around it rendered the element twice.
            result = ReplaceElementsByClass(result, className, _ =>
                "{{ include 'components/" + className + ".html' }}");
        }

        return result;
    }

    private static (string Html, bool FoundCards) ReplaceListCards(
        string html,
        DiscoveredPage page,
        IReadOnlyList<DiscoveredComponent>? components)
    {
        foreach (var (className, defaultName) in ListComponents)
        {
            if (!ContainsClass(html, className))
                continue;

            if (components is { Count: 0 })
                return (html, true);

            var componentName = components?
                .FirstOrDefault(c =>
                    string.Equals(c.SourceClass, className, StringComparison.OrdinalIgnoreCase) &&
                    c.UsedBy.Any(u => u.FilePath == page.FilePath) &&
                    !string.IsNullOrWhiteSpace(c.NormalizedTemplate))?
                .Name ?? defaultName;

            // A list page normally has one card group. Replace only the first
            // contiguous group so a second group elsewhere is not swallowed.
            return (ReplaceCardGroupWithLoop(html, className, componentName), true);
        }

        return (html, false);
    }

    private static string ReplaceCardGroupWithLoop(string html, string className, string componentName)
    {
        var matches = Regex.Matches(
            html,
            ElementWithClassPattern(className),
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matches.Count == 0) return html;

        if (TryReplaceWrappedCards(html, className, componentName, matches.Count) is { } wrapped)
            return wrapped;

        var first = matches[0];
        var last = first;
        for (var i = 1; i < matches.Count; i++)
        {
            var gapStart = last.Index + last.Length;
            var gap = html[gapStart..matches[i].Index];
            if (!string.IsNullOrWhiteSpace(RemoveComments(gap)))
                break;

            last = matches[i];
        }

        // The card component is the whole card element; no extra wrapper per item.
        var loop = "{{ for item in pages }}" + Environment.NewLine +
                   "  {{ include 'components/" + componentName + ".html' }}" + Environment.NewLine +
                   "{{ end }}";

        var start = first.Index;
        var end = last.Index + last.Length;
        return html[..start] + loop + html[end..];
    }

    // A grid often wraps each card in its own column. Replace the range from the
    // first column to the last with one loop that keeps the column wrapper.
    private static string? TryReplaceWrappedCards(
        string html,
        string className,
        string componentName,
        int expectedCards)
    {
        if (expectedCards < 2)
            return null;

        var document = new HtmlParser().ParseDocument($"<body>{html}</body>");
        var cards = document.QuerySelectorAll($".{className}").ToArray();
        if (cards.Length != expectedCards)
            return null;

        var parents = cards.Select(card => card.ParentElement).ToArray();
        if (parents.Any(parent => parent is null) ||
            parents.Distinct().Count() != parents.Length ||
            parents.Any(parent => parent!.QuerySelectorAll($".{className}").Length != 1))
        {
            return null;
        }

        var first = parents[0]!;
        var last = parents[^1]!;
        var card = cards[0].OuterHtml;
        if (!first.OuterHtml.Contains(card, StringComparison.Ordinal))
            return null;

        var shell = first.OuterHtml.Replace(card, "", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(Regex.Replace(shell, "<[^>]+>", "")))
            return null;

        var firstIndex = html.IndexOf(first.OuterHtml, StringComparison.Ordinal);
        var lastIndex = html.LastIndexOf(last.OuterHtml, StringComparison.Ordinal);
        if (firstIndex < 0 || lastIndex < firstIndex)
            return null;

        // Do not span an unrelated section between two wrapped card groups.
        var previousEnd = firstIndex + first.OuterHtml.Length;
        for (var i = 1; i < parents.Length; i++)
        {
            var parentHtml = parents[i]!.OuterHtml;
            var parentIndex = html.IndexOf(parentHtml, previousEnd, StringComparison.Ordinal);
            if (parentIndex < 0)
                return null;

            var gap = html[previousEnd..parentIndex];
            if (!string.IsNullOrWhiteSpace(RemoveComments(gap)))
                return null;

            previousEnd = parentIndex + parentHtml.Length;
        }

        var item = first.OuterHtml.Replace(
            card,
            "{{ include 'components/" + componentName + ".html' }}",
            StringComparison.Ordinal);
        var loop = "{{ for item in pages }}" + Environment.NewLine + item + Environment.NewLine + "{{ end }}";
        return html[..firstIndex] + loop + html[(lastIndex + last.OuterHtml.Length)..];
    }

    private static string RemoveComments(string html)
        => CommentRegex().Replace(html, "");

    private static string ReplaceFirstHeading(string html, string variable)
    {
        return H1Regex().Replace(
            html,
            match => $"{match.Groups["open"].Value}{{{{ {variable} }}}}{match.Groups["close"].Value}",
            count: 1);
    }

    // Forms and embedded media cannot become Markdown in this pipeline; keep their
    // original HTML in the page template so the contact form and map still render.
    private static string PreserveRawBlocks(string html)
    {
        var ranges = new List<(int Start, int End, string Html)>();
        foreach (var regex in new[] { RawFormRegex, RawIframeRegex, RawVideoRegex, RawAudioRegex, RawEmbedRegex })
        {
            foreach (Match match in regex.Matches(html))
                ranges.Add((match.Index, match.Index + match.Length, match.Value));
        }

        ranges.Sort((left, right) => left.Start.CompareTo(right.Start));
        var blocks = new List<string>();
        var coveredUntil = -1;
        foreach (var range in ranges)
        {
            if (range.Start < coveredUntil)
                continue;

            blocks.Add(range.Html);
            coveredUntil = range.End;
        }

        return blocks.Count == 0
            ? ""
            : Environment.NewLine + string.Join(Environment.NewLine, blocks) + Environment.NewLine;
    }

    private static string ReplaceMainContentAfterHeading(string html, string variable)
    {
        var h1 = H1Regex().Match(html);
        if (!h1.Success)
        {
            // A page may use an <h2> title (or no heading at all). Keep a single
            // outer container; with several sibling blocks, replace the whole body
            // so the second block is not swallowed.
            var document = new HtmlParser().ParseDocument($"<body>{html}</body>");
            var children = document.Body?.Children;
            if (children is { Length: 1 })
            {
                var outer = children[0];
                var outerHtml = outer.OuterHtml;
                var index = html.IndexOf(outerHtml, StringComparison.Ordinal);
                var openEnd = outerHtml.IndexOf('>');
                var close = outerHtml.LastIndexOf($"</{outer.LocalName}>", StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && openEnd >= 0 && close >= 0)
                    return html[..(index + openEnd + 1)] +
                           $"{Environment.NewLine}  {{{{ {variable} }}}}{Environment.NewLine}" +
                           PreserveRawBlocks(html[(index + openEnd + 1)..(index + close)]) +
                           html[(index + close)..];
            }

            return $"{Environment.NewLine}{{{{ {variable} }}}}{Environment.NewLine}" +
                   PreserveRawBlocks(html);
        }

        var contentEnd = html.LastIndexOf("</main>", StringComparison.OrdinalIgnoreCase);
        if (contentEnd <= h1.Index)
        {
            // The body may be a single <article> or a <div class="content"> with no
            // <main>. Use the end of the unique body so the page still binds content.
            contentEnd = html.Length;
        }

        var contentStart = h1.Index + h1.Length;
        // Wrappers opened before the heading (e.g. <article>) close inside the replaced
        // part; keep those closing tags so the template stays well formed.
        var removed = html[contentStart..contentEnd];
        var closers = UnmatchedClosingTags(removed);
        var raw = PreserveRawBlocks(removed);
        return html[..contentStart] + $"{Environment.NewLine}  {{{{ {variable} }}}}{Environment.NewLine}" + raw + closers + html[contentEnd..];
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


    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"<form\b[^>]*>.*?</form\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RawFormPattern();

    [GeneratedRegex(@"<iframe\b[^>]*>.*?</iframe\s*>|<iframe\b[^>]*/>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RawIframePattern();

    [GeneratedRegex(@"<video\b[^>]*>.*?</video\s*>|<video\b[^>]*/>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RawVideoPattern();

    [GeneratedRegex(@"<audio\b[^>]*>.*?</audio\s*>|<audio\b[^>]*/>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RawAudioPattern();

    [GeneratedRegex(@"<embed\b[^>]*/?>|<object\b[^>]*>.*?</object\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RawEmbedPattern();
}
