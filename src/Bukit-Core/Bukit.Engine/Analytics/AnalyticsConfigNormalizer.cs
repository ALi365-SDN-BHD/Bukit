using System.Globalization;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Analytics;

internal static class AnalyticsConfigNormalizer
{
    internal static ResolvedAnalyticsConfig Normalize(AnalyticsConfig config)
        => new()
        {
            Enabled = config.Enabled,
            ProductionOnly = config.ProductionOnly,
            Providers = config.Providers.Select(NormalizeProvider).ToArray()
        };

    private static ResolvedAnalyticsProvider NormalizeProvider(AnalyticsProviderConfig provider)
        => provider.Type switch
        {
            "google-analytics" => Create(
                provider.Type,
                "measurementId",
                provider.MeasurementId!),
            "google-tag-manager" => Create(
                provider.Type,
                "containerId",
                provider.ContainerId!),
            "plausible" => Create(
                provider.Type,
                "domain",
                new IdnMapping().GetAscii(provider.Domain!).ToLowerInvariant(),
                ("mode", provider.SnippetMode!),
                ("scriptUrl", provider.ScriptUrl!)),
            "umami" => Create(
                provider.Type,
                "websiteId",
                Guid.Parse(provider.WebsiteId!).ToString("D").ToLowerInvariant(),
                ("scriptUrl", provider.ScriptUrl!)),
            _ => throw new ConfigException(
                $"Unsupported analytics provider type: {provider.Type}",
                DiagnosticCode.ConfigInvalidValue)
        };

    private static ResolvedAnalyticsProvider Create(
        string type,
        string primaryOptionName,
        string primaryOptionValue,
        params (string Name, string Value)[] additionalOptions)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [primaryOptionName] = primaryOptionValue
        };

        foreach (var (name, value) in additionalOptions)
        {
            options[name] = value;
        }

        return new ResolvedAnalyticsProvider
        {
            Type = type,
            Key = $"{type}:{primaryOptionValue}",
            Options = options
        };
    }
}
