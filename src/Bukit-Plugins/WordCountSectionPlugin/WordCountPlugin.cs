using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Plugins;

public sealed class WordCountPlugin : ISectionPlugin
{
    public SectionHook SupportedHook => SectionHook.AfterRender;

    public Task ExecuteAsync(SectionContext context, CancellationToken ct = default)
    {
        if (context.RenderedHtml is null) return Task.CompletedTask;

        var wordCount = CountWords(context.RenderedHtml);
        var charCount = context.RenderedHtml.Length;

        var badge = $"""
            <div style="border-top:1px solid #e2e8f0;margin-top:16px;padding-top:12px;font-size:0.85rem;color:#64748b">
              {wordCount:N0} words · {charCount:N0} characters
            </div>
            """;

        context.RenderedHtml += badge;

        return Task.CompletedTask;
    }

    private static int CountWords(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
