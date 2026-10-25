namespace Bukit.Engine.Analytics;

internal sealed class UmamiProvider : IAnalyticsProvider
{
    public string Type => "umami";

    public AnalyticsHtmlFragments Render(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
        => new(
            provider.Key,
            HeadEnd: $"<script defer src=\"{AnalyticsValueEncoder.HtmlAttribute(provider.Options["scriptUrl"])}\" data-website-id=\"{AnalyticsValueEncoder.HtmlAttribute(provider.Options["websiteId"])}\"></script>");
}
