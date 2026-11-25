using System.Buffers;
using System.Globalization;
using System.Text;
using AngleSharp.Dom;

namespace Bukit.Engine;

/// <summary>
/// Projects body HTML to Markdown for the <c>.md</c> content representation and <c>llms-full.txt</c>.
/// Headings, paragraphs, lists, block quotes and code blocks keep their structure; all text is
/// escaped so untrusted HTML cannot produce raw HTML, images or links other than safe anchors.
/// </summary>
internal static class MarkdownBodyProjection
{
    // Deeper markup is flattened to text so hostile nesting cannot exhaust the stack.
    private const int MaxDepth = 128;
    private const string LanguagePrefix = "language-";
    private const int MaxListNumber = 999_999_999;

    private static readonly HashSet<string> InlineNames = new(StringComparer.Ordinal)
    {
        "a", "b", "code", "del", "em", "i", "mark", "s", "small", "span", "strong", "sub", "sup", "u"
    };

    private static readonly HashSet<string> BlockNames = new(StringComparer.Ordinal)
    {
        "address", "article", "aside", "blockquote", "body", "caption", "center", "dd", "details", "dialog", "div",
        "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "head",
        "header", "hgroup", "hr", "li", "main", "nav", "ol", "p", "pre", "search", "section", "summary", "table",
        "tbody", "tfoot", "thead", "title", "tr", "ul"
    };

    private static readonly SearchValues<char> LanguageChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+-_.#");

    internal static string FromHtml(string html, string? sourceUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        using var document = HtmlTextProjection.Parse(html);
        var blocks = new List<Block>();
        new Renderer(sourceUrl).RenderBlocks(document.DocumentElement.ChildNodes, blocks, 0);
        var markdown = Join(blocks, compact: false);
        return Environment.NewLine == "\n" ? markdown : markdown.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    /// <param name="Text">Markdown lines separated by <c>\n</c>.</param>
    /// <param name="CanInterrupt">A list that may directly follow a paragraph line without a blank line.</param>
    private readonly record struct Block(string Text, bool CanInterrupt = false);

    private sealed class Renderer(string? sourceUrl)
    {
        internal void RenderBlocks(IEnumerable<INode> nodes, List<Block> blocks, int depth)
        {
            var inline = new StringBuilder();
            foreach (var node in nodes)
            {
                if (node is IElement element && depth < MaxDepth && IsHtml(element) && BlockNames.Contains(element.LocalName))
                {
                    AddParagraph(inline, blocks);
                    RenderBlock(element, blocks, depth + 1);
                }
                else
                {
                    AppendInline(node, inline, depth + 1, links: true);
                }
            }
            AddParagraph(inline, blocks);
        }

        private void RenderBlock(IElement element, List<Block> blocks, int depth)
        {
            switch (element.LocalName)
            {
                case ['h', >= '1' and <= '6']:
                    AddHeading(element, blocks, depth);
                    break;
                case "ul" or "ol":
                    AddList(element, blocks, depth);
                    break;
                case "blockquote":
                    var inner = new List<Block>();
                    RenderBlocks(element.ChildNodes, inner, depth);
                    if (inner.Count > 0)
                        blocks.Add(new Block(string.Join('\n', Join(inner, compact: false).Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line))));
                    break;
                case "pre":
                    AddCode(element, blocks);
                    break;
                case "hr":
                    // Never "---": that line separates documents in llms-full.txt.
                    blocks.Add(new Block("* * *"));
                    break;
                default:
                    RenderBlocks(element.ChildNodes, blocks, depth);
                    break;
            }
        }

        private void AddHeading(IElement element, List<Block> blocks, int depth)
        {
            var inline = new StringBuilder();
            foreach (var child in element.ChildNodes) AppendInline(child, inline, depth + 1, links: true);
            var text = inline.Replace('\n', ' ').ToString().Trim();
            if (text.Length == 0) return;
            // A trailing " #" run would otherwise be read as an optional closing sequence.
            if (text[^1] == '#') text = text[..^1] + "\\#";
            // Body headings start at ### so they stay below the projection's own "#" title and "##" sections.
            blocks.Add(new Block(new string('#', Math.Min(element.LocalName[1] - '0' + 2, 6)) + " " + text));
        }

        private void AddList(IElement list, List<Block> blocks, int depth)
        {
            var ordered = list.LocalName == "ol";
            // CommonMark ordered markers allow at most nine digits; clamping also keeps number++ from overflowing.
            var start = ordered && int.TryParse(list.GetAttribute("start"), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? Math.Min(value, MaxListNumber) : 1;
            var number = start;
            var items = new List<string>();
            foreach (var child in list.ChildNodes)
            {
                if (child is IComment || (child is IText text && string.IsNullOrWhiteSpace(text.Data))) continue;
                IEnumerable<INode> content = child is IElement { LocalName: "li" } item ? item.ChildNodes : [child];
                var inner = new List<Block>();
                RenderBlocks(content, inner, depth);
                if (inner.Count == 0) continue;
                var marker = ordered ? $"{Math.Min(number++, MaxListNumber)}. " : "- ";
                items.Add(Indent(Join(inner, compact: true), marker));
            }
            if (items.Count > 0) blocks.Add(new Block(string.Join('\n', items), !ordered || start == 1));
        }

        private void AppendInline(INode node, StringBuilder inline, int depth, bool links)
        {
            if (node is IText text) { AppendText(inline, text.Data); return; }
            if (node is IComment) { AppendSpace(inline); return; }
            if (node is not IElement element) return;
            if (depth >= MaxDepth)
            {
                AppendSpace(inline);
                AppendText(inline, element.TextContent);
                AppendSpace(inline);
                return;
            }
            var html = IsHtml(element);
            if (html && element.LocalName == "br") { NewLine(inline); return; }
            if (html && links && element.LocalName == "a" && TryAppendLink(element, inline, depth)) return;
            // Inline formatting must not split a word (Off<strong>icial</strong>); other elements separate words.
            var spaced = !html || !InlineNames.Contains(element.LocalName);
            if (spaced) AppendSpace(inline);
            foreach (var child in element.ChildNodes) AppendInline(child, inline, depth + 1, links);
            if (spaced) AppendSpace(inline);
        }

        private bool TryAppendLink(IElement anchor, StringBuilder inline, int depth)
        {
            // The sanitizer removes href from anchors that were never explicitly closed.
            var href = anchor.GetAttribute("href")?.Trim();
            if (!IsSafeTarget(href)) return false;
            var label = new StringBuilder();
            foreach (var child in anchor.ChildNodes) AppendInline(child, label, depth + 1, links: false);
            var text = label.Replace('\n', ' ').ToString().Trim();
            if (text.Length == 0) return false;

            AppendSpace(inline);
            inline.Append('[').Append(text).Append("](<");
            foreach (var c in ResolveTarget(href!, sourceUrl))
            {
                // Angle-delimited destinations retain parentheses and Unicode. Encode
                // delimiters/whitespace and entities without decoding a target twice.
                switch (c)
                {
                    case '&': inline.Append("&amp;"); break;
                    case '<': inline.Append("%3C"); break;
                    case '>': inline.Append("%3E"); break;
                    case '\\': inline.Append("%5C"); break;
                    default: inline.Append(char.IsWhiteSpace(c) ? Uri.EscapeDataString(c.ToString()) : c.ToString()); break;
                }
            }
            inline.Append(">)");
            AppendSpace(inline);
            return true;
        }
    }

    private static void AddCode(IElement pre, List<Block> blocks)
    {
        // Code stays verbatim; a fence longer than any backtick run in it cannot be closed early.
        var code = pre.TextContent.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
        if (string.IsNullOrWhiteSpace(code)) return;
        int longest = 0, run = 0;
        foreach (var c in code)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        var fence = new string('`', Math.Max(3, longest + 1));
        blocks.Add(new Block($"{fence}{ReadLanguage(pre)}\n{code}\n{fence}"));
    }

    private static string ReadLanguage(IElement pre)
    {
        if (pre.FirstElementChild is not { LocalName: "code" } code) return string.Empty;
        foreach (var name in code.ClassList)
        {
            if (name.Length > LanguagePrefix.Length && name.StartsWith(LanguagePrefix, StringComparison.Ordinal) &&
                !name.AsSpan(LanguagePrefix.Length).ContainsAnyExcept(LanguageChars))
                return name[LanguagePrefix.Length..];
        }
        return string.Empty;
    }

    private static void AddParagraph(StringBuilder inline, List<Block> blocks)
    {
        var lines = inline.ToString().Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Select(EscapeLineStart);
        inline.Clear();
        var text = string.Join("\\\n", lines);
        if (text.Length > 0) blocks.Add(new Block(text));
    }

    private static string EscapeLineStart(string line)
    {
        // Text must not start a heading, list, setext underline, thematic break or fence.
        if (line[0] is '#' or '+' or '-' or '=' or '~') return "\\" + line;
        var digits = line.AsSpan().IndexOfAnyExceptInRange('0', '9');
        return digits is > 0 and < 10 && line[digits] is '.' or ')' ? line.Insert(digits, "\\") : line;
    }

    private static void AppendText(StringBuilder inline, string text)
    {
        foreach (var c in text)
        {
            switch (c)
            {
                case ' ' or '\t' or '\n' or '\r' or '\f': AppendSpace(inline); break;
                case '&': inline.Append("&amp;"); break;
                case '<': inline.Append("&lt;"); break;
                case '>': inline.Append("&gt;"); break;
                case '\\' or '`' or '*' or '_' or '[' or ']' or '!' or '|': inline.Append('\\').Append(c); break;
                default: inline.Append(c); break;
            }
        }
    }

    private static void AppendSpace(StringBuilder inline)
    {
        if (inline.Length > 0 && inline[^1] is not (' ' or '\n')) inline.Append(' ');
    }

    private static void NewLine(StringBuilder inline)
    {
        while (inline.Length > 0 && inline[^1] == ' ') inline.Length--;
        inline.Append('\n');
    }

    private static string Join(List<Block> blocks, bool compact)
    {
        var result = new StringBuilder();
        for (var i = 0; i < blocks.Count; i++)
        {
            // Inside a list item a nested list follows its text directly, keeping the list tight.
            if (i > 0) result.Append(compact && blocks[i].CanInterrupt ? "\n" : "\n\n");
            result.Append(blocks[i].Text);
        }
        return result.ToString();
    }

    private static string Indent(string text, string marker)
    {
        var pad = new string(' ', marker.Length);
        var lines = text.Split('\n');
        var result = new StringBuilder(marker).Append(lines[0]);
        foreach (var line in lines.AsSpan(1)) result.Append('\n').Append(line.Length == 0 ? string.Empty : pad + line);
        return result.ToString();
    }

    private static bool IsHtml(IElement element) => element.NamespaceUri == NamespaceNames.HtmlUri;

    private static string ResolveTarget(string href, string? sourceUrl)
    {
        if (sourceUrl is null || href.StartsWith('/') || Uri.TryCreate(href, UriKind.Absolute, out _)) return href;
        // Projections live at different paths: resolve relative links and fragments
        // against the HTML page, never against content/*.md or llms-full.txt.
        var page = new Uri(new Uri("https://projection.invalid/"), sourceUrl);
        if (!Uri.TryCreate(page, href, out var target)) return href;
        return sourceUrl.StartsWith('/') ? target.PathAndQuery + target.Fragment : target.AbsoluteUri;
    }

    private static bool IsSafeTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Any(char.IsControl)) return false;
        var colon = target.IndexOf(':');
        var path = target.IndexOfAny(['/', '?', '#']);
        if (colon < 0 || (path >= 0 && path < colon)) return true;
        return target[..colon].ToLowerInvariant() is "https" or "http" or "mailto" or "tel" or "ftp";
    }
}
