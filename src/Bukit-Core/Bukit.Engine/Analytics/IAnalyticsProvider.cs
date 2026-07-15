namespace Bukit.Engine.Analytics;

internal interface IAnalyticsProvider
{
    string Type { get; }

    AnalyticsHtmlFragments Render(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context);
}
