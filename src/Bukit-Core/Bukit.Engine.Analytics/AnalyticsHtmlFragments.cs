namespace Bukit.Engine.Analytics;

internal sealed record AnalyticsHtmlFragments(
    string ProviderKey,
    string? HeadStart = null,
    string? HeadEnd = null,
    string? BodyStart = null);
