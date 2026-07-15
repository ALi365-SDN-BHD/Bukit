namespace Bukit.Engine.Analytics;

internal sealed class PlausibleProvider : IAnalyticsProvider
{
    public string Type => "plausible";

    public AnalyticsHtmlFragments Render(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
        => new(
            provider.Key,
            HeadEnd: $"<script defer data-domain=\"{AnalyticsValueEncoder.HtmlAttribute(provider.Options["domain"])}\" src=\"{AnalyticsValueEncoder.HtmlAttribute(provider.Options["scriptUrl"])}\"></script>");
}
