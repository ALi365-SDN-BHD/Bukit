namespace Bukit.Plugins;

public sealed class WordCountPlugin
{
    public string AppendBadge(string renderedHtml)
    {
        if (string.IsNullOrEmpty(renderedHtml)) return renderedHtml;

        var wordCount = CountWords(renderedHtml);
        var charCount = renderedHtml.Length;

        var badge = $"""
            <div style="border-top:1px solid #e2e8f0;margin-top:16px;padding-top:12px;font-size:0.85rem;color:#64748b">
              {wordCount:N0} words · {charCount:N0} characters
            </div>
            """;

        return renderedHtml + badge;
    }

    private static int CountWords(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
