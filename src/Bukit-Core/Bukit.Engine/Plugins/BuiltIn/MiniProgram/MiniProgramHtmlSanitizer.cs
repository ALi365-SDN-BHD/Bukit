using System.Net;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

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

    private readonly string _siteUrl;
    private readonly string _baseUrl;
    private readonly Dictionary<string, string> _internalIds = new(StringComparer.Ordinal);

    // One parser and fragment context per build: every body of a collection goes through here.
    private readonly HtmlParser _parser = new();
    private readonly IDocument _context;

    /// <param name="internalIds">Absolute page URL → mini-program detail id; keys are normalized with <see cref="NormalizePageUrl"/>.</param>
    internal MiniProgramHtmlSanitizer(string siteUrl, string baseUrl, IReadOnlyDictionary<string, string> internalIds)
    {
        _siteUrl = siteUrl;
        _baseUrl = baseUrl;
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
        if (DroppedTags.Contains(tag))
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

                var absolute = MiniProgramJson.Absolute(_siteUrl, _baseUrl, src, pageUrl);
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

        var absolute = MiniProgramJson.Absolute(_siteUrl, _baseUrl, trimmed, pageUrl);
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
    /// <c>index.html</c> dropped and a trailing slash on extension-less paths, so <c>/a</c>,
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
        else if (!path.EndsWith('/') && !path[(path.LastIndexOf('/') + 1)..].Contains('.', StringComparison.Ordinal))
        {
            path += "/";
        }

        return uri.GetLeftPart(UriPartial.Authority) + path;
    }
}

internal sealed class MiniProgramSignedUrlException(string url)
    : Exception($"signed URL was not localized: {MiniProgramJson.RedactQuery(url)}");
