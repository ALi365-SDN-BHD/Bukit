namespace Bukit.Engine.Analytics;

internal sealed record ResolvedAnalyticsConfig
{
    public bool Enabled { get; init; }
    public bool ProductionOnly { get; init; }
    public IReadOnlyList<ResolvedAnalyticsProvider> Providers { get; init; }
        = Array.Empty<ResolvedAnalyticsProvider>();
}

internal sealed record ResolvedAnalyticsProvider
{
    public required string Type { get; init; }
    public required string Key { get; init; }
    public IReadOnlyDictionary<string, string> Options { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
