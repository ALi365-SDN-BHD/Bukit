using System.Text;

namespace Bukit.Engine.PublishAuditRules;

internal sealed record SemanticLandmarkHeadingInspection(
    bool HasMain,
    bool HasArticle,
    bool HasHeader,
    bool HasNav,
    bool HasFooter,
    IReadOnlyList<PublishSemanticOutlineItem> PrimaryHeadings);

internal static class SemanticLandmarkHeadingInspector
{
    private static readonly HashSet<string> ExcludedLandmarks = new(StringComparer.OrdinalIgnoreCase)
    {
        "header", "nav", "footer"
    };

    private static readonly HashSet<string> RawTextElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "template", "textarea", "title", "xmp", "iframe", "noembed", "noframes", "plaintext"
    };

    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    };

    internal static SemanticLandmarkHeadingInspection Inspect(string html)
    {
        var stack = new List<ElementFrame>();
        var headings = new List<CapturedHeading>();
        HeadingCapture? activeHeading = null;
        var nextMainId = 0;
        var nextArticleId = 0;
        int? firstMainId = null;
        var articleIdsInFirstMain = new List<int>();
        int? firstStandaloneArticleId = null;
        var hasMain = false;
        var hasArticle = false;
        var hasHeader = false;
        var hasNav = false;
        var hasFooter = false;

        var position = 0;
        while (position < html.Length)
        {
            var tagStart = html.IndexOf('<', position);
            if (tagStart < 0)
            {
                AppendHeadingText(activeHeading, html.AsSpan(position));
                break;
            }

            if (tagStart > position)
            {
                AppendHeadingText(activeHeading, html.AsSpan(position, tagStart - position));
            }

            if (html.AsSpan(tagStart).StartsWith("<!--", StringComparison.Ordinal))
            {
                var commentEnd = html.IndexOf("-->", tagStart + 4, StringComparison.Ordinal);
                position = commentEnd < 0 ? html.Length : commentEnd + 3;
                continue;
            }

            var tagEnd = FindTagEnd(html, tagStart + 1);
            if (tagEnd < 0)
            {
                break;
            }

            var token = html.AsSpan(tagStart + 1, tagEnd - tagStart - 1).Trim();
            if (token.IsEmpty || token[0] is '!' or '?')
            {
                position = tagEnd + 1;
                continue;
            }

            var closing = token[0] == '/';
            var name = ReadTagName(closing ? token[1..] : token);
            if (string.IsNullOrWhiteSpace(name))
            {
                position = tagEnd + 1;
                continue;
            }

            if (closing)
            {
                if (activeHeading is not null && string.Equals(activeHeading.Tag, name, StringComparison.OrdinalIgnoreCase))
                {
                    headings.Add(activeHeading.Complete());
                    activeHeading = null;
                }
                PopThrough(stack, name);
                position = tagEnd + 1;
                continue;
            }

            var insideExcludedLandmark = stack.Any(frame => frame.Excluded);
            hasMain |= !insideExcludedLandmark && name.Equals("main", StringComparison.OrdinalIgnoreCase);
            hasArticle |= !insideExcludedLandmark && name.Equals("article", StringComparison.OrdinalIgnoreCase);
            hasHeader |= name.Equals("header", StringComparison.OrdinalIgnoreCase);
            hasNav |= name.Equals("nav", StringComparison.OrdinalIgnoreCase);
            hasFooter |= name.Equals("footer", StringComparison.OrdinalIgnoreCase);

            var mainAncestors = stack.Where(frame => frame.MainId is not null).Select(frame => frame.MainId!.Value).ToList();
            var articleAncestors = stack.Where(frame => frame.ArticleId is not null).Select(frame => frame.ArticleId!.Value).ToList();
            int? mainId = null;
            int? articleId = null;
            if (!insideExcludedLandmark && name.Equals("main", StringComparison.OrdinalIgnoreCase))
            {
                mainId = ++nextMainId;
                mainAncestors.Add(mainId.Value);
                firstMainId ??= mainId;
            }
            else if (!insideExcludedLandmark && name.Equals("article", StringComparison.OrdinalIgnoreCase))
            {
                articleId = ++nextArticleId;
                articleAncestors.Add(articleId.Value);
                var containingMain = mainAncestors.LastOrDefault();
                if (containingMain != 0 && containingMain == firstMainId)
                {
                    articleIdsInFirstMain.Add(articleId.Value);
                }
                else if (containingMain == 0)
                {
                    firstStandaloneArticleId ??= articleId;
                }
            }

            var excluded = insideExcludedLandmark || ExcludedLandmarks.Contains(name);
            if (!excluded && name.Length == 2 && name[0] is 'h' or 'H' && name[1] is >= '1' and <= '6')
            {
                activeHeading = new HeadingCapture(
                    name,
                    name[1] - '0',
                    mainAncestors.ToArray(),
                    articleAncestors.ToArray());
            }

            if (RawTextElements.Contains(name))
            {
                position = SkipRawTextElement(html, name, tagEnd + 1);
                continue;
            }

            var selfClosing = token[^1] == '/' || VoidElements.Contains(name);
            if (!selfClosing)
            {
                stack.Add(new ElementFrame(name, mainId, articleId, excluded));
            }
            position = tagEnd + 1;
        }

        if (activeHeading is not null)
        {
            headings.Add(activeHeading.Complete());
        }

        IEnumerable<CapturedHeading> primary = Array.Empty<CapturedHeading>();
        var articleInMain = articleIdsInFirstMain.FirstOrDefault(articleId =>
            headings.Any(heading =>
                heading.Level == 1 &&
                heading.ArticleAncestors.Contains(articleId) &&
                !string.IsNullOrWhiteSpace(SemanticHtmlAuditRules.NormalizeText(heading.Text))));
        if (articleInMain != 0)
        {
            primary = headings.Where(heading => heading.ArticleAncestors.Contains(articleInMain));
        }
        else if (firstMainId is { } main)
        {
            primary = headings.Where(heading => heading.MainAncestors.Contains(main));
        }
        else if (firstStandaloneArticleId is { } standaloneArticle)
        {
            primary = headings.Where(heading => heading.ArticleAncestors.Contains(standaloneArticle));
        }

        var outline = primary
            .Select(heading => new PublishSemanticOutlineItem(heading.Level, SemanticHtmlAuditRules.NormalizeText(heading.Text)))
            .Where(heading => !string.IsNullOrWhiteSpace(heading.Text))
            .ToArray();
        return new SemanticLandmarkHeadingInspection(hasMain, hasArticle, hasHeader, hasNav, hasFooter, outline);
    }

    private static void AppendHeadingText(HeadingCapture? heading, ReadOnlySpan<char> text)
    {
        if (heading is not null)
        {
            heading.Text.Append(text);
        }
    }

    private static int FindTagEnd(string html, int start)
    {
        char quote = '\0';
        for (var i = start; i < html.Length; i++)
        {
            var current = html[i];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current == '>')
            {
                return i;
            }
        }
        return -1;
    }

    private static string ReadTagName(ReadOnlySpan<char> token)
    {
        token = token.TrimStart();
        var length = 0;
        while (length < token.Length && (char.IsLetterOrDigit(token[length]) || token[length] is '-' or ':'))
        {
            length++;
        }
        return length == 0 ? string.Empty : token[..length].ToString();
    }

    private static int SkipRawTextElement(string html, string name, int contentStart)
    {
        var closeToken = $"</{name}";
        var searchFrom = contentStart;
        while (searchFrom < html.Length)
        {
            var closeStart = html.IndexOf(closeToken, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (closeStart < 0)
            {
                return html.Length;
            }

            var boundary = closeStart + closeToken.Length;
            if (boundary >= html.Length ||
                char.IsWhiteSpace(html[boundary]) ||
                html[boundary] is '>' or '/')
            {
                var closeEnd = FindTagEnd(html, boundary);
                return closeEnd < 0 ? html.Length : closeEnd + 1;
            }

            searchFrom = boundary;
        }

        return html.Length;
    }

    private static void PopThrough(List<ElementFrame> stack, string name)
    {
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var match = string.Equals(stack[i].Tag, name, StringComparison.OrdinalIgnoreCase);
            stack.RemoveAt(i);
            if (match)
            {
                return;
            }
        }
    }

    private sealed record ElementFrame(string Tag, int? MainId, int? ArticleId, bool Excluded);

    private sealed record CapturedHeading(
        int Level,
        string Text,
        IReadOnlyList<int> MainAncestors,
        IReadOnlyList<int> ArticleAncestors);

    private sealed class HeadingCapture(
        string tag,
        int level,
        IReadOnlyList<int> mainAncestors,
        IReadOnlyList<int> articleAncestors)
    {
        internal string Tag { get; } = tag;
        internal StringBuilder Text { get; } = new();

        internal CapturedHeading Complete()
            => new(level, Text.ToString(), mainAncestors, articleAncestors);
    }
}
