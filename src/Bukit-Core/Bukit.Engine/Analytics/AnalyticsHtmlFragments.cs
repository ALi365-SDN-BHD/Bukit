namespace Bukit.Engine.Analytics;

internal sealed record AnalyticsHtmlFragments(
    string ProviderKey,
    string? HeadEnd = null,
    string? BodyStart = null);
