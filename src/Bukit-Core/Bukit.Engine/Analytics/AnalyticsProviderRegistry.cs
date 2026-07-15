using Bukit.Shared;

namespace Bukit.Engine.Analytics;

internal sealed class AnalyticsProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IAnalyticsProvider> _providers;

    internal AnalyticsProviderRegistry(IEnumerable<IAnalyticsProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.Type,
            StringComparer.OrdinalIgnoreCase);
    }

    internal static AnalyticsProviderRegistry CreateDefault()
        => new(
        [
            new GoogleAnalyticsProvider(),
            new GoogleTagManagerProvider(),
            new PlausibleProvider(),
            new UmamiProvider()
        ]);

    internal IAnalyticsProvider GetRequired(string type)
        => _providers.TryGetValue(type, out var provider)
            ? provider
            : throw new ConfigException(
                $"Unsupported analytics provider type: {type}",
                DiagnosticCode.ConfigInvalidValue);
}
