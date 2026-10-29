using System.Net;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Parser.Tokens;
using AngleSharp.Text;

namespace Bukit.Engine;

internal static class HtmlTextProjection
{
    internal static string ToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        if (!html.Contains('<')) return WebUtility.HtmlDecode(html).ReplaceLineEndings(" ").Trim();
        using var document = Parse(html);
        var text = new StringBuilder(html.Length);
        var nodes = new Stack<(INode Node, bool End)>();
        nodes.Push((document, false));
        while (nodes.TryPop(out var item))
        {
            if (item.Node is IText value) { text.Append(value.Data); continue; }
            if (item.Node is IComment) { text.Append(' '); continue; }
            if (item.Node is IElement element && (element.SourceReference is not null ||
                element.NamespaceUri == NamespaceNames.HtmlUri && element.LocalName == "textarea"))
            {
                text.Append(' ');
                if (item.End) continue;
                if (!IsVoid(element)) nodes.Push((element, true));
            }
            for (var child = item.Node.LastChild; child is not null; child = child.PreviousSibling)
                nodes.Push((child, false));
        }
        return text.ToString().ReplaceLineEndings(" ").Trim();
    }

    internal static string Sanitize(string html)
    {
        if (!html.Contains('<')) return html;
        using var document = Parse(html);
        return document.DocumentElement.InnerHtml;
    }

    private static IDocument Parse(string html)
    {
        var scriptEnds = new Dictionary<int, int>();
        var scripts = new List<(int Start, int Body)>();
        var closedAnchors = new HashSet<int>();
        var templates = new Stack<(IHtmlTemplateElement Element, int Anchor)>();
        var anchor = -1;
        var parser = new HtmlParser(new HtmlParserOptions
        {
            IsScripting = true,
            IsKeepingSourceReferences = true,
            OnToken = (token, range) =>
            {
                // AngleSharp moves children into Content when it actually closes an HTML template.
                while (templates.TryPeek(out var template) && template.Element.Content.HasChildNodes)
                    anchor = templates.Pop().Anchor;
                if (token.Type == HtmlTokenType.EndTag && token.Name == "template" &&
                    templates.TryPeek(out var empty) && !empty.Element.HasChildNodes)
                    anchor = templates.Pop().Anchor;
                if (token.Type == HtmlTokenType.StartTag)
                {
                    if (token.Name == "script") scriptEnds[range.Start.Index] = range.End.Index;
                    if (token.Name == "a") anchor = range.Start.Index;
                }
                else if (token.Type == HtmlTokenType.EndTag && token.Name == "a" && anchor >= 0)
                {
                    closedAnchors.Add(anchor);
                    anchor = -1;
                }
            },
            OnCreated = (element, position) =>
            {
                if (element is IHtmlTemplateElement template)
                {
                    templates.Push((template, anchor));
                    anchor = -1;
                }
                if (element.LocalName == "script" && element.NamespaceUri == NamespaceNames.HtmlUri)
                    scripts.Add((position.Index, scriptEnds[position.Index]));
            }
        });
        var document = parser.ParseDocument(html);
        if (scripts.Count > 0)
        {
            // shortcut: AngleSharp 1.5.2 ends some escaped scripts too early; remove this guard only after browser-parity regressions pass upstream.
            var clean = new StringBuilder(html.Length);
            var copied = 0;
            foreach (var (start, body) in scripts)
            {
                if (start < copied) continue;
                clean.Append(html, copied, start - copied).Append(' ');
                copied = FindScriptEnd(html, body);
            }
            clean.Append(html, copied, html.Length - copied);
            document.Dispose();
            closedAnchors.Clear();
            templates.Clear();
            anchor = -1;
            document = parser.ParseDocument(clean.ToString());
        }

        ProjectElements(document, closedAnchors);
        return document;
    }

    private static void ProjectElements(IDocument document, HashSet<int> closedAnchors)
    {
        foreach (var element in document.All.ToArray())
        {
            if (element.LocalName is "script" or "style" or "template")
                element.Parent?.ReplaceChild(document.CreateTextNode(" "), element);
            else if (element.NamespaceUri == NamespaceNames.HtmlUri && element.LocalName is "xmp" or "iframe" or "noembed" or "noframes" or "plaintext" or "noscript")
            {
                var literal = document.CreateElement("textarea");
                literal.TextContent = element.TextContent;
                element.Parent?.ReplaceChild(literal, element);
            }
            else if (element.LocalName == "a" && (element.SourceReference is null || !closedAnchors.Contains(element.SourceReference.Position.Index)))
                element.RemoveAttribute("href");
        }
    }

    private enum ScriptState { Data, Escaped, Dash, DashDash, DoubleEscaped, DoubleDash, DoubleDashDash }

    private static int FindScriptEnd(string html, int index)
    {
        var state = ScriptState.Data;
        for (var i = index; i < html.Length; i++)
        {
            var c = html[i];
            if (state == ScriptState.Data && html.AsSpan(i).StartsWith("<!--", StringComparison.Ordinal))
            {
                state = ScriptState.DashDash;
                i += 3;
                continue;
            }
            var doubled = state is ScriptState.DoubleEscaped or ScriptState.DoubleDash or ScriptState.DoubleDashDash;
            if (c == '<')
            {
                var closing = i + 1 < html.Length && html[i + 1] == '/';
                var name = i + (closing ? 2 : 1);
                if (MatchesScript(html, name))
                {
                    if (closing && !doubled) return ClosingTagEnd(html, i);
                    if (closing && doubled) state = ScriptState.Escaped;
                    else if (state != ScriptState.Data && !closing) state = ScriptState.DoubleEscaped;
                    i = name + "script".Length;
                    continue;
                }
                if (state != ScriptState.Data) state = doubled ? ScriptState.DoubleEscaped : ScriptState.Escaped;
                continue;
            }
            state = state switch
            {
                ScriptState.Escaped when c == '-' => ScriptState.Dash,
                ScriptState.Dash when c == '-' => ScriptState.DashDash,
                ScriptState.Dash => ScriptState.Escaped,
                ScriptState.DashDash when c == '>' => ScriptState.Data,
                ScriptState.DashDash when c != '-' => ScriptState.Escaped,
                ScriptState.DoubleEscaped when c == '-' => ScriptState.DoubleDash,
                ScriptState.DoubleDash when c == '-' => ScriptState.DoubleDashDash,
                ScriptState.DoubleDash => ScriptState.DoubleEscaped,
                ScriptState.DoubleDashDash when c == '>' => ScriptState.Data,
                ScriptState.DoubleDashDash when c != '-' => ScriptState.DoubleEscaped,
                _ => state
            };
        }
        return html.Length;
    }

    private static bool MatchesScript(string html, int start)
    {
        var after = start + "script".Length;
        return after < html.Length && html.AsSpan(start, "script".Length).Equals("script", StringComparison.OrdinalIgnoreCase) &&
            html[after] is '>' or '/' or ' ' or '\t' or '\n' or '\r' or '\f';
    }

    private static int ClosingTagEnd(string html, int start)
    {
        using var memory = new ReadOnlyMemoryTextSource(html.AsMemory(start));
        using var source = new TextSource(memory);
        using var tokenizer = new HtmlTokenizer(source, HtmlEntityProvider.Resolver);
        var end = -1;
        tokenizer.OnToken = (_, range) => end = range.End.Index;
        var token = tokenizer.Get();
        return token.Type == HtmlTokenType.EndTag ? start + end : html.Length;
    }

    private static bool IsVoid(IElement element)
        => element.NamespaceUri == NamespaceNames.HtmlUri && element.LocalName is
            "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";
}
