using System.Net;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bukit.Routing;

namespace Bukit.Engine.Plugins.BuiltIn.MiniProgram;

/// <summary>
/// Reduces a page body to the HTML subset WeChat <c>rich-text</c> renders: allowed tags keep only
/// safe attributes, unknown containers are unwrapped, embeds become links, collapsible blocks are
/// expanded, and every link and image is absolute.
/// </summary>
internal sealed class MiniProgramHtmlSanitizer : IDisposable
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "blockquote", "pre", "code",
        "strong", "em", "b", "i", "u", "s", "del", "sub", "sup", "a", "img", "table", "thead",
        "tbody", "tfoot", "tr", "th", "td", "hr", "br", "span"
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.Ordinal) { "img", "hr", "br" };

    // Removed with their content: executable, interactive or invisible elements.
    private static readonly HashSet<string> DroppedTags = new(StringComparer.Ordinal)
    {
        "script", "style", "noscript", "template", "form", "input", "button", "select", "textarea",
        "object", "embed", "svg", "math", "head", "title", "meta", "link", "canvas"
    };

    // Replaced by a paragraph that links to their source.
    private static readonly HashSet<string> EmbedTags = new(StringComparer.Ordinal) { "iframe", "video", "audio" };

    // Unwrapped like other unknown tags, but each still starts a new block: text directly inside one
    // becomes a paragraph, so sibling containers and captions do not run into each other.
    private static readonly HashSet<string> BlockContainers = new(StringComparer.Ordinal)
    {
        "div", "section", "article", "main", "header", "footer", "aside", "nav", "address", "figure",
        "figcaption", "details", "dl", "dt", "dd", "fieldset", "legend", "center"
    };

    // Elements whose output already starts a block of its own.
    private static readonly HashSet<string> BlockTags = new(StringComparer.Ordinal)
    {
        "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "blockquote", "pre", "table", "hr",
        "summary", "iframe", "video", "audio"
    };

    private readonly MiniProgramUrls _urls;
    private readonly Dictionary<string, string> _internalIds = new(StringComparer.Ordinal);

    // One parser and fragment context per build: every body of a collection goes through here.
    private readonly HtmlParser _parser = new();
    private readonly IDocument _context;

    /// <param name="internalIds">Absolute page URL → mini-program detail id; keys are normalized with <see cref="NormalizePageUrl"/>.</param>
    internal MiniProgramHtmlSanitizer(MiniProgramUrls urls, IReadOnlyDictionary<string, string> internalIds)
    {
        _urls = urls;
        foreach (var (url, id) in internalIds)
        {
            _internalIds.TryAdd(NormalizePageUrl(url), id);
        }

        _context = _parser.ParseDocument("<!DOCTYPE html><html><body></body></html>");
    }

    public void Dispose() => _context.Dispose();

    /// <summary>Returns the sanitized body, or throws <see cref="MiniProgramSignedUrlException"/> for an unlocalized signed image.</summary>
    internal string Sanitize(string? html, string pageUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var nodes = _parser.ParseFragment(html, _context.Body!);
        var builder = new StringBuilder(html.Length);
        foreach (var node in nodes)
        {
            Write(builder, node, pageUrl);
        }

        return builder.ToString().Trim();
    }

    private void Write(StringBuilder builder, INode node, string pageUrl)
    {
        switch (node)
        {
            case IText text:
                builder.Append(WebUtility.HtmlEncode(text.Data));
                return;
            case IElement element:
                WriteElement(builder, element, pageUrl);
                return;
        }
    }

    private void WriteElement(StringBuilder builder, IElement element, string pageUrl)
    {
        var tag = element.LocalName;
        // A caption is written by its table, as a paragraph before it: rich-text has no caption.
        if (DroppedTags.Contains(tag) || tag == "caption")
        {
            return;
        }

        if (EmbedTags.Contains(tag))
        {
            var source = element.GetAttribute("src") ?? element.QuerySelector("source[src]")?.GetAttribute("src");
            var href = source is null ? null : SafeHref(source, pageUrl);
            if (href is not null)
            {
                var encoded = WebUtility.HtmlEncode(href);
                builder.Append("<p><a href=\"").Append(encoded).Append("\">").Append(encoded).Append("</a></p>");
            }

            return;
        }

        if (tag == "summary")
        {
            builder.Append("<p><strong>");
            WriteChildren(builder, element, pageUrl);
            builder.Append("</strong></p>");
            return;
        }

        if (BlockContainers.Contains(tag))
        {
            WriteBlockContainer(builder, element, pageUrl);
            return;
        }

        if (!AllowedTags.Contains(tag))
        {
            WriteChildren(builder, element, pageUrl);
            return;
        }

        var attributes = new List<(string Name, string Value)>();
        switch (tag)
        {
            case "a":
                if (element.GetAttribute("href") is { } rawHref && SafeHref(rawHref, pageUrl) is { } href)
                {
                    attributes.Add(("href", href));
                    if (_internalIds.TryGetValue(NormalizePageUrl(href), out var id))
                    {
                        attributes.Add(("data-bukit-id", id));
                    }
                }

                break;
            case "img":
                var src = element.GetAttribute("src");
                if (string.IsNullOrWhiteSpace(src))
                {
                    return;
                }

                var absolute = _urls.Absolute(src, pageUrl);
                if (!IsHttp(absolute))
                {
                    return;
                }

                if (MiniProgramJson.IsSignedUrl(absolute))
                {
                    throw new MiniProgramSignedUrlException(absolute);
                }

                attributes.Add(("src", absolute));
                if (element.GetAttribute("alt") is { Length: > 0 } alt)
                {
                    attributes.Add(("alt", alt));
                }

                break;
            case "td" or "th":
                AddInteger(element, "colspan", attributes);
                AddInteger(element, "rowspan", attributes);
                break;
        }

        if (tag == "table" && element.Children.FirstOrDefault(child => child.LocalName == "caption") is { } caption)
        {
            AppendParagraph(builder, caption, pageUrl);
        }

        builder.Append('<').Append(tag);
        foreach (var (name, value) in attributes)
        {
            builder.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
        }

        builder.Append('>');
        if (VoidTags.Contains(tag))
        {
            return;
        }

        WriteChildren(builder, element, pageUrl);
        builder.Append("</").Append(tag).Append('>');
    }

    private void WriteChildren(StringBuilder builder, IElement element, string pageUrl)
    {
        foreach (var child in element.ChildNodes)
        {
            Write(builder, child, pageUrl);
        }
    }

    private void WriteBlockContainer(StringBuilder builder, IElement element, string pageUrl)
    {
        if (element.Children.Any(child => BlockTags.Contains(child.LocalName) || BlockContainers.Contains(child.LocalName)))
        {
            WriteChildren(builder, element, pageUrl);
            return;
        }

        // Inside a table cell or list item a paragraph would add margins the website does not show,
        // so line breaks separate the container from visible content before and after it.
        if (element.ParentElement?.Closest("td, th, li") is not null)
        {
            var inner = new StringBuilder();
            WriteChildren(inner, element, pageUrl);
            var html = inner.ToString().Trim();
            if (html.Length == 0)
            {
                return;
            }

            // A block sibling already starts or ends the line, so only inline content needs a break.
            if (NearestVisibleSibling(element, forward: false) is { } before && !IsBlock(before) && !EndsWithBreak(builder))
            {
                builder.Append("<br>");
            }

            builder.Append(html);
            if (NearestVisibleSibling(element, forward: true) is { } after && !IsBlock(after))
            {
                builder.Append("<br>");
            }

            return;
        }

        AppendParagraph(builder, element, pageUrl);
    }

    /// <summary>Writes the element's children as one paragraph, or nothing when they are empty.</summary>
    private void AppendParagraph(StringBuilder builder, IElement element, string pageUrl)
    {
        var inner = new StringBuilder();
        WriteChildren(inner, element, pageUrl);
        var html = inner.ToString().Trim();
        if (html.Length > 0)
        {
            builder.Append("<p>").Append(html).Append("</p>");
        }
    }

    /// <summary>
    /// The nearest sibling after (or before) <paramref name="node"/> that writes visible output, or null.
    /// The scan stops at the first visible sibling, and each check stops at the first visible descendant.
    /// </summary>
    private static INode? NearestVisibleSibling(INode node, bool forward)
    {
        for (var sibling = forward ? node.NextSibling : node.PreviousSibling; sibling is not null; sibling = forward ? sibling.NextSibling : sibling.PreviousSibling)
        {
            if (IsVisible(sibling))
            {
                return sibling;
            }
        }

        return null;
    }

    // Containers inside a cell are written inline (separated by breaks), so only real blocks count.
    private static bool IsBlock(INode node)
        => node is IElement element && BlockTags.Contains(element.LocalName);

    // Text or media that the sanitizer keeps; dropped elements (and everything inside them) write nothing.
    private static bool IsVisible(INode node)
    {
        switch (node)
        {
            case IText text:
                return !string.IsNullOrWhiteSpace(text.Data);
            case IElement element when DroppedTags.Contains(element.LocalName) || element.LocalName == "caption":
                return false;
            case IElement element when VoidTags.Contains(element.LocalName) || EmbedTags.Contains(element.LocalName):
                return true;
            case IElement element:
                foreach (var child in element.ChildNodes)
                {
                    if (IsVisible(child))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private static bool EndsWithBreak(StringBuilder builder)
    {
        const string lineBreak = "<br>";
        if (builder.Length < lineBreak.Length)
        {
            return false;
        }

        for (var i = 0; i < lineBreak.Length; i++)
        {
            if (builder[builder.Length - lineBreak.Length + i] != lineBreak[i])
            {
                return false;
            }
        }

        return true;
    }

    private string? SafeHref(string value, string pageUrl)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('#'))
        {
            return null;
        }

        if (trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var absolute = _urls.Absolute(trimmed, pageUrl);
        return IsHttp(absolute) ? absolute : null;
    }

    private static void AddInteger(IElement element, string name, List<(string Name, string Value)> attributes)
    {
        if (int.TryParse(element.GetAttribute(name), out var value) && value is > 1 and <= 1000)
        {
            attributes.Add((name, value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private static bool IsHttp(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";

    /// <summary>
    /// The comparable form of a page URL: no query or fragment, canonical escaping and host case,
    /// <c>index.html</c> dropped and the route trailing-slash rule applied, so <c>/a</c>,
    /// <c>/a/</c> and <c>/a/index.html</c> name the same page.
    /// </summary>
    internal static string NormalizePageUrl(string url)
    {
        var end = url.IndexOfAny(['?', '#']);
        var trimmed = end < 0 ? url : url[..end];
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed;
        }

        var path = uri.AbsolutePath;
        if (path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"index.html".Length];
        }

        // The route rule decides page versus file, so /v1.2 and /v1.2/ name the same page.
        return uri.GetLeftPart(UriPartial.Authority) + RoutePathBuilder.NormalizeUrl(path);
    }
}

internal sealed class MiniProgramSignedUrlException(string url)
    : Exception($"signed URL was not localized: {MiniProgramJson.RedactQuery(url)}");
