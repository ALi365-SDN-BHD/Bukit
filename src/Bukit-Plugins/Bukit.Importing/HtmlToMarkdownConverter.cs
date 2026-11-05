using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Bukit.Importing;

/// <summary>
/// Converts extracted demo HTML into Markdown. Core renders Markdown with raw HTML
/// disabled, so writing HTML bodies straight into .md files showed tags as text.
/// </summary>
internal static partial class HtmlToMarkdownConverter
{
    private static readonly HtmlParser Parser = new(new HtmlParserOptions { IsScripting = false });

    internal static string Convert(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var document = Parser.ParseDocument(html);
        var sb = new StringBuilder();
        var body = document.Body;
        if (body is not null)
        {
            foreach (var node in body.ChildNodes)
                WriteBlock(sb, node);
        }

        var text = sb.ToString()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        return ExcessNewlines().Replace(text, "\n\n");
    }

    private static void WriteBlock(StringBuilder sb, INode node)
    {
        if (node is IText textNode)
        {
            var text = Collapse(textNode.Data);
            if (text.Length > 0)
                sb.Append(text);
            return;
        }

        if (node is not IElement element)
            return;

        switch (element.LocalName)
        {
            case "script" or "style" or "noscript" or "template":
                return;

            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                var level = element.LocalName[1] - '0';
                sb.Append("\n\n").Append('#', level).Append(' ').Append(Inline(element)).Append('\n');
                return;

            case "p":
                var paragraph = EscapeBlockStart(Inline(element));
                if (paragraph.Length > 0)
                    sb.Append("\n\n").Append(paragraph).Append('\n');
                return;

            case "br":
                sb.Append('\n');
                return;

            case "hr":
                sb.Append("\n\n---\n");
                return;

            case "pre":
                var language = element.QuerySelector("code")?.ClassName?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(name => name.StartsWith("language-", StringComparison.OrdinalIgnoreCase))?["language-".Length..] ?? "";
                sb.Append("\n\n```").Append(language).Append('\n')
                    .Append(element.TextContent.TrimEnd()).Append("\n```\n");
                return;

            case "blockquote":
                var quoted = Inline(element);
                if (quoted.Length > 0)
                {
                    sb.Append("\n\n");
                    foreach (var line in quoted.Split('\n'))
                        sb.Append("> ").Append(line).Append('\n');
                }
                return;

            case "ul" or "ol":
                var index = 1;
                foreach (var item in element.Children.Where(child => child.LocalName == "li"))
                {
                    var marker = element.LocalName == "ol" ? $"{index++}. " : "- ";
                    var body = EscapeBlockStart(Inline(item));
                    if (body.Length > 0)
                        sb.Append('\n').Append(marker).Append(body);
                }
                sb.Append('\n');
                return;

            case "img":
                var image = Inline(element);
                if (image.Length > 0)
                    sb.Append("\n\n").Append(image).Append('\n');
                return;

            case "table":
                WriteTable(sb, element);
                return;

            default:
                if (!element.Children.Any(child => IsBlockTag(child)))
                {
                    sb.Append(Inline(element));
                    return;
                }

                // Adjacent block children must not run together ("<div>A</div><div>B</div>").
                foreach (var child in element.ChildNodes)
                {
                    WriteBlock(sb, child);
                    if (child is IElement blockChild && IsBlockTag(blockChild) && !EndsWithNewline(sb))
                        sb.Append('\n');
                }

                return;
        }
    }

    /// <summary>Keeps paragraph text that would otherwise become a block construct.</summary>
    private static string EscapeBlockStart(string text)
    {
        if (text.Length == 0)
            return text;

        if (text[0] == '>')
            return "\\" + text;

        if (text[0] == '#')
        {
            var hashes = 0;
            while (hashes < text.Length && text[hashes] == '#')
                hashes++;
            return hashes == text.Length || char.IsWhiteSpace(text[hashes]) ? "\\" + text : text;
        }

        if (text[0] is '-' or '+' or '*')
            return text.Length == 1 || char.IsWhiteSpace(text[1]) ? "\\" + text : text;

        var digits = 0;
        while (digits < text.Length && char.IsAsciiDigit(text[digits]))
            digits++;
        if (digits > 0 && digits < text.Length && text[digits] is '.' or ')' &&
            (digits + 1 == text.Length || char.IsWhiteSpace(text[digits + 1])))
        {
            return text[..digits] + "\\" + text[digits..];
        }

        return text;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\n{3,}")]
    private static partial System.Text.RegularExpressions.Regex ExcessNewlines();

    private static readonly HashSet<string> BlockTags = new(StringComparer.Ordinal)
    {
        "address", "article", "aside", "blockquote", "details", "div", "dl", "fieldset",
        "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "hr", "li", "main", "nav", "ol", "p", "pre", "section", "table", "ul"
    };

    private static bool IsBlockTag(IElement element) => BlockTags.Contains(element.LocalName);

    private static bool EndsWithNewline(StringBuilder sb) => sb.Length > 0 && sb[^1] == '\n';

    private static void WriteTable(StringBuilder sb, IElement table)
    {
        var rows = table.QuerySelectorAll("tr");
        if (rows.Length == 0)
        {
            var fallback = Inline(table);
            if (fallback.Length > 0)
                sb.Append("\n\n").Append(fallback).Append('\n');
            return;
        }

        sb.Append("\n\n");
        for (var i = 0; i < rows.Length; i++)
        {
            var cells = rows[i].Children
                .Where(cell => cell.LocalName is "td" or "th")
                .Select(Inline);
            sb.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
            if (i == 0)
                sb.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", Math.Max(1, rows[i].Children.Length)))).Append('\n');
        }
    }

    private static string Inline(INode node)
    {
        var sb = new StringBuilder();
        AppendInline(sb, node);
        return Collapse(sb.ToString()).Trim();
    }

    private static string InlineChildren(IElement element)
    {
        var sb = new StringBuilder();
        foreach (var child in element.ChildNodes)
            AppendInline(sb, child);
        return Collapse(sb.ToString()).Trim();
    }

    private static void AppendInline(StringBuilder sb, INode node)
    {
        if (node is IText text)
        {
            // Keep word boundaries here; Inline() collapses and trims at the end.
            sb.Append(text.Data.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '));
            return;
        }

        if (node is not IElement element)
            return;

        switch (element.LocalName)
        {
            case "script" or "style" or "noscript" or "template":
                return;
            case "br":
                sb.Append('\n');
                return;
            case "strong" or "b":
                sb.Append("**").Append(InlineChildren(element)).Append("**");
                return;
            case "em" or "i":
                sb.Append('*').Append(InlineChildren(element)).Append('*');
                return;
            case "code":
                sb.Append('`').Append(Collapse(element.TextContent)).Append('`');
                return;
            case "a":
                var href = element.GetAttribute("href") ?? "";
                var label = InlineChildren(element);
                if (label.Length == 0)
                    return;
                sb.Append(href.Length == 0 ? label : $"[{label}]({href})");
                return;
            case "img":
                var src = element.GetAttribute("src") ?? "";
                // A lazy image often has a placeholder in src and the real file in
                // data-src; prefer the real file for the Markdown body.
                if (src.Length == 0 || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    src = element.GetAttribute("data-src")
                          ?? element.GetAttribute("data-original")
                          ?? FirstSrcsetUrl(element.GetAttribute("srcset"))
                          ?? src;
                }
                if (src.Length > 0)
                    sb.Append($"![{element.GetAttribute("alt") ?? ""}]({src})");
                return;
            default:
                foreach (var child in element.ChildNodes)
                    AppendInline(sb, child);
                return;
        }
    }

    private static string? FirstSrcsetUrl(string? srcset)
        => string.IsNullOrWhiteSpace(srcset)
            ? null
            : srcset.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(candidate => candidate.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length > 0)
                .Select(parts => parts[0])
                .FirstOrDefault();

    private static string Collapse(string value)
        => string.Join(' ', value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
