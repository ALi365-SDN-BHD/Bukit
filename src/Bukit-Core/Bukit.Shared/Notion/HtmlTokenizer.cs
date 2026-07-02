namespace Bukit.Shared.Notion;

public static class HtmlTokenizer
{
    public enum HtmlTokenType
    {
        OpenTag, CloseTag, SelfClosingTag, Text
    }

    public sealed class HtmlToken
    {
        public HtmlTokenType Type { get; init; }
        public string TagName { get; init; } = "";
        public string Attributes { get; init; } = "";
        public string TextContent { get; init; } = "";
    }

    public static List<HtmlToken> Tokenize(string html)
    {
        var tokens = new List<HtmlToken>();
        var i = 0;

        while (i < html.Length)
        {
            if (html[i] == '<')
            {
                var tagEnd = html.IndexOf('>', i);
                if (tagEnd < 0) break;

                var tagContent = html[(i + 1)..tagEnd];
                i = tagEnd + 1;

                if (tagContent.StartsWith('/'))
                {
                    tokens.Add(new HtmlToken
                    {
                        Type = HtmlTokenType.CloseTag,
                        TagName = ExtractTagName(tagContent[1..])
                    });
                }
                else if (tagContent.EndsWith('/'))
                {
                    var selfClosingContent = tagContent[..^1].TrimEnd();
                    tokens.Add(new HtmlToken
                    {
                        Type = HtmlTokenType.SelfClosingTag,
                        TagName = ExtractTagName(selfClosingContent),
                        Attributes = selfClosingContent
                    });
                }
                else
                {
                    tokens.Add(new HtmlToken
                    {
                        Type = HtmlTokenType.OpenTag,
                        TagName = ExtractTagName(tagContent),
                        Attributes = tagContent
                    });
                }
            }
            else
            {
                var nextTag = html.IndexOf('<', i);
                var textEnd = nextTag >= 0 ? nextTag : html.Length;
                var text = html[i..textEnd];
                i = textEnd;

                var trimmed = DecodeHtmlEntities(text.Trim());
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    tokens.Add(new HtmlToken
                    {
                        Type = HtmlTokenType.Text,
                        TextContent = trimmed
                    });
                }
            }
        }

        return tokens;
    }

    public static string ExtractTagName(string tagContent)
    {
        tagContent = tagContent.Trim();
        var space = tagContent.IndexOf(' ');
        var name = space >= 0 ? tagContent[..space] : tagContent;
        return name.Trim().ToLowerInvariant();
    }

    public static string DecodeHtmlEntities(string text)
    {
        return text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&nbsp;", " ");
    }
}
