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
        =>
        [
            .. Bukit.Notion.Conversion.HtmlTokenizer.Tokenize(html)
                .Select(static token => new HtmlToken
                {
                    Type = (HtmlTokenType)(int)token.Type,
                    TagName = token.TagName,
                    Attributes = token.Attributes,
                    TextContent = token.TextContent
                })
        ];

    public static string ExtractTagName(string tagContent)
        => Bukit.Notion.Conversion.HtmlTokenizer.ExtractTagName(tagContent);

    public static string DecodeHtmlEntities(string text)
        => Bukit.Notion.Conversion.HtmlTokenizer.DecodeHtmlEntities(text);
}
